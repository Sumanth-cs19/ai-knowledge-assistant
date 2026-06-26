namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record UserResponse(
    Guid Id,
    string Email,
    DateTime CreatedAt,
    RoleResponse Role);
