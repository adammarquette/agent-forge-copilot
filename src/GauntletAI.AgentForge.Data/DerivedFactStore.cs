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
    public async Task AddAsync(IngestedDocument document, CancellationToken cancellationToken = default)
    {
        _db.IngestedDocuments.Add(document);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
