using ai_knowledge_assistant.Domain.Common;

namespace ai_knowledge_assistant.Domain.Entities;

public sealed class User : BaseEntity
{
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }

    public ICollection<Document> Documents { get; set; } = [];

    public ICollection<Conversation> Conversations { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<ChatFeedback> ChatFeedback { get; set; } = [];
}
