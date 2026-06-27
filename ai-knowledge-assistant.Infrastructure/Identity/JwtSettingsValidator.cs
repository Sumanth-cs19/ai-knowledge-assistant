namespace ai_knowledge_assistant.Infrastructure.Identity;

public static class JwtSettingsValidator
{
    private const int MinimumSigningKeyLength = 32;

    public static void Validate(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException("JWT issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException("JWT audience is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.SigningKey)
            || settings.SigningKey.Length < MinimumSigningKeyLength
            || settings.SigningKey.StartsWith("CONFIGURE_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"JWT signing key must be configured and contain at least {MinimumSigningKeyLength} characters.");
        }
    }
}
