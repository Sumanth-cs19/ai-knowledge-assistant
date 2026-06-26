using ai_knowledge_assistant.Application.Interfaces;
using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using ai_knowledge_assistant.Infrastructure.Persistence;
using ai_knowledge_assistant.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ai_knowledge_assistant.IntegrationTests.TestSupport;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ai-knowledge-assistant-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IAIProvider>();
            services.RemoveAll<IEmbeddingProvider>();
            services.RemoveAll<ISemanticSearchService>();

            services.AddDbContext<TestApplicationDbContext>(options =>
            {
                options
                    .UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
            services.AddScoped<ApplicationDbContext>(provider =>
                provider.GetRequiredService<TestApplicationDbContext>());

            services.AddScoped<IAIProvider, FakeAIProvider>();
            services.AddScoped<IEmbeddingProvider, FakeEmbeddingProvider>();
            services.AddScoped<ISemanticSearchService, FakeSemanticSearchService>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
            SeedRoles(context);
            SeedAdminUser(context);
        });
    }

    private sealed class TestApplicationDbContext : ApplicationDbContext
    {
        public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }
    }

    private static void SeedRoles(ApplicationDbContext context)
    {
        if (context.Roles.Any())
        {
            return;
        }

        context.Roles.AddRange(
            new Role { Id = DefaultRoles.AdminRoleId, Name = DefaultRoles.Admin, Description = "Administrator" },
            new Role { Id = DefaultRoles.UserRoleId, Name = DefaultRoles.User, Description = "Standard user" });
        context.SaveChanges();
    }

    private static void SeedAdminUser(ApplicationDbContext context)
    {
        if (context.Users.Any(user => user.Email == "admin@example.com"))
        {
            return;
        }

        var passwordHasher = new BCryptPasswordHasher();
        context.Users.Add(new User
        {
            Email = "admin@example.com",
            PasswordHash = passwordHasher.Hash("Password123!"),
            RoleId = DefaultRoles.AdminRoleId
        });
        context.SaveChanges();
    }
}
