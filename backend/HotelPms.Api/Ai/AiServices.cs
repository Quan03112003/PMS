using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HotelPms.Api;

public interface IAiRuntime
{
    bool Enabled { get; }
    string ModelName { get; }
    Task<string?> AnswerAsync(object packet, CancellationToken ct);
    Task<AiFinetuneStatus> PrepareFinetuneAsync(IReadOnlyList<AiGoldenQuestion> rows, CancellationToken ct);
}

public sealed record AiFinetuneStatus(int Exported, bool Trainable, string Reason);

public interface IEmbeddingService
{
    bool Enabled { get; }
}

public interface IKnowledgeSearchService
{
    string Mode { get; }
    Task<IReadOnlyList<AiCitation>> SearchAsync(string question, int? propertyId, CancellationToken ct);
}

public interface IKnowledgeIngestionService
{
    Task<AiKnowledgeDocument> SaveAsync(AiUpload upload, int userId, CancellationToken ct);
    Task ReindexAsync(Guid documentId, CancellationToken ct);
}

public interface IAIChatService
{
    Task<AiChatResult> AskAsync(AiChatRequest request, int userId, int? propertyId, bool canSeeFinance, CancellationToken ct);
}

public interface IGoldenDatasetService
{
    Task<AiGoldenQuestion> SaveAsync(AiGoldenInput input, CancellationToken ct);
}

public interface IAIEvaluationService
{
    Task<AiEvaluationRun> RunAsync(int? propertyId, CancellationToken ct);
}

public sealed record AiCitation(Guid DocumentId, string Title, string Section, string Excerpt);
public sealed record AiChatRequest(Guid? ConversationId, string Message, string Module, int? PropertyId);
public sealed record AiChatResult(Guid ConversationId, Guid MessageId, string Content, IReadOnlyList<AiCitation> Citations, bool RequiresEscalation, DateTime CreatedAt);
public sealed record AiUpload(string FileName, string Title, string Module, string Version, int? PropertyId, string Text);
public sealed record AiGoldenInput(Guid? Id, string DatasetCode, string Module, string Intent, string Question, string QuestionVariations, string QuestionType, string Difficulty, string ExpectedAnswer, string ExpectedKeywords, string EvaluationCriteria, string Answerable, string SourceDocumentIds, string SourceVersion);

public sealed class DisabledEmbeddingService : IEmbeddingService
{
    public bool Enabled => false;
}

public sealed class PythonAiRuntime(HttpClient http, IConfiguration config, ILogger<PythonAiRuntime> logger) : IAiRuntime
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool Enabled => Uri.TryCreate(config["AI:ServiceUrl"], UriKind.Absolute, out _);
    public string ModelName => Enabled ? "aiservice" : "grounded-fallback";

    public async Task<string?> AnswerAsync(object packet, CancellationToken ct)
    {
        if (!Enabled) return null;
        try
        {
            using var response = await SendAsync("/v1/chat", packet, ct);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadFromJsonAsync<AiRuntimeReply>(Json, ct);
            return string.IsNullOrWhiteSpace(body?.Content) ? null : body.Content.Trim();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Python AI service did not answer");
            return null;
        }
    }

    public async Task<AiFinetuneStatus> PrepareFinetuneAsync(IReadOnlyList<AiGoldenQuestion> rows, CancellationToken ct)
    {
        if (!Enabled) throw new InvalidOperationException("Dịch vụ AI Python chưa được cấu hình.");
        var payload = new
        {
            rows = rows.Select(row => new
            {
                question = row.Question,
                expectedAnswer = row.ExpectedAnswer,
                module = row.Module,
                questionType = row.QuestionType,
                approvalStatus = row.ApprovalStatus
            })
        };
        using var response = await SendAsync("/v1/finetune/prepare", payload, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Dịch vụ AI Python chưa lập được bộ fine-tune.");
        var body = await response.Content.ReadFromJsonAsync<AiFinetuneReply>(Json, ct)
            ?? throw new InvalidOperationException("Dịch vụ AI Python chưa lập được bộ fine-tune.");
        return new AiFinetuneStatus(body.Exported, body.Trainable, body.Reason ?? "");
    }

    async Task<HttpResponseMessage> SendAsync(string path, object payload, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(config["AI:ServiceUrl"]!), path))
        {
            Content = JsonContent.Create(payload, options: Json)
        };
        var key = config["AI:ServiceKey"];
        if (!string.IsNullOrWhiteSpace(key)) request.Headers.TryAddWithoutValidation("X-Lotus-Ai-Key", key);
        return await http.SendAsync(request, ct);
    }

    sealed record AiRuntimeReply(string? Content, string? Model);
    sealed record AiFinetuneReply(int Exported, bool Trainable, string? Reason);
}

