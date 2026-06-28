namespace ai_knowledge_assistant.Application.DTOs.Chat;

public sealed record ChatSourceResponse(
    Guid DocumentId,
    Guid ChunkId,
    int ChunkIndex,
    string DocumentName,
    string OriginalFileName,
    double Similarity,
    string Snippet,
    string ScoreType = "hybrid");
