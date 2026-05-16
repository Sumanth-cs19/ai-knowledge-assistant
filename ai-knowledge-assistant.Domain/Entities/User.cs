using ai_knowledge_assistant.Domain.Common;

namespace ai_knowledge_assistant.Domain.Entities;

public sealed class User : BaseEntity
{
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public ICollection<Document> Documents { get; set; } = [];

    public ICollection<ChatMessage> ChatMessages { get; set; } = [];
}
