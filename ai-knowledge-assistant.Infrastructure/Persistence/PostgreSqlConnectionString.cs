using Npgsql;

namespace ai_knowledge_assistant.Infrastructure.Persistence;

public static class PostgreSqlConnectionString
{
    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is required.");
        }

        try
        {
            return IsPostgreSqlUri(connectionString)
                ? FromUri(connectionString)
                : new NpgsqlConnectionStringBuilder(connectionString).ConnectionString;
        }
        catch (Exception exception) when (exception is UriFormatException or ArgumentException or FormatException)
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' must be a valid Npgsql connection string or PostgreSQL URI.");
        }
    }

    private static string FromUri(string connectionString)
    {
        var uri = new Uri(connectionString);
        var userInfoSeparator = uri.UserInfo.IndexOf(':');
        if (userInfoSeparator <= 0)
        {
            throw new FormatException("PostgreSQL URI credentials are incomplete.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(uri.UserInfo[..userInfoSeparator]),
            Password = Uri.UnescapeDataString(uri.UserInfo[(userInfoSeparator + 1)..])
        };

        foreach (var pair in ParseQuery(uri.Query))
        {
            switch (pair.Key)
            {
                case "sslmode":
                    builder["SSL Mode"] = pair.Value;
                    break;
                case "channel_binding":
                    builder["Channel Binding"] = pair.Value;
                    break;
            }
        }

        return builder.ConnectionString;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        return query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parameter => parameter.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]).ToLowerInvariant(),
                parts => Uri.UnescapeDataString(parts[1]),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsPostgreSqlUri(string connectionString)
    {
        return connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            || connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase);
    }
}
