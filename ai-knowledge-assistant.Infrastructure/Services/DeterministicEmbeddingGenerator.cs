using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ai_knowledge_assistant.Application.Interfaces;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed class DeterministicEmbeddingGenerator : IEmbeddingGenerator
{
    public int Dimensions => 1536;

    public float[] GenerateEmbedding(string text)
    {
        var embedding = new float[Dimensions];
        var tokens = Regex
            .Matches(text.ToLowerInvariant(), @"[a-z0-9]+")
            .Select(match => match.Value);

        foreach (var token in tokens)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var dimension = BitConverter.ToUInt32(hash, 0) % Dimensions;
            var sign = (hash[4] & 1) == 0 ? 1f : -1f;
            embedding[dimension] += sign;
        }

        Normalize(embedding);
        return embedding;
    }

    private static void Normalize(float[] embedding)
    {
        var magnitude = Math.Sqrt(embedding.Sum(value => value * value));
        if (magnitude == 0)
        {
            return;
        }

        for (var index = 0; index < embedding.Length; index++)
        {
            embedding[index] = (float)(embedding[index] / magnitude);
        }
    }
}
