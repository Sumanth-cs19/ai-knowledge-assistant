namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record DocumentAnalyticsResponse(
    int TotalDocuments,
    int PendingDocuments,
    int ProcessingDocuments,
    int IndexedDocuments,
    int FailedDocuments,
    IReadOnlyCollection<MostUsedDocumentResponse> MostUsedDocumentsInCitations,
    IReadOnlyCollection<DocumentProcessingFailureResponse> RecentProcessingFailures);
