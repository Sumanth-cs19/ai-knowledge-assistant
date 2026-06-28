namespace ai_knowledge_assistant.Application.DTOs.Search;

public sealed record SearchQueryRequest(
    string Query,
    int TopK = 5,
    IReadOnlyCollection<Guid>? DocumentIds = null);
