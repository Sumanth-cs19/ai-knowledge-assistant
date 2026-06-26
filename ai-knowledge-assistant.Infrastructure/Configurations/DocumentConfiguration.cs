using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ai_knowledge_assistant.Infrastructure.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(document => document.OriginalFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(document => document.ContentType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(document => document.FilePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(document => document.UploadedAt)
            .IsRequired();

        builder.Property(document => document.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(document => document.ErrorMessage)
            .HasMaxLength(2048);

        builder.Property(document => document.VersionNumber)
            .IsRequired();

        builder.Property(document => document.IsDeleted)
            .IsRequired();

        builder.HasOne(document => document.User)
            .WithMany(user => user.Documents)
            .HasForeignKey(document => document.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
