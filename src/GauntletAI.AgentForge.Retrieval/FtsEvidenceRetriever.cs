using System.Data;
using System.Data.Common;
using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Data;
using Microsoft.EntityFrameworkCore;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>
/// Evidence-retriever backed by Postgres full-text search over the guideline corpus (the sparse half of the
/// hybrid design, W2_ARCHITECTURE.md §5). The RRF fusion with pgvector dense search + rerank is the documented
/// fast-follow; this baseline gives a working, grounded retriever with no embeddings dependency. The query is
/// raw parameterized SQL (W2-D14) — the FTS ranking is not expressible in LINQ.
/// </summary>
public sealed class FtsEvidenceRetriever : IEvidenceRetriever
{
    private const string RetrieveSql = """
        SELECT gd."Source", gc."Section", gc."Id"::text, gc."Content",
               ts_rank(gc.search_vector, websearch_to_tsquery('english', @q))::float8 AS score
        FROM guideline_chunks gc
        JOIN guideline_documents gd ON gd."Id" = gc."GuidelineDocumentId"
        WHERE gc.search_vector @@ websearch_to_tsquery('english', @q)
        ORDER BY score DESC
        LIMIT @k;
        """;

    private readonly AgentForgeDbContext _context;

    /// <summary>Creates the retriever over the given context.</summary>
    public FtsEvidenceRetriever(AgentForgeDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<EvidenceSnippet>> RetrieveAsync(
        string query, int topK, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var mustClose = connection.State != ConnectionState.Open;
        if (mustClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = RetrieveSql;
            // OR the query terms: websearch_to_tsquery ANDs by default, which kills recall for multi-word
            // clinical questions. OR-ing keeps the sparse retriever broad; ts_rank still orders by relevance.
            AddParameter(command, "q", ToOrQuery(query));
            AddParameter(command, "k", topK);

            var results = new List<EvidenceSnippet>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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
                await connection.CloseAsync();
            }
        }
    }

    private static string ToOrQuery(string query)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return terms.Length == 0 ? query : string.Join(" or ", terms);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
