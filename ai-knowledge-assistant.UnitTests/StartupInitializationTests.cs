using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace ai_knowledge_assistant.UnitTests;

public sealed class StartupInitializationTests
{
    [Fact]
    public async Task Role_seeding_restores_admin_and_user_roles()
    {
        await using var context = TestDbContextFactory.Create();
        context.Roles.RemoveRange(context.Roles);
        await context.SaveChangesAsync();

        await DatabaseInitializer.SeedDefaultRolesAsync(context, NullLogger.Instance);

        Assert.Contains(context.Roles, role => role.Id == DefaultRoles.AdminRoleId && role.Name == DefaultRoles.Admin);
        Assert.Contains(context.Roles, role => role.Id == DefaultRoles.UserRoleId && role.Name == DefaultRoles.User);
    }

    [Fact]
    public void Jwt_validation_rejects_placeholder_signing_key()
    {
        var settings = new JwtSettings
        {
            Issuer = "issuer",
            Audience = "audience",
            SigningKey = "CONFIGURE_VIA_Jwt__SigningKey_MINIMUM_32_CHARACTERS"
        };

        var exception = Assert.Throws<InvalidOperationException>(() => JwtSettingsValidator.Validate(settings));

        Assert.Contains("signing key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Jwt_validation_accepts_complete_settings()
    {
        var settings = new JwtSettings
        {
            Issuer = "issuer",
            Audience = "audience",
            SigningKey = "safe-test-signing-key-with-at-least-32-characters"
        };

        JwtSettingsValidator.Validate(settings);
    }
}
