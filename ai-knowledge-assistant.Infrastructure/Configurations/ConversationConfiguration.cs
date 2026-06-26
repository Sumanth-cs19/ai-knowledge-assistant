using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ai_knowledge_assistant.Infrastructure.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(conversation => conversation.CreatedAt)
            .IsRequired();

        builder.Property(conversation => conversation.UpdatedAt)
            .IsRequired();

        builder.Property(conversation => conversation.IsArchived)
            .IsRequired();

        builder.Property(conversation => conversation.IsDeleted)
            .IsRequired();

        builder.HasOne(conversation => conversation.User)
            .WithMany(user => user.Conversations)
            .HasForeignKey(conversation => conversation.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
