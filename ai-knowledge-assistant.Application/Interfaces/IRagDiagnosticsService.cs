using ai_knowledge_assistant.Application.DTOs.Admin;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IRagDiagnosticsService
{
    Task<IReadOnlyCollection<RagDocumentDiagnosticResponse>> GetDocumentsAsync(
        CancellationToken cancellationToken = default);

    Task<RagDocumentDetailResponse> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<RagTestResponse> TestAsync(
        RagTestRequest request,
        CancellationToken cancellationToken = default);
}
