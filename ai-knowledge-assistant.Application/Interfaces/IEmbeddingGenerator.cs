namespace ai_knowledge_assistant.Application.Interfaces;

public interface IEmbeddingGenerator
{
    int Dimensions { get; }

    float[] GenerateEmbedding(string text);
}
