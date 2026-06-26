namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record ChatAnalyticsResponse(
    int TotalConversations,
    int ArchivedConversations,
    int TotalChatMessages,
    int UserMessages,
    int AssistantMessages,
    int ConversationsLast7Days);
