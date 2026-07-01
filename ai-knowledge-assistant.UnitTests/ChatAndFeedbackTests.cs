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
}
