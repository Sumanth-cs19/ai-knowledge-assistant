namespace ai_knowledge_assistant.Application.Interfaces;

public interface IEmbeddingProvider
{
    string Name { get; }

    int Dimensions { get; }

    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
