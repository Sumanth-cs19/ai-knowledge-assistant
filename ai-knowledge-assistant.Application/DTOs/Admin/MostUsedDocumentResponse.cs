namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record MostUsedDocumentResponse(
    Guid DocumentId,
    string OriginalFileName,
    int CitationCount);
