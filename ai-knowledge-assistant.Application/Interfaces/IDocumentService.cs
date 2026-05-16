using ai_knowledge_assistant.Application.DTOs.Documents;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IDocumentService
{
    Task<DocumentResponse> UploadAsync(UploadDocumentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentResponse>> GetUserDocumentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
