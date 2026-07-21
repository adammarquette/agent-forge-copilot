using MarqSpec.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarqSpec.AgentForge.Data.Configurations;

internal sealed class IngestedDocumentConfiguration : IEntityTypeConfiguration<IngestedDocument>
{
    public void Configure(EntityTypeBuilder<IngestedDocument> builder)
    {
        builder.ToTable("ingested_documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.PatientId).HasMaxLength(128);
        builder.Property(d => d.DocumentType).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.OpenEmrDocumentReferenceId).HasMaxLength(256);
        builder.Property(d => d.ContentHash).HasMaxLength(64);

        // Idempotent ingestion: the same source file (by content hash) never creates a second row (W2-D3/§4).
        builder.HasIndex(d => d.ContentHash).IsUnique();
        builder.HasIndex(d => d.PatientId);

        builder.HasMany(d => d.DerivedFacts)
            .WithOne(f => f.Document!)
            .HasForeignKey(f => f.IngestedDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
