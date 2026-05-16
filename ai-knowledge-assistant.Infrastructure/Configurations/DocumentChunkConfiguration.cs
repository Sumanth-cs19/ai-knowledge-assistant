using ai_knowledge_assistant.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ai_knowledge_assistant.Infrastructure.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("DocumentChunks");

        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.ChunkIndex)
            .IsRequired();

        builder.Property(chunk => chunk.Content)
            .IsRequired();

        builder.Property(chunk => chunk.Embedding)
            .HasColumnType("vector(1536)");

        builder.Property(chunk => chunk.CreatedAt)
            .IsRequired();

        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.ChunkIndex })
            .IsUnique();

        builder.HasIndex(chunk => chunk.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        builder.HasOne(chunk => chunk.Document)
            .WithMany(document => document.Chunks)
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
