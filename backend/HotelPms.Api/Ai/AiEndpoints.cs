using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public static class AiEndpoints
{
    static readonly HashSet<string> Modules = ["DASHBOARD", "BOOKING", "CHECK_IN", "CHECK_OUT", "ROOM_MANAGEMENT", "ROOM_TRANSFER", "HOUSEKEEPING", "RATE_MANAGEMENT", "OTA", "GUEST", "FOLIO", "PAYMENT", "CASHIER", "DEBT", "INVENTORY", "SUPPLIER", "REPORT", "SYSTEM_ERROR", "OTHER"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/ai/chat", Chat);
        api.MapGet("/ai/conversations", ListConversations);
        api.MapGet("/ai/conversations/{id:guid}", GetConversation);
        api.MapDelete("/ai/conversations/{id:guid}", DeleteConversation);
        api.MapPost("/ai/feedback", Feedback);
        api.MapPost("/admin/ai/knowledge/upload", Upload).DisableAntiforgery();
        api.MapGet("/admin/ai/knowledge", ListKnowledge);
        api.MapGet("/admin/ai/knowledge/{id:guid}", GetKnowledge);
        api.MapPost("/admin/ai/knowledge/{id:guid}/approve", Approve);
        api.MapPost("/admin/ai/knowledge/{id:guid}/reject", Reject);
        api.MapPost("/admin/ai/knowledge/{id:guid}/reindex", Reindex);
        api.MapDelete("/admin/ai/knowledge/{id:guid}", DeleteKnowledge);
        api.MapGet("/admin/ai/golden-dataset", ListGolden);
        api.MapPost("/admin/ai/golden-dataset", SaveGolden);
        api.MapPatch("/admin/ai/golden-dataset/{id:guid}", SaveGolden);
        api.MapPost("/admin/ai/golden-dataset/{id:guid}/approve", ApproveGolden);
        api.MapPost("/admin/ai/golden-dataset/import", ImportGolden).DisableAntiforgery();
        api.MapGet("/admin/ai/golden-dataset/export", ExportGolden);
        api.MapPost("/admin/ai/evaluations/run", RunEvaluation);
        api.MapGet("/admin/ai/evaluations", ListEvaluations);
        api.MapPost("/admin/ai/finetune/prepare", PrepareFinetune);
    }

    static async Task<IResult> Chat(AiChatBody body, ClaimsPrincipal user, IAIChatService chat, PmsDbContext db, CancellationToken ct)
    {
        var message = (body.Message ?? "").Trim();
        if (message.Length is 0 or > 2000) return Ops.Fail("Câu hỏi cần từ 1 đến 2000 ký tự");
        var module = Modules.Contains(body.Context?.Module ?? "") ? body.Context!.Module! : "OTHER";
        var propertyId = Scope(user, body.Context?.PropertyId);
        if (propertyId.Denied) return Results.Forbid();
        try
        {
            var access = await StaffAccess.Load(db, user);
            var result = await chat.AskAsync(new AiChatRequest(body.ConversationId, message, module, propertyId.Id), Ops.UserId(user), propertyId.Id, access.Keys.Contains("report.finance"), ct);
            return Results.Ok(new
            {
                conversationId = result.ConversationId,
                message = new { id = result.MessageId, content = result.Content, createdAt = result.CreatedAt },
                citations = result.Citations.Select(x => new { documentId = x.DocumentId, title = x.Title, page = (int?)null, section = x.Section, excerpt = x.Excerpt }),
                requiresEscalation = result.RequiresEscalation,
                suggestedQuestions = Suggestions(module)
            });
        }
        catch (InvalidOperationException ex)
        {
            return Ops.Fail(ex.Message);
        }
    }

    static async Task<IResult> ListConversations(ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        var userId = Ops.UserId(user);
        var rows = await db.AiConversations.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc).Take(30)
            .Select(x => new { x.Id, x.Module, x.CreatedAtUtc }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    static async Task<IResult> GetConversation(Guid id, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        var userId = Ops.UserId(user);
        var conversation = await db.AiConversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (conversation == null) return Results.NotFound(new { message = "Không thấy hội thoại" });
        var messages = await db.AiMessages.AsNoTracking().Where(x => x.ConversationId == id).OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.Role, x.Content, x.CitationsJson, x.CreatedAtUtc }).ToListAsync(ct);
        return Results.Ok(new { conversation, messages });
    }

    static async Task<IResult> DeleteConversation(Guid id, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        var row = await db.AiConversations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Ops.UserId(user), ct);
        if (row == null) return Results.NotFound(new { message = "Không thấy hội thoại" });
        db.AiConversations.Remove(row);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { deleted = true });
    }

    static async Task<IResult> Feedback(AiFeedbackBody body, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        var userId = Ops.UserId(user);
        var owns = await (from message in db.AiMessages
            join conversation in db.AiConversations on message.ConversationId equals conversation.Id
            where message.Id == body.MessageId && conversation.UserId == userId && message.Role == "assistant"
            select message.Id).AnyAsync(ct);
        if (!owns) return Results.Forbid();
        db.AiAnswerFeedback.Add(new AiAnswerFeedback { Id = Guid.NewGuid(), MessageId = body.MessageId, UserId = userId, Helpful = body.Helpful, Note = Ops.TrimOrNull(body.Note), CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { saved = true });
    }

    static async Task<IResult> Upload(IFormFile file, string? title, string? module, string? version, int? propertyId, ClaimsPrincipal user, IKnowledgeIngestionService ingestion, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var scope = Scope(user, propertyId);
        if (scope.Denied) return Results.Forbid();
        if (file.Length is 0 or > 1_048_576) return Ops.Fail("Tệp rỗng hoặc lớn hơn 1 MB");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".txt" or ".md" or ".csv" or ".docx")) return Ops.Fail("Chỉ nhận TXT, Markdown, CSV và DOCX. PDF và XLSX chưa có bộ đọc.");
        await using var stream = file.OpenReadStream();
        string text;
        if (ext == ".docx") text = ReadDocx(stream);
        else using (var reader = new StreamReader(stream, Encoding.UTF8, true)) text = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(text)) return Ops.Fail("Không đọc được nội dung tệp");
        var saved = await ingestion.SaveAsync(new AiUpload(Path.GetFileName(file.FileName), title ?? "", Modules.Contains(module ?? "") ? module! : "OTHER", version ?? "1", scope.Id, text), Ops.UserId(user), ct);
        return Results.Ok(new { saved.Id, saved.Code, saved.Status, message = "Đã lưu bản nháp. Cần duyệt trước khi trợ lý dùng." });
    }

    static async Task<IResult> ListKnowledge(ClaimsPrincipal user, int? propertyId, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var scope = Scope(user, propertyId);
        if (scope.Denied) return Results.Forbid();
        var query = db.AiKnowledgeDocuments.AsNoTracking().AsQueryable();
        if (scope.Id is int id) query = query.Where(x => x.PropertyId == null || x.PropertyId == id);
        var rows = await query.OrderByDescending(x => x.UploadedAtUtc).Select(x => new { x.Id, x.Code, x.Title, x.FileName, x.Module, x.Status, x.VersionLabel, x.UploadedAtUtc }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    static async Task<IResult> GetKnowledge(Guid id, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var doc = await db.AiKnowledgeDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (doc == null || !CanSee(user, doc.PropertyId)) return Results.NotFound(new { message = "Không thấy tài liệu" });
        var chunks = await db.AiKnowledgeChunks.AsNoTracking().Where(x => x.DocumentId == id).OrderBy(x => x.Ordinal).Select(x => new { x.Ordinal, x.Heading, x.Content }).ToListAsync(ct);
        return Results.Ok(new { doc.Id, doc.Code, doc.Title, doc.Status, doc.Module, doc.VersionLabel, chunks });
    }

    static async Task<IResult> Approve(Guid id, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct) => await SetStatus(id, "APPROVED", user, db, ct);
    static async Task<IResult> Reject(Guid id, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct) => await SetStatus(id, "REJECTED", user, db, ct);

    static async Task<IResult> SetStatus(Guid id, string status, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var doc = await db.AiKnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (doc == null || !CanSee(user, doc.PropertyId)) return Results.NotFound(new { message = "Không thấy tài liệu" });
        doc.Status = status;
        doc.ApprovedByUserId = status == "APPROVED" ? Ops.UserId(user) : null;
        doc.ApprovedAtUtc = status == "APPROVED" ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { doc.Id, doc.Status });
    }

    static async Task<IResult> Reindex(Guid id, ClaimsPrincipal user, PmsDbContext db, IKnowledgeIngestionService ingestion, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var doc = await db.AiKnowledgeDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (doc == null || !CanSee(user, doc.PropertyId)) return Results.NotFound(new { message = "Không thấy tài liệu" });
        await ingestion.ReindexAsync(id, ct);
        return Results.Ok(new { id, reindexed = true });
    }

    static async Task<IResult> DeleteKnowledge(Guid id, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var doc = await db.AiKnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (doc == null || !CanSee(user, doc.PropertyId)) return Results.NotFound(new { message = "Không thấy tài liệu" });
        db.AiKnowledgeDocuments.Remove(doc);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { deleted = true });
    }

    static async Task<IResult> ListGolden(ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var rows = await db.AiGoldenQuestions.AsNoTracking().OrderByDescending(x => x.UpdatedAtUtc).Take(200).ToListAsync(ct);
        return Results.Ok(rows);
    }

    static async Task<IResult> SaveGolden(AiGoldenInput body, Guid? id, ClaimsPrincipal user, IGoldenDatasetService golden, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(body.Question) || string.IsNullOrWhiteSpace(body.ExpectedAnswer)) return Ops.Fail("Cần câu hỏi và đáp án chuẩn");
        var saved = await golden.SaveAsync(body with { Id = id ?? body.Id }, ct);
        return Results.Ok(new { saved.Id, saved.ApprovalStatus });
    }

    static async Task<IResult> ApproveGolden(Guid id, ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var row = await db.AiGoldenQuestions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) return Results.NotFound(new { message = "Không thấy mẫu" });
        if (row.ApprovalStatus is not ("DRAFT" or "IN_REVIEW")) return Ops.Fail("Chỉ duyệt mẫu đang nháp hoặc đang xem");
        row.ApprovalStatus = "APPROVED";
        row.ApprovedByUserId = Ops.UserId(user);
        row.ApprovedAtUtc = DateTime.UtcNow;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { row.Id, row.ApprovalStatus });
    }

    static async Task<IResult> ImportGolden(IFormFile file, ClaimsPrincipal user, IGoldenDatasetService golden, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        if (file.Length is 0 or > 1_048_576) return Ops.Fail("Tệp rỗng hoặc lớn hơn 1 MB");
        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return Ops.Fail("Chỉ nhập CSV");
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true);
        var header = await reader.ReadLineAsync(ct) ?? "";
        var columns = SplitCsv(header);
        var count = 0;
        while (await reader.ReadLineAsync(ct) is string line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cells = SplitCsv(line);
            string Cell(string name)
            {
                var index = columns.FindIndex(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                return index >= 0 && index < cells.Count ? cells[index] : "";
            }
            if (string.IsNullOrWhiteSpace(Cell("question"))) continue;
            await golden.SaveAsync(new AiGoldenInput(null, Cell("dataset_code"), Cell("module"), Cell("intent"), Cell("question"), Cell("question_variations"), Cell("question_type"), Cell("difficulty"), Cell("expected_answer"), Cell("expected_keywords"), Cell("evaluation_criteria"), Cell("answerable"), Cell("source_document_ids"), Cell("source_version")), ct);
            count++;
        }
        return Results.Ok(new { imported = count, approvalStatus = "DRAFT" });
    }

    static async Task<IResult> ExportGolden(ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var rows = await db.AiGoldenQuestions.AsNoTracking().OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var builder = new StringBuilder();
        builder.AppendLine("id,dataset_code,module,intent,question,question_type,difficulty,answerable,approval_status,expected_answer");
        foreach (var row in rows)
            builder.AppendLine(string.Join(",", new[] { row.Id.ToString(), row.DatasetCode, row.Module, row.Intent, row.Question, row.QuestionType, row.Difficulty, row.Answerable, row.ApprovalStatus, row.ExpectedAnswer }.Select(Csv)));
        return Results.Text(builder.ToString(), "text/csv", Encoding.UTF8);
    }

    static async Task<IResult> RunEvaluation(ClaimsPrincipal user, int? propertyId, IAIEvaluationService evaluation, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var scope = Scope(user, propertyId);
        if (scope.Denied) return Results.Forbid();
        var run = await evaluation.RunAsync(scope.Id, ct);
        var total = run.Passed + run.Failed;
        return Results.Ok(new { run.Id, run.Passed, run.Failed, total, accuracy = total == 0 ? 0 : Math.Round(100m * run.Passed / total, 1), retrieval = run.RetrievalMode, model = run.ModelName });
    }

    static async Task<IResult> PrepareFinetune(ClaimsPrincipal user, PmsDbContext db, IAiRuntime ai, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var rows = await db.AiGoldenQuestions.AsNoTracking().Where(x => x.ApprovalStatus == "APPROVED").ToListAsync(ct);
        var status = await ai.PrepareFinetuneAsync(rows, ct);
        return Results.Ok(new { status.Exported, status.Trainable, status.Reason, file = "AIservice/data/finetune.jsonl" });
    }

    static async Task<IResult> ListEvaluations(ClaimsPrincipal user, PmsDbContext db, CancellationToken ct)
    {
        if (!Ops.CanManage(user)) return Results.Forbid();
        var rows = await db.AiEvaluationRuns.AsNoTracking().OrderByDescending(x => x.StartedAtUtc).Take(20)
            .Select(x => new { x.Id, x.ModelName, x.RetrievalMode, x.PromptVersion, x.Passed, x.Failed, x.StartedAtUtc, x.FinishedAtUtc }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    static (int? Id, bool Denied) Scope(ClaimsPrincipal user, int? requested)
    {
        var own = Ops.UserProperty(user);
        if (own.HasValue) return (own, false);
        if (requested is > 0 && !Ops.HasScope(user, requested.Value)) return (null, true);
        return (requested is > 0 ? requested : null, false);
    }

    static bool CanSee(ClaimsPrincipal user, int? propertyId) => propertyId is null || Ops.HasScope(user, propertyId.Value);

    static string[] Suggestions(string module) => module switch
    {
        "BOOKING" => ["Tạo đặt phòng cần những gì?", "Vì sao không giữ được phòng?"],
        "CHECK_IN" => ["Điều kiện nhận phòng là gì?", "Phòng chưa kiểm thì sao?"],
        "FOLIO" or "PAYMENT" or "CASHIER" => ["Thu tiền mặt cần gì?", "Công nợ City Ledger ghi ở đâu?"],
        "HOUSEKEEPING" => ["Trạng thái vệ sinh nào được bán?", "Phiếu dọn gồm những gì?"],
        _ => ["Chức năng này dùng để làm gì?", "Lỗi thường gặp là gì?"]
    };

    static string ReadDocx(Stream stream)
    {
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null) return "";
        using var reader = new StreamReader(entry.Open());
        var xml = reader.ReadToEnd().Replace("</w:p>", "\n");
        return System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", "");
    }

    static List<string> SplitCsv(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
            else if (ch == '"') quoted = !quoted;
            else if (ch == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(ch);
        }
        cells.Add(cell.ToString());
        return cells;
    }

    static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}

public sealed record AiChatBody(Guid? ConversationId, string? Message, AiChatContext? Context);
public sealed record AiChatContext(string? Module, string? PageTitle, int? PropertyId, string? Locale);
public sealed record AiFeedbackBody(Guid MessageId, bool Helpful, string? Note);
