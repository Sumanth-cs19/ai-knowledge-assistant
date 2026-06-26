using ai_knowledge_assistant.Application.DTOs.Chat;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IChatService
{
    Task<ChatResponse> AskAsync(Guid userId, ChatAskRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamEvent> AskStreamAsync(
        Guid userId,
        ChatAskRequest request,
        CancellationToken cancellationToken = default);
}
