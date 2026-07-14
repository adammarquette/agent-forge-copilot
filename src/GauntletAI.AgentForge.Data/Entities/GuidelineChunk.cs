using NpgsqlTypes;
using Pgvector;

namespace GauntletAI.AgentForge.Data.Entities;

/// <summary>
/// A retrievable chunk of a <see cref="GuidelineDocument"/>, carrying both a dense embedding (vector search)
/// and a generated full-text vector (keyword search) — the two halves of hybrid RAG (W2_ARCHITECTURE.md §5).
/// </summary>
public sealed class GuidelineChunk
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Foreign key to the owning <see cref="GuidelineDocument"/>.</summary>
    public Guid GuidelineDocumentId { get; set; }

    /// <summary>The owning document; navigation property.</summary>
    public GuidelineDocument? Document { get; set; }

    /// <summary>Section heading the chunk was taken from (used for citation).</summary>
    public required string Section { get; set; }

    /// <summary>Zero-based order of the chunk within its document.</summary>
    public int Ordinal { get; set; }

    /// <summary>The chunk text fed to retrieval and (when selected) to the answer model.</summary>
    public required string Content { get; set; }

    /// <summary>Dense embedding of <see cref="Content"/>; null until embedded. Column type <c>vector(N)</c>.</summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// Generated <c>tsvector</c> over <see cref="Content"/> for sparse/keyword search. Database-generated —
    /// never assigned in code.
    /// </summary>
    public NpgsqlTsVector SearchVector { get; private set; } = null!;
}
