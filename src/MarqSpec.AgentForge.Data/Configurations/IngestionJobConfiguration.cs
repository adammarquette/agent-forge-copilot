using MarqSpec.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarqSpec.AgentForge.Data.Configurations;

internal sealed class IngestionJobConfiguration : IEntityTypeConfiguration<IngestionJob>
{
    public void Configure(EntityTypeBuilder<IngestionJob> builder)
    {
        builder.ToTable("ingestion_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.PatientId).HasMaxLength(128);
        builder.Property(j => j.DocumentType).HasConversion<string>().HasMaxLength(32);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(j => j.CorrelationId).HasMaxLength(128);

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.CorrelationId);
    }
}
