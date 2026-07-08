using System.Text.RegularExpressions;

namespace GauntletAI.AgentForge.Verification;

/// <summary>
/// Deterministic, line-level implementation of <see cref="ISourceAttributionEngine"/>.
/// <para>
/// A line fails and is suppressed when either: it carries a citation bracket
/// (<c>[ResourceType/Id]</c>) that does not resolve against <see cref="Verify"/>'s
/// <c>availableCitations</c> (a fabricated/hallucinated reference); or it carries no citation at
/// all but names a specific clinical value (<see cref="ClinicalFactKeywords"/>) without also
/// reading as a reported gap (<see cref="GapIndicatorPhrases"/>) - CardiologyProfile explicitly
/// asks the model to state gaps plainly ("no INR on file..."), and a gap has nothing to cite by
/// definition, so it must not be treated the same as an uncited claim.
/// </para>
/// <para>
/// The gap-vs-claim check runs per clause (split on commas, semicolons, and " but "), not per
/// whole line, so a line that both reports a gap and asserts an unrelated uncited claim ("No
/// significant change, but her potassium is 6.2 today.") doesn't let the second clause ride along
/// on the first clause's exemption.
/// </para>
/// <para>
/// <b>Known limitation</b> (ARCHITECTURE.md §9.3's framing applied here): both keyword lists are a
/// deliberately narrow, reviewable heuristic, not real NLP claim extraction - a claim phrased
/// without any listed keyword can still slip through uncited, and a gap statement using unlisted
/// phrasing could be suppressed as if it were a claim. Documented, not hidden.
/// </para>
/// </summary>
public sealed class SourceAttributionEngine : ISourceAttributionEngine
{
    private static readonly Regex CitationPattern = new(
        @"\[([A-Za-z]+/[A-Za-z0-9\-\.]+)\]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Reviewable, versioned list of terms indicating a line asserts a specific quantitative
    /// clinical value (a dose, a lab result, a measurement) - the kind of claim FR-VERIF-1 treats
    /// as needing a citation.
    /// </summary>
    private static readonly string[] ClinicalFactKeywords =
    [
        "mg", "inr", "creatinine", "potassium", "k+", "ejection fraction", " ef ", "bnp", "nt-probnp",
        "systolic", "diastolic", "bpm", "mmhg", "meq",
    ];

    /// <summary>
    /// Reviewable, versioned list of phrases indicating a line is reporting an absence or
    /// uncertainty (UC-5) rather than asserting a fact - these never need a citation, since there
    /// is nothing to cite.
    /// </summary>
    private static readonly string[] GapIndicatorPhrases =
    [
        "no ", "not on file", "cannot verify", "cannot confirm", "unable to", "unavailable",
        "no data", "i cannot", "i am scoped", "i'm scoped", "refuse",
    ];

    /// <inheritdoc />
    public AttributionResult Verify(string answer, IReadOnlyCollection<string> availableCitations)
    {
        if (string.IsNullOrEmpty(answer))
        {
            return new AttributionResult(true, string.Empty, []);
        }

        var citationSet = new HashSet<string>(availableCitations, StringComparer.Ordinal);
        var keptLines = new List<string>();
        var suppressed = new List<SuppressedClaim>();

        foreach (var rawLine in answer.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var reason = Evaluate(line, citationSet);

            if (reason is null)
            {
                keptLines.Add(line);
            }
            else
            {
                suppressed.Add(new SuppressedClaim(line, reason));
            }
        }

        return new AttributionResult(suppressed.Count == 0, string.Join('\n', keptLines), suppressed);
    }

    /// <summary>Clause boundaries a single line gets split on before the gap-vs-claim check.</summary>
    private static readonly string[] ClauseDelimiters = [",", ";", " but "];

    private static string? Evaluate(string line, HashSet<string> availableCitations)
    {
        var matches = CitationPattern.Matches(line);
        if (matches.Count > 0)
        {
            foreach (Match match in matches)
            {
                var citation = match.Groups[1].Value;
                if (!availableCitations.Contains(citation))
                {
                    return $"cites {citation}, which no tool actually returned this turn";
                }
            }

            return null;
        }

        // Evaluated per clause, not per whole line: a single line can both report a gap and
        // assert an unrelated, uncited claim ("No significant change, but her potassium is 6.2
        // today.") - checking the gap phrase against the entire line would let the second clause
        // ride along on the first clause's exemption.
        foreach (var clause in line.Split(ClauseDelimiters, StringSplitOptions.None))
        {
            var lowerClause = clause.ToLowerInvariant();
            if (GapIndicatorPhrases.Any(lowerClause.Contains))
            {
                continue;
            }

            if (ClinicalFactKeywords.Any(lowerClause.Contains))
            {
                return "asserts a clinical value with no citation";
            }
        }

        return null;
    }
}
