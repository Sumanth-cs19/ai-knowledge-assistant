namespace ai_knowledge_assistant.Infrastructure.Identity;

public sealed class AISettings
{
    public const string SectionName = "AI";

    public string Provider { get; init; } = "Ollama";

    public string ApiKey { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string Model { get; init; } = "local-rag";

    public string EmbeddingModel { get; init; } = "local-hash-embedding";
}
