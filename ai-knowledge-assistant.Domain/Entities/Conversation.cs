namespace ai_knowledge_assistant.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public required string Title { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }

    public bool IsDeleted { get; set; }

    public User? User { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = [];
}
