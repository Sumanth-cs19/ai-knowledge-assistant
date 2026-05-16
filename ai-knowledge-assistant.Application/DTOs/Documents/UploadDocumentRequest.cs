namespace ai_knowledge_assistant.Application.DTOs.Documents;

public sealed record UploadDocumentRequest(
    Guid UserId,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    Stream Content);
