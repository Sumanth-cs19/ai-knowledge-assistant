namespace ai_knowledge_assistant.Application.DTOs.Search;

public sealed record SearchResultResponse(
    Guid DocumentId,
    Guid ChunkId,
    int ChunkIndex,
    string Content,
    double Similarity,
    string FileName,
    string OriginalFileName,
    DateTime UploadedAt);
