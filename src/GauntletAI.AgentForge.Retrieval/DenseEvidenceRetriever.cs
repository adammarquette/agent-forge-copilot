using System.Data;
using System.Data.Common;
using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Data;
using Microsoft.EntityFrameworkCore;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// The dense half of hybrid retrieval (W2_ARCHITECTURE.md §5): embeds the query (input_type = query) via
/// <see cref="IEmbeddingProvider"/>, then does a pgvector cosine KNN over the guideline-chunk embeddings. If
/// no embedding provider is configured (no Cohere key) it returns empty, so the hybrid retriever degrades to
/// sparse-only. The query vector is passed as text and cast <c>::vector</c> in SQL, so this doesn't depend on
/// an ADO-level pgvector type handler on the raw connection.
/// </summary>
public sealed class DenseEvidenceRetriever : IDenseRetriever
{
    private const string KnnSql = """
        SELECT gd."Source", gc."Section", gc."Id"::text, gc."Content",
               (1 - (gc."Embedding" <=> CAST(@q AS vector)))::float8 AS score
        FROM guideline_chunks gc
        JOIN guideline_documents gd ON gd."Id" = gc."GuidelineDocumentId"
        WHERE gc."Embedding" IS NOT NULL
        ORDER BY gc."Embedding" <=> CAST(@q AS vector)
        LIMIT @k;
        """;

    private readonly IEmbeddingProvider _embeddings;
    private readonly AgentForgeDbContext _context;

    /// <summary>Creates the dense retriever over the embedding provider and the data context.</summary>
    public DenseEvidenceRetriever(IEmbeddingProvider embeddings, AgentForgeDbContext context)
    {
        _embeddings = embeddings;
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EvidenceSnippet>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken)
    {
        var embedded = await _embeddings.EmbedAsync([query], EmbeddingInputType.Query, cancellationToken).ConfigureAwait(false);
        if (embedded.Count == 0 || embedded[0].Length == 0)
        {
            // No embeddings provider (or it degraded) — the hybrid retriever falls back to sparse-only.
            return [];
        }

        var queryVector = new Pgvector.Vector(embedded[0]).ToString();

        var connection = _context.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = KnnSql;
            AddParameter(command, "q", queryVector);
            AddParameter(command, "k", topK);

            var results = new List<EvidenceSnippet>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new EvidenceSnippet
                {
                    DocumentId = reader.GetString(0),
                    Section = reader.GetString(1),
                    ChunkId = reader.GetString(2),
                    Text = reader.GetString(3),
                    Score = reader.GetDouble(4),
                });
            }

            return results;
        }
        finally
        {
            if (mustClose)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
