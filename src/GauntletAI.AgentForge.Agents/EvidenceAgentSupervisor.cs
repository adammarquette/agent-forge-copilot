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

        // 2. evidence-retriever.
        Route(handoffs, SupervisorNode, "evidence-retriever", "question needs guideline evidence");
        var evidence = await _retriever.RetrieveAsync(request.Question, DefaultTopK, cancellationToken);

        // 3. answer-composer.
        Route(handoffs, SupervisorNode, "answer-composer", "facts + evidence assembled");
        var draft = await ComposeAsync(request.Question, factsJson, evidence, cancellationToken);

        // 4. critic = the Week 1 verification gate, reused as a node.
        Route(handoffs, "answer-composer", "critic", "draft ready for verification");
        var verification = _verifier.Verify(draft, BuildToolResults(factsJson, evidence));
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
        string question, string? factsJson, IReadOnlyList<EvidenceSnippet> evidence, CancellationToken cancellationToken)
    {
        var facts = factsJson ?? "(no document facts on file)";
        var evidenceText = evidence.Count == 0
            ? "(no guideline evidence found)"
            : string.Join("\n", evidence.Select((e, i) => $"[E{i + 1}] {e.DocumentId} - {e.Section} ({e.ChunkId}): {e.Text}"));

        var userContent = $"Question: {question}\n\nPatient facts (JSON):\n{facts}\n\nGuideline evidence:\n{evidenceText}";

        var response = await _llm.CompleteAsync(
            new LlmRequest(EvidenceComposerPrompt.System, [LlmMessage.FromText(LlmRole.User, userContent)]),
            cancellationToken);

        return response.Content;
    }

    // Hand the extracted facts and retrieved evidence to the critic as the tool results the answer must be
    // grounded in - the same shape the Week 1 orchestrator passes to the verifier.
    private static List<string> BuildToolResults(string? factsJson, IReadOnlyList<EvidenceSnippet> evidence)
    {
        var results = new List<string>();
        if (factsJson is not null)
        {
            results.Add(factsJson);
        }

        if (evidence.Count > 0)
        {
            results.Add(JsonSerializer.Serialize(evidence.ToArray(), AgentsJsonContext.Default.EvidenceSnippetArray));
        }

        return results;
    }
}
