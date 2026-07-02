using System.Text.RegularExpressions;
using ai_knowledge_assistant.Application.DTOs.Search;

namespace ai_knowledge_assistant.Application.Features.Chat;

public static partial class RagAnswerGuard
{
    public static bool IsNoContextAnswer(string answer)
    {
        var normalized = Normalize(answer);
        var fallback = Normalize(RagPromptBuilder.NoContextAnswer);

        return normalized.Equals(fallback, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(fallback, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CouldBeNoContextAnswer(string answer)
    {
        var normalized = Normalize(answer);
        var fallback = Normalize(RagPromptBuilder.NoContextAnswer);

        return string.IsNullOrEmpty(normalized)
            || fallback.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(fallback, StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildGroundedExtractiveAnswer(
        string question,
        IReadOnlyCollection<SearchResultResponse> matches)
    {
        var source = matches.FirstOrDefault()
            ?? throw new ArgumentException("At least one accepted source is required.", nameof(matches));
        var acronymMatch = DefinitionQuestionRegex().Match(question.Trim());

        if (acronymMatch.Success)
        {
            var acronym = acronymMatch.Groups[1].Value.ToUpperInvariant();
            var expansion = FindAcronymExpansion(acronym, source.Content);
            if (expansion is not null)
            {
                return $"{acronym} stands for {expansion} in the uploaded document. "
                    + $"[source: {source.OriginalFileName}#{source.ChunkIndex}]";
            }
        }

        var excerpt = CreateExcerpt(source.Content);
        return "The indexed document contains the following relevant information: "
            + $"{excerpt} [source: {source.OriginalFileName}#{source.ChunkIndex}]";
    }

    private static string? FindAcronymExpansion(string acronym, string content)
    {
        var words = WordRegex().Matches(content)
            .Select(match => match.Value)
            .ToArray();

        for (var wordCount = Math.Min(6, acronym.Length + 2); wordCount >= 2; wordCount--)
        {
            for (var index = 0; index <= words.Length - wordCount; index++)
            {
                var candidate = words[index..(index + wordCount)];
                var initials = string.Concat(candidate.Select(word => char.ToUpperInvariant(word[0])));
                if (!initials.Equals(acronym, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return string.Join(' ', candidate.Select(ToDisplayWord));
            }
        }

        return null;
    }

    private static string ToDisplayWord(string word)
    {
        return word.Length == 1
            ? word.ToUpperInvariant()
            : $"{char.ToUpperInvariant(word[0])}{word[1..].ToLowerInvariant()}";
    }

    private static string CreateExcerpt(string content)
    {
        const int maxLength = 320;
        var normalized = string.Join(' ', content.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return normalized.Length <= maxLength
            ? normalized
            : $"{normalized[..maxLength].Trim()}...";
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Trim().Trim('"', '\'', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    [GeneratedRegex(@"^\s*(?:what\s+is|what\s+does)\s+([a-z][a-z0-9]{1,9})(?:\s+stand\s+for)?\s*[?.!]*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DefinitionQuestionRegex();

    [GeneratedRegex(@"[A-Za-z][A-Za-z'-]*")]
    private static partial Regex WordRegex();
}
