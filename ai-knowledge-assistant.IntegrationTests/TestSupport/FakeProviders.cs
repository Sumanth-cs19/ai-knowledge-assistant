using System.Runtime.CompilerServices;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.IntegrationTests.TestSupport;

internal sealed class FakeAIProvider : IAIProvider
{
    public string Name => "Fake";

    public Task<string> GenerateAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Fake answer grounded in test context [source: test.pdf#0]");
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        string prompt,
        IReadOnlyCollection<string> contextChunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "Fake ";
        await Task.CompletedTask;
        yield return "answer";
    }
}

internal sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "Fake";

    public int Dimensions => 1536;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embedding = new float[Dimensions];
        embedding[0] = 1;
        return Task.FromResult(embedding);
    }
}

internal sealed class FakeSemanticSearchService : ISemanticSearchService
{
    public Task<IReadOnlyCollection<SearchResultResponse>> SearchAsync(
        Guid userId,
        SearchQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<SearchResultResponse> response =
        [
            new SearchResultResponse(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                0,
                "The uploaded test document says integration tests should use fake AI providers.",
                0.99,
                "stored.pdf",
                "test.pdf",
                DateTime.UtcNow)
        ];

        return Task.FromResult(response);
    }
}
