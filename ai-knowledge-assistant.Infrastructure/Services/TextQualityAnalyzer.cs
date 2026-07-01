using System.Text.RegularExpressions;

namespace ai_knowledge_assistant.Infrastructure.Services;

public sealed record TextQualityResult(double Score, bool IsLowQuality, int CharacterCount, int WordCount);

public static partial class TextQualityAnalyzer
{
    public const string LowQualityMessage =
        "This document contains poor quality extracted text. Answers may be inaccurate. This PDF may be scanned or handwritten and may require OCR.";

    private const double MinimumQualityScore = 0.45;

    public static TextQualityResult Analyze(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextQualityResult(0, true, 0, 0);
        }

        var visibleCharacters = text.Where(character => !char.IsWhiteSpace(character)).ToArray();
        var tokens = TokenRegex().Matches(text).Select(match => match.Value).ToArray();
        var alphanumericRatio = visibleCharacters.Length == 0
            ? 0
            : visibleCharacters.Count(char.IsLetterOrDigit) / (double)visibleCharacters.Length;
        var readableWordRatio = tokens.Length == 0
            ? 0
            : tokens.Count(token => ReadableWordRegex().IsMatch(token)) / (double)tokens.Length;
        var invalidCharacterRatio = visibleCharacters.Length == 0
            ? 1
            : visibleCharacters.Count(character => char.IsControl(character) || character == '\uFFFD')
                / (double)visibleCharacters.Length;
        var lengthScore = Math.Min(1, visibleCharacters.Length / 200d);
        var score = Math.Clamp(
            (alphanumericRatio * 0.45)
            + (readableWordRatio * 0.40)
            + (lengthScore * 0.15)
            - (invalidCharacterRatio * 0.50),
            0,
            1);

        return new TextQualityResult(
            Math.Round(score, 4),
            score < MinimumQualityScore,
            visibleCharacters.Length,
            tokens.Length);
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N}'-]{1,}$")]
    private static partial Regex ReadableWordRegex();
}
