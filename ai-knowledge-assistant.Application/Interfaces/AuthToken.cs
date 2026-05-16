namespace ai_knowledge_assistant.Application.Interfaces;

public sealed record AuthToken(
    string Value,
    DateTime ExpiresAt);
