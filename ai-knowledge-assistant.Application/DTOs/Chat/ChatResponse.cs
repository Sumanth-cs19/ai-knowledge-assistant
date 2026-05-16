namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatResponse(
    Guid Id,
    string Question,
    string Answer,
    DateTime CreatedAt,
    IReadOnlyCollection<ChatSourceResponse> Sources);
