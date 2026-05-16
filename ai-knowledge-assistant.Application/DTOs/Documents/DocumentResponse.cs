namespace ai_knowledge_assistant.Application.DTOs.Documents;

public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string OriginalFileName,
    string ContentType,
    string FilePath,
    DateTime UploadedAt);
