using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ai_knowledge_assistant.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            logger.LogInformation("Applying database migrations during application startup");
            await context.Database.MigrateAsync(cancellationToken);
            await SeedDefaultRolesAsync(context, logger, cancellationToken);
            logger.LogInformation("Database migration and reference data initialization completed");
        }
        catch (Exception exception)
        {
            logger.LogCritical(
                "Database startup initialization failed. FailureType={FailureType}. Verify the production connection, pgvector extension, and migration state",
                exception.GetType().Name);
            throw;
        }
    }

    public static async Task SeedDefaultRolesAsync(
        ApplicationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var requiredRoles = new[]
        {
            new Role
            {
                Id = DefaultRoles.AdminRoleId,
                Name = DefaultRoles.Admin,
                Description = "Administrator with full platform access."
            },
            new Role
            {
                Id = DefaultRoles.UserRoleId,
                Name = DefaultRoles.User,
                Description = "Standard authenticated user."
            }
        };

        var addedRoleNames = new List<string>();
        foreach (var requiredRole in requiredRoles)
        {
            var existingById = await context.Roles
                .SingleOrDefaultAsync(role => role.Id == requiredRole.Id, cancellationToken);

            if (existingById is not null)
            {
                continue;
            }

            var conflictingRole = await context.Roles
                .AsNoTracking()
                .SingleOrDefaultAsync(role => role.Name == requiredRole.Name, cancellationToken);

            if (conflictingRole is not null)
            {
                throw new InvalidOperationException(
                    $"Default role '{requiredRole.Name}' exists with an unexpected identifier.");
            }

            context.Roles.Add(requiredRole);
            addedRoleNames.Add(requiredRole.Name);
        }

        if (addedRoleNames.Count == 0)
        {
            logger.LogInformation("Default roles are present");
            return;
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded missing default roles {RoleNames}", addedRoleNames);
    }
}
