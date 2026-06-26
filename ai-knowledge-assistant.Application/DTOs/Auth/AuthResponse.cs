namespace ai_knowledge_assistant.Application.DTOs.Auth;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string Email,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
