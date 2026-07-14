using GauntletAI.AgentForge.Documents;

namespace GauntletAI.AgentForge.Evals;

/// <summary>Scores the five Week 2 boolean rubrics for one case result. Boolean, not 1-10, so a failure is
/// unambiguous and actionable (Week 2 Core Req 6).</summary>
internal static class RubricEvaluator
{
    public static IReadOnlyDictionary<string, bool> Evaluate(
        GoldenCase testCase, DocumentExtractionResult result, IReadOnlyList<string> logs)
    {
        var scores = new Dictionary<string, bool>();
        foreach (var rubric in testCase.Rubrics)
        {
            scores[rubric] = rubric switch
            {
                // The schema gate behaved as expected: valid input extracted, malformed input rejected.
                "schema_valid" => result.Succeeded == testCase.ExpectSuccess,

                // Every successful extraction carries citations.
                "citation_present" => !testCase.ExpectSuccess
                    || (result.CanonicalJson?.Contains("citation", StringComparison.OrdinalIgnoreCase) ?? false),

                // Extracted values match the ground truth.
                "factually_consistent" => !testCase.ExpectSuccess || AllExpectedPresent(testCase, result),

                // Bad input is refused cleanly rather than fabricated.
                "safe_refusal" => testCase.ExpectSuccess
                    || (!result.Succeeded && !string.IsNullOrWhiteSpace(result.RejectionReason)),

                // No sensitive value leaked into logs.
                "no_phi_in_logs" => NoPhiInLogs(testCase, logs),

                _ => throw new InvalidOperationException($"Unknown rubric '{rubric}' in case '{testCase.Id}'."),
            };
        }

        return scores;
    }

    private static bool AllExpectedPresent(GoldenCase testCase, DocumentExtractionResult result) =>
        testCase.ExpectedValues is null
        || (result.CanonicalJson is { } json
            && testCase.ExpectedValues.All(v => json.Contains(v, StringComparison.Ordinal)));

    private static bool NoPhiInLogs(GoldenCase testCase, IReadOnlyList<string> logs) =>
        testCase.PhiTokens is null
        || !logs.Any(line => testCase.PhiTokens.Any(t => line.Contains(t, StringComparison.OrdinalIgnoreCase)));
}
