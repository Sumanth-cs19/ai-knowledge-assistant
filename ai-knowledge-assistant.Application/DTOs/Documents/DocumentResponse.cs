using ai_knowledge_assistant.Domain.Enums;

namespace ai_knowledge_assistant.Application.DTOs.Documents;

public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string OriginalFileName,
    string ContentType,
    string FilePath,
    DateTime UploadedAt,
    DocumentStatus Status,
    string? ErrorMessage,
    DateTime? ProcessedAt,
    int VersionNumber,
    bool IsDeleted);
