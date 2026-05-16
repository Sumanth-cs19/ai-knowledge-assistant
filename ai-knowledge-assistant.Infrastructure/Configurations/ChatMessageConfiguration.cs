using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ai_knowledge_assistant.Infrastructure.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Question)
            .IsRequired();

        builder.Property(message => message.Answer)
            .IsRequired();

        builder.Property(message => message.SourceReferencesJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(message => message.CreatedAt)
            .IsRequired();

        builder.HasOne(message => message.User)
            .WithMany(user => user.ChatMessages)
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
