namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record DocumentProcessingFailureResponse(
    Guid DocumentId,
    string OriginalFileName,
    string? ErrorMessage,
    DateTime UploadedAt,
    DateTime? ProcessedAt);
