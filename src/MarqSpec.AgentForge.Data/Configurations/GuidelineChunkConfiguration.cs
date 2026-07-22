using MarqSpec.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarqSpec.AgentForge.Data.Configurations;

internal sealed class GuidelineChunkConfiguration : IEntityTypeConfiguration<GuidelineChunk>
{
    public void Configure(EntityTypeBuilder<GuidelineChunk> builder)
    {
        builder.ToTable("guideline_chunks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Section).HasMaxLength(512);

        // Dense half: pgvector column + HNSW index (cosine). Build-time index params live in the model (W2-D14);
        // per-query recall knobs (hnsw.ef_search) are a runtime SET, not configured here.
        builder.Property(c => c.Embedding).HasColumnType(EmbeddingModel.ColumnType);
        builder.HasIndex(c => c.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);

        // Sparse half: database-generated tsvector over Content + GIN index. The RRF fusion of these two
        // halves is a raw FromSqlRaw query, not expressible in LINQ (W2_ARCHITECTURE.md §5).
        builder.HasGeneratedTsVectorColumn(c => c.SearchVector, "english", c => new { c.Content });
        builder.Property(c => c.SearchVector).HasColumnName("search_vector");
        builder.HasIndex(c => c.SearchVector).HasMethod("GIN");

        builder.HasIndex(c => new { c.GuidelineDocumentId, c.Ordinal });
    }
}