public sealed class LexicalKnowledgeSearch(PmsDbContext db) : IKnowledgeSearchService
{
    public string Mode => "lexical";

    public async Task<IReadOnlyList<AiCitation>> SearchAsync(string question, int? propertyId, CancellationToken ct)
    {
        var tokens = Tokens(question);
        if (tokens.Count == 0) return [];
        var rows = await (from chunk in db.AiKnowledgeChunks.AsNoTracking()
            join doc in db.AiKnowledgeDocuments.AsNoTracking() on chunk.DocumentId equals doc.Id
            where doc.Status == "APPROVED" && (propertyId == null || doc.PropertyId == null || doc.PropertyId == propertyId)
            select new { doc.Id, doc.Title, chunk.Heading, chunk.Content }).ToListAsync(ct);
        return rows
            .Select(row => new { row, score = tokens.Count(token => row.Content.Contains(token, StringComparison.OrdinalIgnoreCase) || row.Heading.Contains(token, StringComparison.OrdinalIgnoreCase)) })
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(5)
            .Select(x => new AiCitation(x.row.Id, x.row.Title, x.row.Heading, x.row.Content.Length <= 500 ? x.row.Content : x.row.Content[..500]))
            .ToList();
    }

    public static List<string> Tokens(string question) =>
        (question ?? "").Split([' ', '\n', '\t', ',', '.', '?', '!', ':', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length >= 2)
            .Distinct()
            .Take(24)
            .ToList();
}

public sealed class KnowledgeIngestionService(PmsDbContext db, IConfiguration config) : IKnowledgeIngestionService
{
    public async Task<AiKnowledgeDocument> SaveAsync(AiUpload upload, int userId, CancellationToken ct)
    {
        var doc = new AiKnowledgeDocument
        {
            Id = Guid.NewGuid(),
            PropertyId = upload.PropertyId,
            Code = "AI-" + Guid.NewGuid().ToString("N")[..8],
            Title = string.IsNullOrWhiteSpace(upload.Title) ? upload.FileName : upload.Title.Trim(),
            FileName = upload.FileName,
            VersionLabel = string.IsNullOrWhiteSpace(upload.Version) ? "1" : upload.Version.Trim(),
            Module = string.IsNullOrWhiteSpace(upload.Module) ? "OTHER" : upload.Module.Trim(),
            Status = "DRAFT",
            RawText = upload.Text,
            UploadedByUserId = userId,
            UploadedAtUtc = DateTime.UtcNow
        };
        db.AiKnowledgeDocuments.Add(doc);
        await ReplaceChunks(doc, ct);
        db.AiIngestionJobs.Add(new AiIngestionJob { Id = Guid.NewGuid(), DocumentId = doc.Id, Status = "DONE", Message = "Đã cắt đoạn. Tài liệu vẫn ở nháp cho đến khi được duyệt.", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task ReindexAsync(Guid documentId, CancellationToken ct)
    {
        var doc = await db.AiKnowledgeDocuments.FirstAsync(x => x.Id == documentId, ct);
        await ReplaceChunks(doc, ct);
        db.AiIngestionJobs.Add(new AiIngestionJob { Id = Guid.NewGuid(), DocumentId = doc.Id, Status = "DONE", Message = "Đã lập lại chỉ mục từ bản đã lưu.", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    async Task ReplaceChunks(AiKnowledgeDocument doc, CancellationToken ct)
    {
        var old = await db.AiKnowledgeChunks.Where(x => x.DocumentId == doc.Id).ToListAsync(ct);
        db.AiKnowledgeChunks.RemoveRange(old);
        var size = int.TryParse(config["AI:ChunkChars"], out var chars) ? chars : 800;
        var overlap = int.TryParse(config["AI:ChunkOverlap"], out var lap) ? lap : 120;
        var ordinal = 0;
        foreach (var chunk in KnowledgeChunker.Split(doc.RawText, size, overlap))
        {
            db.AiKnowledgeChunks.Add(new AiKnowledgeChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                Ordinal = ordinal++,
                Heading = chunk.Heading,
                Content = chunk.Content
            });
        }
    }
}

public sealed class AiChatService(PmsDbContext db, IKnowledgeSearchService search, IAiRuntime ai) : IAIChatService
{
    static readonly ConcurrentDictionary<int, Queue<DateTime>> Hits = new();

    public async Task<AiChatResult> AskAsync(AiChatRequest request, int userId, int? propertyId, bool canSeeFinance, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var gate = Hits.GetOrAdd(userId, _ => new Queue<DateTime>());
        lock (gate)
        {
            while (gate.Count > 0 && gate.Peek() < now.AddMinutes(-1)) gate.Dequeue();
            if (gate.Count >= 20) throw new InvalidOperationException("Bạn hỏi quá nhanh. Đợi một phút rồi thử lại.");
            gate.Enqueue(now);
        }
        AiConversation conversation;
        if (request.ConversationId is Guid existing)
        {
            conversation = await db.AiConversations.FirstOrDefaultAsync(x => x.Id == existing && x.UserId == userId, ct)
                ?? throw new InvalidOperationException("Không thấy hội thoại của bạn");
        }
        else
        {
            conversation = new AiConversation { Id = Guid.NewGuid(), UserId = userId, PropertyId = propertyId, Module = request.Module, CreatedAtUtc = now };
            db.AiConversations.Add(conversation);
        }
        db.AiMessages.Add(new AiMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id, Role = "user", Content = request.Message, CreatedAtUtc = now });
        var docs = await search.SearchAsync(request.Message, propertyId, ct);
        var golden = await db.AiGoldenQuestions.AsNoTracking()
            .Where(x => x.ApprovalStatus == "APPROVED")
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(40)
            .Select(x => new { x.Question, x.ExpectedAnswer, x.Module, x.QuestionType, x.ApprovalStatus })
            .ToListAsync(ct);
        var facts = await SoftwareAnswers.CollectFactsAsync(db, propertyId, canSeeFinance, ct);
        var packet = new
        {
            message = request.Message,
            module = request.Module,
            propertySelected = facts.PropertySelected,
            canSeeFinance,
            today = facts.Today?.ToString("yyyy-MM-dd"),
            counts = facts.Counts,
            rooms = facts.Rooms,
            reports = facts.Reports,
            documents = docs.Select(x => new { title = x.Title, section = x.Section, excerpt = x.Excerpt }),
            golden
        };
        var drafted = await ai.AnswerAsync(packet, ct);
        var citations = new List<AiCitation>();
        string content;
        if (!string.IsNullOrWhiteSpace(drafted))
        {
            content = drafted.Trim();
            citations.Add(new AiCitation(SoftwareAnswers.DataId, "AIservice", "Python", "Số liệu do PMS gửi sang. Dịch vụ Python không đọc database."));
            citations.AddRange(docs);
        }
        else
        {
            var live = await SoftwareAnswers.CollectAsync(db, request.Message, propertyId, request.Module, canSeeFinance, ct);
            citations.AddRange(live);
            citations.AddRange(docs);
            var data = live.FirstOrDefault(x => x.DocumentId == SoftwareAnswers.DataId);
            content = data?.Excerpt ?? (citations.Count == 0 ? AiAssistantPolicy.Abstain : string.Join("\n\n", citations.Select(x => x.Excerpt)));
        }
        citations = citations.Take(6).ToList();
        var escalate = false;
        var answer = new AiMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = content,
            CitationsJson = JsonSerializer.Serialize(citations),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AiMessages.Add(answer);
        await db.SaveChangesAsync(ct);
        return new AiChatResult(conversation.Id, answer.Id, content, citations, escalate, answer.CreatedAtUtc);
    }
}

public sealed class GoldenDatasetService(PmsDbContext db) : IGoldenDatasetService
{
    public async Task<AiGoldenQuestion> SaveAsync(AiGoldenInput input, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = input.Id is Guid id ? await db.AiGoldenQuestions.FirstAsync(x => x.Id == id, ct) : new AiGoldenQuestion { Id = Guid.NewGuid(), CreatedAtUtc = now };
        if (input.Id is null) db.AiGoldenQuestions.Add(row);
        row.DatasetCode = Clean(input.DatasetCode, "default");
        row.Module = Clean(input.Module, "OTHER");
        row.Intent = Clean(input.Intent, "HOW_TO");
        row.Question = input.Question.Trim();
        row.QuestionVariations = input.QuestionVariations ?? "";
        row.QuestionType = Clean(input.QuestionType, "PROCEDURE");
        row.Difficulty = Clean(input.Difficulty, "MEDIUM");
        row.ExpectedAnswer = input.ExpectedAnswer.Trim();
        row.ExpectedKeywords = input.ExpectedKeywords ?? "";
        row.EvaluationCriteria = input.EvaluationCriteria ?? "";
        row.Answerable = Clean(input.Answerable, "ANSWERABLE");
        row.SourceDocumentIds = input.SourceDocumentIds ?? "";
        row.SourceVersion = input.SourceVersion ?? "";
        row.ApprovalStatus = "DRAFT";
        row.ApprovedByUserId = null;
        row.ApprovedAtUtc = null;
        row.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return row;
    }

    static string Clean(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

public sealed class AiEvaluationService(PmsDbContext db, IKnowledgeSearchService search, IAiRuntime ai) : IAIEvaluationService
{
    public async Task<AiEvaluationRun> RunAsync(int? propertyId, CancellationToken ct)
    {
        var run = new AiEvaluationRun
        {
            Id = Guid.NewGuid(),
            ModelName = ai.ModelName,
            PromptVersion = AiAssistantPolicy.PromptVersion,
            RetrievalMode = search.Mode,
            StartedAtUtc = DateTime.UtcNow
        };
        db.AiEvaluationRuns.Add(run);
        var questions = await db.AiGoldenQuestions.AsNoTracking().Where(x => x.ApprovalStatus == "APPROVED").ToListAsync(ct);
        foreach (var question in questions)
        {
            var citations = await search.SearchAsync(question.Question, propertyId, ct);
            var retrieved = string.Join("\n", citations.Select(x => x.Excerpt));
            var keywords = question.ExpectedKeywords.Split([',', '|', ';'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            string actual;
            string note;
            bool passed;
            if (question.Answerable == "UNANSWERABLE")
            {
                actual = citations.Count == 0 ? AiAssistantPolicy.Abstain : "Có đoạn tài liệu, cần người QC xem có trả lời vượt nguồn không.";
                passed = citations.Count == 0;
                note = passed ? "Từ chối đúng vì không có đoạn đã duyệt." : "Có đoạn duyệt nên chưa tính là từ chối đúng.";
            }
            else
            {
                actual = citations.Count == 0 ? AiAssistantPolicy.Abstain : retrieved;
                passed = keywords.Count > 0 && keywords.All(k => retrieved.Contains(k, StringComparison.OrdinalIgnoreCase));
                note = passed ? "Đủ từ khóa trong đoạn tìm được." : "Thiếu từ khóa hoặc chưa có tài liệu duyệt. Đây là chấm truy xuất, không phải AI tự chấm AI.";
            }
            if (passed) run.Passed++; else run.Failed++;
            db.AiEvaluationResults.Add(new AiEvaluationResult
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                QuestionId = question.Id,
                Question = question.Question,
                Retrieved = retrieved,
                ExpectedAnswer = question.ExpectedAnswer,
                ActualAnswer = actual,
                Citations = string.Join(", ", citations.Select(x => x.DocumentId)),
                Passed = passed,
                Note = note,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        run.FinishedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return run;
    }
}
