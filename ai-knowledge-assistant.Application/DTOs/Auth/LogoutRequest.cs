namespace ai_knowledge_assistant.Application.DTOs.Auth;

public sealed record LogoutRequest(
    string RefreshToken);
