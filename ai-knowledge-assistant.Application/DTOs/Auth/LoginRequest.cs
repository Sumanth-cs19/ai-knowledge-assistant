namespace ai_knowledge_assistant.Application.DTOs.Auth;

public sealed record LoginRequest(
    string Email,
    string Password);
