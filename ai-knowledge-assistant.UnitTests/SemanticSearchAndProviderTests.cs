using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Domain.Enums;
using ai_knowledge_assistant.Infrastructure;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.Infrastructure.Services;
using ai_knowledge_assistant.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;

namespace ai_knowledge_assistant.UnitTests;

public sealed class SemanticSearchAndProviderTests
{
    [Fact]
    public async Task Semantic_search_prefers_keyword_and_vector_matches()
    {
        var highKeywordScore = SemanticSearchService.CalculateCombinedScore(0.80, 1.00);
        var lowKeywordScore = SemanticSearchService.CalculateCombinedScore(0.80, 0.00);

        Assert.True(highKeywordScore > lowKeywordScore);
        Assert.Equal(0.85, highKeywordScore, precision: 2);
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

}
