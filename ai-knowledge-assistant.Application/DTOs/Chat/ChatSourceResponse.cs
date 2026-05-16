namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatSourceResponse(
    Guid DocumentId,
    Guid ChunkId,
    int ChunkIndex,
    string OriginalFileName,
    double Similarity);
