using System.Text.Json;
using MarqSpec.AgentForge.Data.Entities;
using MarqSpec.AgentForge.Documents;
using MarqSpec.AgentForge.Evals;

// Golden-set eval gate (Week 2 Core Req 6 / HARD GATE). Deterministic: each case pins the model's response,
// so a regression anywhere in the extraction pipeline flips a case and fails the build. No live API needed.
var evalsDir = args.Length > 0 ? args[0] : "evals";
var goldenDir = Path.Combine(evalsDir, "golden");
var baselinePath = Path.Combine(evalsDir, "baseline.json");
var resultsPath = Path.Combine(evalsDir, "results.json");

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
};

if (!Directory.Exists(goldenDir) || !File.Exists(baselinePath))
{
    await Console.Error.WriteLineAsync($"Eval assets not found under '{evalsDir}' (need golden/ and baseline.json).");
    return 2;
}

var cases = new List<GoldenCase>();
foreach (var file in Directory.GetFiles(goldenDir, "*.json", SearchOption.AllDirectories))
{
    var parsed = JsonSerializer.Deserialize<GoldenCase>(await File.ReadAllTextAsync(file), jsonOptions)
        ?? throw new InvalidOperationException($"Failed to parse golden case: {file}");
    cases.Add(parsed);
}

if (cases.Count == 0)
{
    await Console.Error.WriteLineAsync("No golden cases found.");
    return 2;
}

var baseline = JsonSerializer.Deserialize<EvalBaseline>(await File.ReadAllTextAsync(baselinePath), jsonOptions)
    ?? throw new InvalidOperationException("Failed to parse baseline.json.");

byte[] syntheticBytes = [0];
var perRubricPass = new Dictionary<string, int>();
var perRubricTotal = new Dictionary<string, int>();
var caseReports = new List<object>();

foreach (var testCase in cases.OrderBy(c => c.Id, StringComparer.Ordinal))
{
    var docType = ParseDocType(testCase.DocType);
    var logger = new CapturingLogger<DocumentExtractor>();
    var extractor = new DocumentExtractor(
        new StubLlmProvider(testCase.StubModelResponse),
        new PdfPigWordReader(new CapturingLogger<PdfPigWordReader>()),
        logger);

    var result = await extractor.ExtractAsync(docType, syntheticBytes, testCase.MediaType, CancellationToken.None);
    var scores = RubricEvaluator.Evaluate(testCase, result, logger.Messages);

    foreach (var (rubric, passed) in scores)
    {
        perRubricTotal[rubric] = perRubricTotal.GetValueOrDefault(rubric) + 1;
        if (passed)
        {
            perRubricPass[rubric] = perRubricPass.GetValueOrDefault(rubric) + 1;
        }
    }

    caseReports.Add(new { id = testCase.Id, category = testCase.Category, scores });
}

var categoryRates = new Dictionary<string, double>();
var failures = new List<string>();
foreach (var rubric in perRubricTotal.Keys.OrderBy(k => k, StringComparer.Ordinal))
{
    var rate = (double)perRubricPass.GetValueOrDefault(rubric) / perRubricTotal[rubric];
    categoryRates[rubric] = rate;

    var baselineRate = baseline.Categories.GetValueOrDefault(rubric, baseline.PassThreshold);
    if (rate < baseline.PassThreshold)
    {
        failures.Add($"{rubric}: {rate:P0} is below the {baseline.PassThreshold:P0} pass threshold");
    }
    else if (rate < baselineRate - baseline.MaxRegression)
    {
        failures.Add($"{rubric}: {rate:P0} regressed more than {baseline.MaxRegression:P0} from baseline {baselineRate:P0}");
    }
}

var gatePassed = failures.Count == 0;
var report = new
{
    generated_at = DateTimeOffset.UtcNow,
    total_cases = cases.Count,
    passed = gatePassed,
    category_rates = categoryRates,
    failures,
    cases = caseReports,
};
await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"Eval gate: {cases.Count} cases across {perRubricTotal.Count} rubric categories");
foreach (var (rubric, rate) in categoryRates.OrderBy(k => k.Key, StringComparer.Ordinal))
{
    Console.WriteLine($"  {rubric,-22} {rate,6:P0}  ({perRubricPass.GetValueOrDefault(rubric)}/{perRubricTotal[rubric]})");
}

if (gatePassed)
{
    Console.WriteLine($"PASS - no rubric category below threshold or regressed. Results: {resultsPath}");
    return 0;
}

await Console.Error.WriteLineAsync("FAIL - the eval gate blocked the build:");
foreach (var failure in failures)
{
    await Console.Error.WriteLineAsync($"  - {failure}");
}

return 1;

static ClinicalDocumentType ParseDocType(string docType) => docType switch
{
    "lab_pdf" => ClinicalDocumentType.LabPdf,
    "intake_form" => ClinicalDocumentType.IntakeForm,
    _ => throw new InvalidOperationException($"Unknown doc_type '{docType}'."),
};
