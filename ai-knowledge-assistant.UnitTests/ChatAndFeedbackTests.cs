using ai_knowledge_assistant.Application.DTOs.Chat;
using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Application.Exceptions;
using ai_knowledge_assistant.Application.Features.Chat;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Domain.Enums;
using ai_knowledge_assistant.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace ai_knowledge_assistant.UnitTests;

public sealed class ChatAndFeedbackTests
{
    [Theory]
    [InlineData("summarize the pdf")]
    [InlineData("Summarize this document")]
    [InlineData("give summary")]
    [InlineData("what is this document about")]
    [InlineData("list the important points")]
    [InlineData("explain chapter 1")]
    public void Summarization_queries_are_detected(string question)
    {
        Assert.True(ChatService.IsSummarizationQuestion(question));
    }

    [Fact]
    public async Task Summarization_uses_broad_ordered_document_context()
    {
        var search = new FakeSemanticSearchService([
            new SearchResultResponse(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
                "Document introduction and main topic.",
                1,
                "stored.pdf",
                "summary.pdf",
                DateTime.UtcNow,
                "document-coverage")
        ]);
        var service = new ChatService(
            search,
            new FakeAIProvider(),
            new InMemoryConversationRepository(),
            NullLogger<ChatService>.Instance);

        await service.AskAsync(Guid.NewGuid(), new ChatAskRequest("summarize the pdf"));

        Assert.True(search.DocumentContextRequested);
        Assert.Equal(12, search.RequestedContextChunkCount);
    }

    [Fact]
    public async Task Chat_returns_grounded_fallback_when_no_context_is_relevant()
    {
        var service = new ChatService(
            new FakeSemanticSearchService([]),
            new FakeAIProvider(),
            new InMemoryConversationRepository(),
            NullLogger<ChatService>.Instance);

        var response = await service.AskAsync(Guid.NewGuid(), new ChatAskRequest("unrelated question"));

        Assert.Equal(RagPromptBuilder.NoContextAnswer, response.Answer);
        Assert.Empty(response.Citations);
    }

    [Fact]
    public async Task Local_fallback_chunk_with_point_30_score_is_accepted_and_cited()
    {
        var result = new SearchResultResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "NATIONAL PENSION SYSTEM PRAN Mapping Process",
            0.30,
            "stored.pdf",
            "NPS_Shifting_Process.pdf",
            DateTime.UtcNow,
            "local-fallback");
        var aiProvider = new FakeAIProvider();
        var service = new ChatService(
            new FakeSemanticSearchService([result]),
            aiProvider,
            new InMemoryConversationRepository(),
            NullLogger<ChatService>.Instance);

        var response = await service.AskAsync(Guid.NewGuid(), new ChatAskRequest("What is NPS"));

