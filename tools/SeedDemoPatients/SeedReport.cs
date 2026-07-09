namespace GauntletAI.AgentForge.SeedDemoPatients;

/// <summary>One patient's seeding outcome - success (with identifiers) or failure (with the reason).</summary>
public sealed record SeedOutcome(DemoPatient Patient, CreatedPatient? Created, string? Error);

/// <summary>Accumulates per-patient outcomes across the batch and prints a final summary table.</summary>
public sealed class SeedReport
{
    private readonly List<SeedOutcome> _outcomes = [];

    public void RecordSuccess(DemoPatient patient, CreatedPatient created) =>
        _outcomes.Add(new SeedOutcome(patient, created, null));

    public void RecordFailure(DemoPatient patient, string error) =>
        _outcomes.Add(new SeedOutcome(patient, null, error));

    public IReadOnlyList<SeedOutcome> Successes => [.. _outcomes.Where(o => o.Created is not null)];

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=== Seed summary ===");
        foreach (var outcome in _outcomes)
        {
            var name = $"{outcome.Patient.GivenName} {outcome.Patient.FamilyName}";
            if (outcome.Created is { } created)
            {
                Console.WriteLine($"OK    {name,-32} pid={created.Pid,-6} uuid={created.Uuid}");
            }
            else
            {
                Console.WriteLine($"FAIL  {name,-32} {outcome.Error}");
            }
        }

        var successCount = _outcomes.Count(o => o.Created is not null);
        Console.WriteLine();
        Console.WriteLine($"{successCount}/{_outcomes.Count} patients created.");
    }
}
