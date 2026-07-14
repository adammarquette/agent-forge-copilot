using GauntletAI.AgentForge.Data;
using GauntletAI.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Seeds a small, synthetic cardiology-guideline corpus for demo/eval (W2_ARCHITECTURE.md §5). Idempotent:
/// does nothing if the corpus is already populated. Embeddings are left null (FTS baseline); the dense half
/// is populated by a later embedding pass.
/// </summary>
public sealed class GuidelineCorpusSeeder
{
    private readonly AgentForgeDbContext _context;

    /// <summary>Creates the seeder over the given context.</summary>
    public GuidelineCorpusSeeder(AgentForgeDbContext context) => _context = context;

    /// <summary>Populates the corpus once; a no-op if any guideline document already exists.</summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await _context.GuidelineDocuments.AnyAsync(cancellationToken))
        {
            return;
        }

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

    private static GuidelineChunk Chunk(int ordinal, string section, string content) => new()
    {
        Id = Guid.NewGuid(),
        Section = section,
        Ordinal = ordinal,
        Content = content,
    };
}
