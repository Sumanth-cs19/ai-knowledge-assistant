using ai_knowledge_assistant.Application.DTOs.Documents;
using ai_knowledge_assistant.Application.DTOs.Common;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IDocumentService
{
    Task<DocumentResponse> UploadAsync(UploadDocumentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentResponse>> GetUserDocumentsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<DocumentResponse>> GetUserDocumentsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DocumentResponse> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task ReindexAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentResponse>> GetVersionsAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<DocumentChunkResponse>> GetChunksAsync(
        Guid userId,
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
