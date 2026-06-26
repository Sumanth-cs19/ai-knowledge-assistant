namespace ai_knowledge_assistant.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Description { get; set; }

    public ICollection<User> Users { get; set; } = [];
}
