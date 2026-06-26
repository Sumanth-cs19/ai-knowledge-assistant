using ai_knowledge_assistant.Domain.Common;
using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ai_knowledge_assistant.Infrastructure.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(role => role.Name)
            .IsUnique();

        builder.Property(role => role.Description)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasData(
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
            });
    }
}
