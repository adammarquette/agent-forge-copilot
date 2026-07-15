using System.Text.Json;
using GauntletAI.AgentForge.Data.Entities;
using GauntletAI.AgentForge.Documents;
using GauntletAI.AgentForge.Evals;

namespace GauntletAI.AgentForge.EvalTests;

/// <summary>
/// Locates and runs the committed golden set the same way the console gate's Program does, so the xUnit
/// tests and the <c>evals</c> job share one extraction pipeline and cannot drift apart.
/// reference: evals/README.md
/// </summary>
internal static class GoldenSet
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The repo's <c>evals/golden</c> directory, resolved by walking up from the test assembly
    /// location — robust to the working directory the runner uses.</summary>
    public static string GoldenDir { get; } = ResolveGoldenDir();

    /// <summary>Golden-case file names (not full paths), ordered — used as the theory's data source.</summary>
    public static IReadOnlyList<string> CaseFileNames() =>
        [.. Directory.EnumerateFiles(GoldenDir, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)];

    public static GoldenCase Load(string fileName)
    {
        var path = Path.Combine(GoldenDir, fileName);
        return JsonSerializer.Deserialize<GoldenCase>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"Failed to parse golden case '{fileName}'.");
    }

    /// <summary>Runs the extraction pipeline for a case with the pinned stub response (no live API).</summary>
    public static async Task<(DocumentExtractionResult Result, IReadOnlyList<string> Logs)> RunAsync(
        GoldenCase testCase)
    {
        var logger = new CapturingLogger<DocumentExtractor>();
        var extractor = new DocumentExtractor(new StubLlmProvider(testCase.StubModelResponse), logger);
        byte[] syntheticBytes = [0];
        var result = await extractor.ExtractAsync(
            ParseDocType(testCase.DocType), syntheticBytes, testCase.MediaType, CancellationToken.None);
        return (result, logger.Messages);
    }

    private static ClinicalDocumentType ParseDocType(string docType) => docType switch
    {
        "lab_pdf" => ClinicalDocumentType.LabPdf,
        "intake_form" => ClinicalDocumentType.IntakeForm,
        _ => throw new InvalidOperationException($"Unknown doc_type '{docType}'."),
    };

    private static string ResolveGoldenDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "evals", "golden");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate evals/golden by walking up from the test assembly directory.");
    }
}
