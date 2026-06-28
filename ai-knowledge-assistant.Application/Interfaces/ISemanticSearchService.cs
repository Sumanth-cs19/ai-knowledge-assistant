using ai_knowledge_assistant.Application.DTOs.Search;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface ISemanticSearchService
{
    Task<IReadOnlyCollection<SearchResultResponse>> SearchAsync(
        Guid userId,
        SearchQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SearchResultResponse>> GetDocumentContextAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? documentIds,
        int maxChunks,
        CancellationToken cancellationToken = default);
}
