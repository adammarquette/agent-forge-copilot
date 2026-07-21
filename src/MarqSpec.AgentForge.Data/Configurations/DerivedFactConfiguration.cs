using MarqSpec.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarqSpec.AgentForge.Data.Configurations;

internal sealed class DerivedFactConfiguration : IEntityTypeConfiguration<DerivedFact>
{
    public void Configure(EntityTypeBuilder<DerivedFact> builder)
    {
        builder.ToTable("derived_facts");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.FactType).HasMaxLength(128);
        builder.Property(f => f.PayloadJson).HasColumnType("jsonb");

        // Citation contract (W2_ARCHITECTURE.md §7) stored inline on the fact row as an owned value object.
        builder.OwnsOne(f => f.Citation, citation =>
        {
            citation.Property(c => c.SourceType).HasConversion<string>()
                .HasColumnName("citation_source_type").HasMaxLength(32);
            citation.Property(c => c.SourceId).HasColumnName("citation_source_id").HasMaxLength(256);
            citation.Property(c => c.PageOrSection).HasColumnName("citation_page_or_section").HasMaxLength(128);
            citation.Property(c => c.FieldOrChunkId).HasColumnName("citation_field_or_chunk_id").HasMaxLength(256);
            citation.Property(c => c.QuoteOrValue).HasColumnName("citation_quote_or_value");
            citation.Property(c => c.BoundingBox).HasColumnName("citation_bbox");
        });

        builder.HasIndex(f => f.IngestedDocumentId);
    }
}
