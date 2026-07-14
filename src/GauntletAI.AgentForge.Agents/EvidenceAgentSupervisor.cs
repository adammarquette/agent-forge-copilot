using System.Text.Json;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.Logging;

namespace GauntletAI.AgentForge.Agents;

/// <summary>
/// The Week 2 supervisor: a small, typed, inspectable graph (W2_ARCHITECTURE.md §6). It routes explicitly —
/// intake-extractor (when a document is attached) → evidence-retriever → answer-composer → critic — logging
/// every handoff. The critic is the Week 1 <see cref="IClinicalResponseVerifier"/>, reused as a node: it
/// suppresses uncited claims and surfaces domain-constraint flags. The supervisor never fabricates; on a
/// worker failure it degrades and continues rather than crashing (NFR-REL-1).
/// </summary>
public sealed class EvidenceAgentSupervisor : IEvidenceAgentSupervisor
{
    private const int DefaultTopK = 5;
    private const string SupervisorNode = "supervisor";

    private readonly IDocumentExtractor _extractor;
    private readonly IEvidenceRetriever _retriever;
    private readonly ILlmProvider _llm;
    private readonly IClinicalResponseVerifier _verifier;
    private readonly ILogger<EvidenceAgentSupervisor> _logger;

    /// <summary>Creates the supervisor.</summary>
    public EvidenceAgentSupervisor(
        IDocumentExtractor extractor,
        IEvidenceRetriever retriever,
        ILlmProvider llm,
        IClinicalResponseVerifier verifier,
        ILogger<EvidenceAgentSupervisor> logger)
    {
        _extractor = extractor;
        _retriever = retriever;
        _llm = llm;
        _verifier = verifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EvidenceAgentResult> RunAsync(EvidenceAgentRequest request, CancellationToken cancellationToken)
    {
        var handoffs = new List<HandoffEvent>();
        string? factsJson = null;

        // 1. intake-extractor - only when a document is attached this turn.
        if (request.Document is { } document)
        {
            Route(handoffs, SupervisorNode, "intake-extractor", "document attached; extraction needed");
            var extraction = await _extractor.ExtractAsync(
                document.DocumentType, document.Content, document.MediaType, cancellationToken);

            if (extraction.Succeeded)
            {
                factsJson = extraction.CanonicalJson;
            }
            else
            {
                EvidenceAgentSupervisorLog.ExtractionDegraded(_logger);
                Route(handoffs, "intake-extractor", SupervisorNode, "extraction rejected; continuing without document facts");
            }
        }

        // Project the extracted patient labs into citable facts once - reused by the composer (to cite
        // them) and the critic (to resolve those citations). Empty for non-lab extractions.
        var labFacts = ExtractLabFacts(factsJson);

        // 2. evidence-retriever.
        Route(handoffs, SupervisorNode, "evidence-retriever", "question needs guideline evidence");
        var evidence = await _retriever.RetrieveAsync(request.Question, DefaultTopK, cancellationToken);

        // 3. answer-composer.
        Route(handoffs, SupervisorNode, "answer-composer", "facts + evidence assembled");
        var draft = await ComposeAsync(request.Question, factsJson, labFacts, evidence, cancellationToken);

        // 4. critic = the Week 1 verification gate, reused as a node.
        Route(handoffs, "answer-composer", "critic", "draft ready for verification");
        var verification = _verifier.Verify(draft, BuildToolResults(factsJson, labFacts, evidence));
        Route(handoffs, "critic", SupervisorNode,
            verification.Passed ? "all claims grounded" : $"{verification.SuppressedClaims.Count} uncited claim(s) suppressed");

        return new EvidenceAgentResult
        {
            Answer = verification.VerifiedAnswer,
            SafetyFlags = verification.ConstraintFlags,
            SuppressedClaims = verification.SuppressedClaims,
            Handoffs = handoffs,
            ExtractedFactsJson = factsJson,
            Evidence = evidence,
        };
    }

    private void Route(List<HandoffEvent> handoffs, string from, string to, string reason)
    {
        handoffs.Add(new HandoffEvent(from, to, reason));
        EvidenceAgentSupervisorLog.Handoff(_logger, from, to, reason);
    }

    private async Task<string> ComposeAsync(
        string question, string? factsJson, IReadOnlyList<LabFact> labFacts,
        IReadOnlyList<EvidenceSnippet> evidence, CancellationToken cancellationToken)
    {
        var facts = factsJson ?? "(no document facts on file)";
        var labText = labFacts.Count == 0
            ? "(no patient lab values extracted)"
            : string.Join("\n", labFacts.Select(l =>
                $"[Lab/{l.Slug}] {l.TestName}: {l.Value}{(l.Unit is null ? "" : $" {l.Unit}")}"
                + $"{(l.ReferenceRange is null ? "" : $" (ref {l.ReferenceRange})")}{(l.Abnormal == true ? " [ABNORMAL]" : "")}"));
        var evidenceText = evidence.Count == 0
            ? "(no guideline evidence found)"
            : string.Join("\n", evidence.Select(e => $"[Guideline/{e.ChunkId}] {e.DocumentId} - {e.Section}: {e.Text}"));

        var userContent =
            $"Question: {question}\n\n"
            + $"Patient lab values (cite each with the token shown):\n{labText}\n\n"
            + $"Full extracted document facts (JSON):\n{facts}\n\n"
            + $"Guideline evidence:\n{evidenceText}";

        var response = await _llm.CompleteAsync(
            new LlmRequest(EvidenceComposerPrompt.System, [LlmMessage.FromText(LlmRole.User, userContent)]),
            cancellationToken);

        return response.Content;
    }

    // Hand the extracted facts and retrieved evidence to the critic as the tool results the answer must be
    // grounded in - the same shape the Week 1 orchestrator passes to the verifier.
    private static List<string> BuildToolResults(
        string? factsJson, IReadOnlyList<LabFact> labFacts, IReadOnlyList<EvidenceSnippet> evidence)
    {
        var results = new List<string>();
        if (factsJson is not null)
        {
            results.Add(factsJson);
        }

        if (labFacts.Count > 0)
        {
            // Project extracted labs to the scanner's citation shape (ResourceType "Lab" + Id), so a
            // [Lab/<slug>] citation for a patient-specific value resolves instead of being suppressed.
            var labRecords = labFacts
                .Select(l => new LabToolResult("Lab", l.Slug, l.TestName, l.Value))
                .ToArray();
            results.Add(JsonSerializer.Serialize(labRecords, AgentsJsonContext.Default.LabToolResultArray));
        }

        if (evidence.Count > 0)
        {
            // Project to the citation shape the Week 1 attribution scanner recognizes (ResourceType + Id),
            // so [Guideline/<chunkId>] citations in the answer resolve instead of being suppressed.
            var records = evidence
                .Select(e => new EvidenceToolResult("Guideline", e.ChunkId, e.Section, e.Text))
                .ToArray();
            results.Add(JsonSerializer.Serialize(records, AgentsJsonContext.Default.EvidenceToolResultArray));
        }

        return results;
    }

    // Parses the extracted lab facts (the LabPdf schema's "tests" array) into citable facts. Returns empty
    // for non-lab extractions (e.g. intake forms carry no "tests" array), which keep the prior behavior.
    private static List<LabFact> ExtractLabFacts(string? factsJson)
    {
        if (factsJson is null)
        {
            return [];
        }

        var facts = new List<LabFact>();
        try
        {
            using var document = JsonDocument.Parse(factsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("tests", out var tests)
                || tests.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            foreach (var test in tests.EnumerateArray())
            {
                if (test.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = ReadString(test, "test_name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var slug = Slugify(name);
                if (slug.Length == 0)
                {
                    continue;
                }

                facts.Add(new LabFact(
                    slug, name, ReadString(test, "value") ?? "?",
                    ReadString(test, "unit"), ReadString(test, "reference_range"), ReadBool(test, "abnormal_flag")));
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return facts;
    }

    // Reduce a test name to the citation-id charset the attribution regex accepts ([A-Za-z0-9-.]); dropping
    // spaces is what lets a multi-word name like "LDL Cholesterol" cite as [Lab/LDLCholesterol].
    private static string Slugify(string value) =>
        new(value.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.').ToArray());

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    // One extracted patient lab value in citable form (Slug is the whitespace-free [Lab/<slug>] id).
    private sealed record LabFact(
        string Slug, string TestName, string Value, string? Unit, string? ReferenceRange, bool? Abnormal);
}
