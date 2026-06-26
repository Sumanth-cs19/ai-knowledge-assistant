namespace ai_knowledge_assistant.Domain.Entities;

public sealed class ChatFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChatMessageId { get; set; }

    public Guid UserId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChatMessage? ChatMessage { get; set; }

    public User? User { get; set; }
}
