namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatStreamEvent(
    string Type,
    string? Token = null,
    ChatResponse? Response = null);
