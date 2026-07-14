using FluentAssertions;
using GauntletAI.AgentForge.Evals;

namespace GauntletAI.AgentForge.EvalTests;

/// <summary>
/// Runs the golden set's <b>deterministic</b> rubrics as fast, hermetic xUnit tests — one result per case —
/// in the standard <c>dotnet test</c> flow. These three rubrics are mechanically checked and never call an
/// LLM judge (evals/README.md), so a regression in the extraction/schema pipeline turns a case red here
/// immediately. The baseline/regression policy and the judge-bound rubrics (<c>factually_consistent</c>,
/// <c>safe_refusal</c>) stay in the console <c>evals</c> gate (.gitlab/ci/evals.yml), which never runs here.
/// reference: documentation/W2_ARCHITECTURE.md §8
/// </summary>
public sealed class DeterministicRubricTests
{
    // The rubrics evals/README.md pins as deterministic (no judge, cannot drift). The judge-bound rubrics are
    // deliberately excluded so this hermetic suite never depends on a live model.
    private static readonly HashSet<string> DeterministicRubrics =
        ["schema_valid", "citation_present", "no_phi_in_logs"];

    public static TheoryData<string> GoldenCases()
    {
        var data = new TheoryData<string>();
        foreach (var name in GoldenSet.CaseFileNames())
        {
            data.Add(name);
        }

        return data;
    }

    [Fact]
    public void GoldenSet_WhenResolved_IsNotEmpty() =>
        GoldenSet.CaseFileNames().Should().NotBeEmpty(
            "the eval gate is meaningless without golden cases — an empty set must fail, not silently pass");

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task GoldenCase_DeterministicRubrics_Pass(string caseFile)
    {
        var testCase = GoldenSet.Load(caseFile);
        var (result, logs) = await GoldenSet.RunAsync(testCase);

        var scores = RubricEvaluator.Evaluate(testCase, result, logs);
        var deterministic = testCase.Rubrics.Where(DeterministicRubrics.Contains).ToArray();

        deterministic.Should().NotBeEmpty(
            $"golden case '{testCase.Id}' should exercise at least one deterministic rubric");

        foreach (var rubric in deterministic)
        {
            scores[rubric].Should().BeTrue(
                $"deterministic rubric '{rubric}' must pass for golden case '{testCase.Id}'");
        }
    }
}
