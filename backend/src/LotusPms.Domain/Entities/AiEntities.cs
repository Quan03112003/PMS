using System.ComponentModel.DataAnnotations;

namespace HotelPms.Api;

public sealed class AiKnowledgeDocument
{
    public Guid Id { get; set; }
    public int? PropertyId { get; set; }
    [MaxLength(40)] public string Code { get; set; } = "";
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(200)] public string FileName { get; set; } = "";
    [MaxLength(40)] public string VersionLabel { get; set; } = "1";
    [MaxLength(40)] public string Module { get; set; } = "OTHER";
    [MaxLength(20)] public string Status { get; set; } = "DRAFT";
    public string RawText { get; set; } = "";
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
}

public sealed class AiKnowledgeChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int Ordinal { get; set; }
    [MaxLength(200)] public string Heading { get; set; } = "";
    public string Content { get; set; } = "";
}

public sealed class AiConversation
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public int? PropertyId { get; set; }
    [MaxLength(40)] public string Module { get; set; } = "OTHER";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AiMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    [MaxLength(20)] public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public string CitationsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AiAnswerFeedback
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public int UserId { get; set; }
    public bool Helpful { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AiGoldenQuestion
{
    public Guid Id { get; set; }
    [MaxLength(40)] public string DatasetCode { get; set; } = "default";
    [MaxLength(40)] public string Module { get; set; } = "OTHER";
    [MaxLength(40)] public string Intent { get; set; } = "HOW_TO";
    public string Question { get; set; } = "";
    public string QuestionVariations { get; set; } = "";
    [MaxLength(40)] public string QuestionType { get; set; } = "PROCEDURE";
    [MaxLength(20)] public string Difficulty { get; set; } = "MEDIUM";
    public string ExpectedAnswer { get; set; } = "";
    public string ExpectedKeywords { get; set; } = "";
    public string EvaluationCriteria { get; set; } = "";
    [MaxLength(40)] public string Answerable { get; set; } = "ANSWERABLE";
    public string SourceDocumentIds { get; set; } = "";
    [MaxLength(40)] public string SourceVersion { get; set; } = "";
    [MaxLength(20)] public string ApprovalStatus { get; set; } = "DRAFT";
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AiEvaluationRun
{
    public Guid Id { get; set; }
    [MaxLength(80)] public string ModelName { get; set; } = "";
    [MaxLength(40)] public string PromptVersion { get; set; } = "v1";
    [MaxLength(20)] public string RetrievalMode { get; set; } = "lexical";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
}

public sealed class AiEvaluationResult
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid QuestionId { get; set; }
    public string Question { get; set; } = "";
    public string Retrieved { get; set; } = "";
    public string ExpectedAnswer { get; set; } = "";
    public string ActualAnswer { get; set; } = "";
    public string Citations { get; set; } = "";
    public bool Passed { get; set; }
    public string Note { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AiIngestionJob
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "DONE";
    public string Message { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
