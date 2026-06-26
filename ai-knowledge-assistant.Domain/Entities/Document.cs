using ai_knowledge_assistant.Domain.Enums;

namespace ai_knowledge_assistant.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public required string FileName { get; set; }

    public required string OriginalFileName { get; set; }

    public required string ContentType { get; set; }

    public required string FilePath { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public string? ErrorMessage { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public int VersionNumber { get; set; } = 1;

    public bool IsDeleted { get; set; }

    public User? User { get; set; }

    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}
