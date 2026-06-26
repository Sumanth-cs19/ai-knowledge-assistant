using ai_knowledge_assistant.Domain.Enums;

namespace ai_knowledge_assistant.Application.DTOs.Conversations;

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ConversationId,
    ChatMessageRole Role,
    string Content,
    int TokenCount,
    DateTime CreatedAt);
