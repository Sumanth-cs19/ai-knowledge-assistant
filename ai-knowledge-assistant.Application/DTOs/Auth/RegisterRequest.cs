namespace ai_knowledge_assistant.Application.DTOs.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password);
