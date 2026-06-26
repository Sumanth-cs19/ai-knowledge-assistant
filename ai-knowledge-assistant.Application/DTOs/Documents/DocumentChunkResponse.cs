namespace ai_knowledge_assistant.Application.DTOs.Documents;

public sealed record DocumentChunkResponse(
    Guid Id,
    Guid DocumentId,
    int ChunkIndex,
    string Content,
    DateTime CreatedAt);
