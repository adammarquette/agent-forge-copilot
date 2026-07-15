using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Seeds a small, synthetic cardiology-guideline corpus for demo/eval (W2_ARCHITECTURE.md §5), then embeds any
/// chunk that has no dense vector yet. Idempotent: the document set is added once, and the embedding backfill
/// only touches chunks with a null <c>Embedding</c> — so a corpus seeded before a Cohere key existed still gets
/// its dense vectors on the next run. A no-op backfill when the embedding provider is disabled (dense retrieval
/// then simply returns nothing and the hybrid retriever degrades to sparse-only).
/// </summary>
public sealed class GuidelineCorpusSeeder
{
    private readonly AgentForgeDbContext _context;
    private readonly IEmbeddingProvider _embeddings;

    /// <summary>Creates the seeder over the data context and embedding provider.</summary>
    public GuidelineCorpusSeeder(AgentForgeDbContext context, IEmbeddingProvider embeddings)
    {
        _context = context;
        _embeddings = embeddings;
    }

    /// <summary>Populates the corpus once (if empty), then embeds any not-yet-embedded chunk.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await _context.GuidelineDocuments.AnyAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            var anticoagulation = new GuidelineDocument
            {
                Id = Guid.NewGuid(),
                Title = "Anticoagulation in Atrial Fibrillation",
                Source = "Synthetic Cardiology Guidance 2026 - Anticoagulation",
                Version = "2026.1",
                IngestedAt = now,
                Chunks =
                [
                    Chunk(0, "Warfarin INR target",
                        "For most patients with atrial fibrillation on warfarin, the target INR is 2.0 to 3.0. An INR below 2.0 indicates subtherapeutic anticoagulation and increased stroke risk; an INR above 3.0 indicates elevated bleeding risk."),
                    Chunk(1, "DOAC renal dosing",
                        "Direct oral anticoagulants require dose reduction or avoidance in renal impairment. Assess creatinine clearance before starting a DOAC and periodically thereafter."),
                ],
            };

            var gdmt = new GuidelineDocument
            {
                Id = Guid.NewGuid(),
                Title = "Guideline-Directed Medical Therapy Monitoring",
                Source = "Synthetic Cardiology Guidance 2026 - GDMT",
                Version = "2026.1",
                IngestedAt = now,
                Chunks =
                [
                    Chunk(0, "ACEi/ARB potassium and creatinine",
                        "When starting or up-titrating an ACE inhibitor or ARB, monitor serum potassium and creatinine within one to two weeks. Hold or reduce the dose if potassium exceeds 5.5 mmol/L or creatinine rises substantially."),
                    Chunk(1, "Beta-blocker and non-dihydropyridine calcium channel blocker",
                        "Combining a beta-blocker with a non-dihydropyridine calcium channel blocker such as diltiazem or verapamil can cause excessive bradycardia and heart block; avoid or use with close monitoring."),
                    Chunk(2, "Statin therapy in hyperlipidemia",
                        "High-intensity statin therapy is recommended for patients with established atherosclerotic cardiovascular disease to lower LDL cholesterol."),
                ],
            };

            _context.GuidelineDocuments.AddRange(anticoagulation, gdmt);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await BackfillEmbeddingsAsync(cancellationToken);
    }

    // Embeds every chunk missing a dense vector, as search_document (the corpus side of the input-type
    // asymmetry). No-op when the provider is disabled or returns a mismatched count — the vectors stay null.
    private async Task BackfillEmbeddingsAsync(CancellationToken cancellationToken)
    {
        var unembedded = await _context.Set<GuidelineChunk>()
            .Where(chunk => chunk.Embedding == null)
            .ToListAsync(cancellationToken);
        if (unembedded.Count == 0)
        {
            return;
        }

        var vectors = await _embeddings.EmbedAsync(
            [.. unembedded.Select(chunk => chunk.Content)], EmbeddingInputType.Document, cancellationToken);
        if (vectors.Count != unembedded.Count)
        {
            return;
        }

        for (var i = 0; i < unembedded.Count; i++)
        {
            unembedded[i].Embedding = new Vector(vectors[i]);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static GuidelineChunk Chunk(int ordinal, string section, string content) => new()
    {
        Id = Guid.NewGuid(),
        Section = section,
        Ordinal = ordinal,
        Content = content,
    };
}
