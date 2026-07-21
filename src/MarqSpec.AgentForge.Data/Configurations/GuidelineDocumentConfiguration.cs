using MarqSpec.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarqSpec.AgentForge.Data.Configurations;

internal sealed class GuidelineDocumentConfiguration : IEntityTypeConfiguration<GuidelineDocument>
{
    public void Configure(EntityTypeBuilder<GuidelineDocument> builder)
    {
        builder.ToTable("guideline_documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Title).HasMaxLength(512);
        builder.Property(d => d.Source).HasMaxLength(512);
        builder.Property(d => d.Version).HasMaxLength(64);

        builder.HasMany(d => d.Chunks)
            .WithOne(c => c.Document!)
            .HasForeignKey(c => c.GuidelineDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
