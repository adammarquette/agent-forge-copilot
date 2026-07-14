using GauntletAI.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GauntletAI.AgentForge.Data;

/// <summary>
/// EF Core context for the Week 2 data tier: the clinical-guideline RAG corpus and the sidecar-owned
/// derived-fact store (W2_ARCHITECTURE.md §5, §9). Schema is deployed exclusively through EF Core Migrations;
/// the <c>vector</c> extension is created by the migration via the model configuration below (W2-D14).
/// </summary>
public sealed class AgentForgeDbContext : DbContext
{
    /// <summary>Creates the context with the given options.</summary>
    /// <param name="options">Context options (provider, connection, pgvector support).</param>
    public AgentForgeDbContext(DbContextOptions<AgentForgeDbContext> options)
        : base(options)
    {
    }

    /// <summary>Clinical-guideline corpus documents.</summary>
    public DbSet<GuidelineDocument> GuidelineDocuments => Set<GuidelineDocument>();

    /// <summary>Retrievable guideline chunks (dense + sparse indexed).</summary>
    public DbSet<GuidelineChunk> GuidelineChunks => Set<GuidelineChunk>();

    /// <summary>Ingested patient documents (the sidecar's index of OpenEMR-authoritative source docs).</summary>
    public DbSet<IngestedDocument> IngestedDocuments => Set<IngestedDocument>();

    /// <summary>Facts derived from ingested documents (sidecar-authoritative).</summary>
    public DbSet<DerivedFact> DerivedFacts => Set<DerivedFact>();

    /// <summary>Asynchronous ingestion-job tracking.</summary>
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pgvector extension is created by the migration itself, so a fresh database self-provisions (W2-D14).
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentForgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
