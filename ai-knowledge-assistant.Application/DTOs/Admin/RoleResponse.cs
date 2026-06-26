namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string Description);
