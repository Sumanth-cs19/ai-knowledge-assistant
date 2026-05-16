namespace ai_knowledge_assistant.Application.DTOs.Auth;

public sealed record AuthResponse(
    string Token,
    string Email,
    DateTime ExpiresAt);
