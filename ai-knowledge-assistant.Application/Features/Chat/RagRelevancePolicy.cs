using ai_knowledge_assistant.Application.DTOs.Search;

namespace ai_knowledge_assistant.Application.Features.Chat;

public static class RagRelevancePolicy
{
    public const double LocalFallbackMinimumSimilarity = 0.20;
    public const double VectorMinimumSimilarity = 0.35;

    public static bool IsRelevant(SearchResultResponse result)
    {
        if (result.ScoreType.Equals("document-coverage", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return result.Similarity >= GetMinimumSimilarity(result.ScoreType);
    }

    public static double GetMinimumSimilarity(string scoreType)
    {
        return scoreType.Equals("local-fallback", StringComparison.OrdinalIgnoreCase)
            ? LocalFallbackMinimumSimilarity
            : VectorMinimumSimilarity;
    }
}
