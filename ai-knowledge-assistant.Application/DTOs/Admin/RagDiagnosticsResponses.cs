using ai_knowledge_assistant.Application.DTOs.Search;
using ai_knowledge_assistant.Domain.Enums;

namespace ai_knowledge_assistant.Application.DTOs.Admin;

public sealed record RagDocumentDiagnosticResponse(
    Guid DocumentId,
    string OriginalFileName,
    string StoredFileName,
    DateTime UploadedAt,
    DocumentStatus Status,
    int VersionNumber,
    double ExtractionQualityScore,
    string ExtractionQuality,
    int ExtractedTextLength,
    int ChunkCount,
    double AverageChunkSize,
    string EmbeddingProvider,
    int EmbeddingDimension,
    int StoredEmbeddingCount,
    string VectorStatus,
    string? Warning);

public sealed record RagChunkDiagnosticResponse(
    Guid ChunkId,
    int ChunkIndex,
    int CharacterCount,
    string Content,
    int EmbeddingDimension,
    bool EmbeddingStored);

public sealed record RagDocumentDetailResponse(
    RagDocumentDiagnosticResponse Document,
    string ExtractedTextPreview,
    IReadOnlyCollection<RagChunkDiagnosticResponse> Chunks);

public sealed record RagTestRequest(Guid DocumentId, string Question);

public sealed record RagTestResponse(
    Guid DocumentId,
    string Question,
    bool BroadContextMode,
    IReadOnlyCollection<SearchResultResponse> RetrievalResults,
    string FinalPrompt,
    string RawAiResponse);
