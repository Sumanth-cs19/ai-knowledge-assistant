using ai_knowledge_assistant.Application.DTOs.Admin;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IRoleService
{
    Task<IReadOnlyCollection<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default);
}
