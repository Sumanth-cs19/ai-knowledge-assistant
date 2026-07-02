using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Application.Features.Chat;
using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Domain.Enums;
using ai_knowledge_assistant.Infrastructure;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.Infrastructure.Services;
using ai_knowledge_assistant.Infrastructure.Services.AI;
using ai_knowledge_assistant.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pgvector;

namespace ai_knowledge_assistant.UnitTests;

public sealed class SemanticSearchAndProviderTests
{
    [Theory]
    [InlineData("local-fallback", 0.20)]
    [InlineData("hybrid", 0.35)]
    public void Relevance_threshold_is_provider_aware(string scoreType, double expected)
    {
        Assert.Equal(expected, RagRelevancePolicy.GetMinimumSimilarity(scoreType));
    }

    [Fact]
    public async Task Semantic_search_prefers_keyword_and_vector_matches()
    {
        var highKeywordScore = SemanticSearchService.CalculateCombinedScore(0.80, 1.00);
        var lowKeywordScore = SemanticSearchService.CalculateCombinedScore(0.80, 0.00);

        Assert.True(highKeywordScore > lowKeywordScore);
        Assert.Equal(0.85, highKeywordScore, precision: 2);
    }

    [Fact]
    public void Semantic_search_scores_are_clamped_to_meaningful_range()
    {
        Assert.Equal(1, SemanticSearchService.CalculateCombinedScore(2, 1));
        Assert.Equal(0, SemanticSearchService.CalculateCombinedScore(-1, 0));
    }

    [Fact]
    public void Infrastructure_di_resolves_configured_ai_provider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["AI:Provider"] = "Groq",
                ["AI:Model"] = "fake-chat",
                ["AI:EmbeddingModel"] = "fake-embedding",
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:SigningKey"] = "safe-fake-test-signing-key-with-at-least-32-characters"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructureServices(configuration)
            .BuildServiceProvider();

        var provider = services.GetRequiredService<IAIProvider>();

        Assert.Equal("Groq", provider.Name);
    }

    [Fact]
    public async Task Groq_provider_uses_chat_completion_api_and_keeps_embeddings_local()
    {
        var handler = new StubHttpMessageHandler();
        var settings = Options.Create(new AISettings
        {
            Provider = "Groq",
            ApiKey = "safe-fake-api-key",
            Endpoint = "https://api.groq.test/openai/v1/chat/completions",
            Model = "llama-test",
            EmbeddingModel = "local-hash-embedding"
        });
        var provider = new GroqProvider(
            new HttpClient(handler),
            settings,
            NullLogger<GroqProvider>.Instance);

        var answer = await provider.GenerateAnswerAsync("Use this context.", ["Grounded context"]);
        var embedding = await provider.GenerateEmbeddingAsync("Grounded context");

        Assert.Equal("Grounded answer", answer);
        Assert.Equal(1536, embedding.Length);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"Grounded answer\"}}]}",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        }
    }

}
