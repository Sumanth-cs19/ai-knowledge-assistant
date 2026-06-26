using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ai_knowledge_assistant.Infrastructure.Configurations;

public sealed class ChatFeedbackConfiguration : IEntityTypeConfiguration<ChatFeedback>
{
    public void Configure(EntityTypeBuilder<ChatFeedback> builder)
    {
        builder.ToTable("ChatFeedback");

        builder.HasKey(feedback => feedback.Id);

        builder.Property(feedback => feedback.Rating)
            .IsRequired();

        builder.Property(feedback => feedback.Comment)
            .HasMaxLength(1000);

        builder.Property(feedback => feedback.CreatedAt)
            .IsRequired();

        builder.HasOne(feedback => feedback.ChatMessage)
            .WithMany()
            .HasForeignKey(feedback => feedback.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(feedback => feedback.User)
            .WithMany(user => user.ChatFeedback)
            .HasForeignKey(feedback => feedback.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
