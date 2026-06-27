using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Infrastructure.Identity;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

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

    [Fact]
    public void PostgreSql_uri_is_normalized_for_npgsql()
    {
        const string uri = "postgresql://neon_owner:p%40ssword@ep-example-pooler.neon.tech/neondb?sslmode=require&channel_binding=require";

        var normalized = PostgreSqlConnectionString.Normalize(uri);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("ep-example-pooler.neon.tech", builder.Host);
        Assert.Equal("neondb", builder.Database);
        Assert.Equal("neon_owner", builder.Username);
        Assert.Equal("p@ssword", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
    }

    [Fact]
    public void Invalid_connection_string_error_does_not_echo_secret_value()
    {
        const string invalid = "postgresql://super-secret@host/database";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgreSqlConnectionString.Normalize(invalid));

        Assert.DoesNotContain("super-secret", exception.Message, StringComparison.Ordinal);
    }
}
