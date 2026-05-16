using ai_knowledge_assistant.Domain.Entities;

namespace ai_knowledge_assistant.Application.Interfaces;

public interface IJwtTokenService
{
    AuthToken CreateToken(User user);
}