        Assert.NotEqual(RagPromptBuilder.NoContextAnswer, response.Answer);
        Assert.Equal(1, aiProvider.GenerateCallCount);
        Assert.Contains(result.Content, aiProvider.LastContextChunks);
        Assert.Contains(result.Content, aiProvider.LastPrompt, StringComparison.Ordinal);
        var citation = Assert.Single(response.Citations);
        Assert.Equal("local-fallback", citation.ScoreType);
        Assert.Equal(0.30, citation.Similarity, precision: 2);
    }

    [Fact]
    public async Task Valid_citation_prevents_provider_no_context_fallback()
    {
        var result = CreateNpsSearchResult(0.30);
        var service = new ChatService(
            new FakeSemanticSearchService([result]),
            new FakeAIProvider(RagPromptBuilder.NoContextAnswer),
            new InMemoryConversationRepository(),
            NullLogger<ChatService>.Instance);

        var response = await service.AskAsync(Guid.NewGuid(), new ChatAskRequest("What is NPS"));

        Assert.DoesNotContain(RagPromptBuilder.NoContextAnswer, response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NPS stands for National Pension System", response.Answer, StringComparison.Ordinal);
        Assert.Single(response.Citations);
    }

    [Fact]
    public async Task Streaming_valid_source_suppresses_provider_no_context_tokens()
    {
        var result = CreateNpsSearchResult(0.30);
        var service = new ChatService(
            new FakeSemanticSearchService([result]),
            new FakeAIProvider(RagPromptBuilder.NoContextAnswer),
            new InMemoryConversationRepository(),
            NullLogger<ChatService>.Instance);
        var events = new List<ChatStreamEvent>();

        await foreach (var streamEvent in service.AskStreamAsync(
                           Guid.NewGuid(),
                           new ChatAskRequest("What is NPS")))
        {
            events.Add(streamEvent);
        }

        var streamedAnswer = string.Concat(events.Where(item => item.Type == "token").Select(item => item.Token));
        Assert.DoesNotContain(RagPromptBuilder.NoContextAnswer, streamedAnswer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NPS stands for National Pension System", streamedAnswer, StringComparison.Ordinal);
        var completed = Assert.Single(events, item => item.Type == "complete");
        Assert.NotNull(completed.Response);
        Assert.Single(completed.Response.Citations);
    }

    [Fact]
    public async Task No_context_response_is_used_when_all_chunks_fail_provider_threshold()
    {
        var result = new SearchResultResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "Unrelated local content",
            0.19,
            "stored.pdf",
            "unrelated.pdf",
            DateTime.UtcNow,
            "local-fallback");
        var aiProvider = new FakeAIProvider();
        var service = new ChatService(
            new FakeSemanticSearchService([result]),
            aiProvider,
            new InMemoryConversationRepository(),
            NullLogger<ChatService>.Instance);

        var response = await service.AskAsync(Guid.NewGuid(), new ChatAskRequest("What is NPS"));

        Assert.Equal(RagPromptBuilder.NoContextAnswer, response.Answer);
        Assert.Empty(response.Citations);
        Assert.Equal(0, aiProvider.GenerateCallCount);
    }

    [Fact]
    public void Prompt_requires_grounded_no_context_response()
    {
        var prompt = RagPromptBuilder.Build("What is NPS?", [], false);

        Assert.Contains(RagPromptBuilder.NoContextAnswer, prompt, StringComparison.Ordinal);
        Assert.Contains("Do not use outside knowledge", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Chat_response_maps_citations_from_search_results()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var conversationRepository = new InMemoryConversationRepository();
        var service = new ChatService(
            new FakeSemanticSearchService([
                new SearchResultResponse(
                    documentId,
                    chunkId,
                    0,
                    new string('a', 260),
                    0.91,
                    "stored.pdf",
                    "alpha.pdf",
                    DateTime.UtcNow)
            ]),
            new FakeAIProvider(),
            conversationRepository,
            NullLogger<ChatService>.Instance);

        var response = await service.AskAsync(Guid.NewGuid(), new ChatAskRequest("What is alpha?"));

        var citation = Assert.Single(response.Citations);
        Assert.Equal(documentId, citation.DocumentId);
        Assert.Equal(chunkId, citation.ChunkId);
        Assert.Equal("alpha.pdf", citation.OriginalFileName);
        Assert.True(citation.Snippet.Length <= 223);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Feedback_rejects_rating_outside_supported_range(int rating)
    {
        var service = new ChatFeedbackService(
            new InMemoryChatFeedbackRepository(new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = Guid.NewGuid(),
                Role = ChatMessageRole.Assistant,
                Content = "answer"
            }),
            NullLogger<ChatFeedbackService>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitAsync(Guid.NewGuid(), Guid.NewGuid(), new ChatFeedbackRequest(rating)));
    }

    [Fact]
    public async Task Feedback_accepts_owned_assistant_message()
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            Role = ChatMessageRole.Assistant,
            Content = "answer"
        };
        var repository = new InMemoryChatFeedbackRepository(message);
        var service = new ChatFeedbackService(repository, NullLogger<ChatFeedbackService>.Instance);

        var response = await service.SubmitAsync(Guid.NewGuid(), message.Id, new ChatFeedbackRequest(5, "Useful"));

        Assert.Equal(5, response.Rating);
        Assert.Single(repository.Feedback);
    }

    private static SearchResultResponse CreateNpsSearchResult(double similarity)
    {
        return new SearchResultResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            "NATIONAL PENSION SYSTEM PRAN Mapping Process",
            similarity,
            "stored.pdf",
            "NPS_Shifting_Process.pdf",
            DateTime.UtcNow,
            "local-fallback");
    }
}
