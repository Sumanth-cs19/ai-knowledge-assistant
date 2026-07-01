using System.Text;
using ai_knowledge_assistant.Application.DTOs.Search;

namespace ai_knowledge_assistant.Application.Features.Chat;

public static class RagPromptBuilder
{
    public const string NoContextAnswer = "No relevant information was found in the indexed documents.";

    public static bool IsBroadContextQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var normalized = string.Join(' ', question.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return normalized.Contains("summarize", StringComparison.Ordinal)
            || normalized.Contains("summary", StringComparison.Ordinal)
            || normalized.Contains("important points", StringComparison.Ordinal)
            || normalized.Contains("key points", StringComparison.Ordinal)
            || normalized.Contains("what is this document about", StringComparison.Ordinal)
            || normalized.Contains("what is the document about", StringComparison.Ordinal)
            || normalized.Contains("what is this pdf about", StringComparison.Ordinal)
            || normalized.Contains("give me an overview", StringComparison.Ordinal)
            || normalized.Contains("explain chapter", StringComparison.Ordinal)
            || normalized.Equals("give overview", StringComparison.Ordinal);
    }

    public static string Build(
        string question,
        IReadOnlyCollection<SearchResultResponse> matches,
        bool broadContextMode)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an AI knowledge assistant. Answer using only the indexed document context below.");
        prompt.AppendLine($"If the context is missing, unclear, or insufficient, respond exactly: \"{NoContextAnswer}\"");
        prompt.AppendLine("Do not use outside knowledge or invent details. Cite claims using [source: original-file-name#chunk-index].");
        if (broadContextMode)
        {
            prompt.AppendLine("Broad document mode is active. Synthesize the ordered chunks into a coherent answer covering the main topic, important points, and conclusions.");
        }

        prompt.AppendLine();
        prompt.AppendLine("Question:");
        prompt.AppendLine(question.Trim());
        prompt.AppendLine();
        prompt.AppendLine("Indexed document context:");

        foreach (var match in matches)
        {
            prompt.AppendLine($"[source: {match.OriginalFileName}#{match.ChunkIndex}]");
            prompt.AppendLine(match.Content);
            prompt.AppendLine();
        }

        return prompt.ToString();
    }
}
