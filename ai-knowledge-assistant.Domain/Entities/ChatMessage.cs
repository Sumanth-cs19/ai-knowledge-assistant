using ai_knowledge_assistant.Domain.Enums;

namespace ai_knowledge_assistant.Domain.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }

    public ChatMessageRole Role { get; set; }

    public required string Content { get; set; }

    public int TokenCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
}
