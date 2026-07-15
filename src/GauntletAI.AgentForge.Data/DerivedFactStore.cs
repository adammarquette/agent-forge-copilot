using GauntletAI.AgentForge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GauntletAI.AgentForge.Data;

/// <inheritdoc />
public sealed class DerivedFactStore : IDerivedFactStore
{
    private readonly AgentForgeDbContext _db;

    /// <summary>Creates the store over the given context.</summary>
    public DerivedFactStore(AgentForgeDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<IngestedDocument?> FindByContentHashAsync(
        string contentHash, CancellationToken cancellationToken = default) =>
        _db.IngestedDocuments
            .Include(d => d.DerivedFacts)
            .FirstOrDefaultAsync(d => d.ContentHash == contentHash, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<DerivedFact>> GetByPatientAsync(
        string patientId, CancellationToken cancellationToken = default) =>
        await _db.DerivedFacts
            .Include(f => f.Document)
            .Where(f => f.Document!.PatientId == patientId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddAsync(IngestedDocument document, CancellationToken cancellationToken = default)
    {
        _db.IngestedDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
