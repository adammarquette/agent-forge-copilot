using MarqSpec.AgentForge.SeedDemoPatients;

var baseUrl = Environment.GetEnvironmentVariable("SeedDemo__BaseUrl") ?? "https://openemr-staging-25fc.up.railway.app";
var site = Environment.GetEnvironmentVariable("SeedDemo__Site") ?? "default";

Console.WriteLine($"Seeding {DemoPatientCatalog.Patients.Count} demo patients into {baseUrl} (site: {site}).");

var accessToken = await AuthBootstrap.AcquireAccessTokenAsync(baseUrl, site);

using var http = new HttpClient();
var report = new SeedReport();

foreach (var patient in DemoPatientCatalog.Patients)
{
    var name = $"{patient.GivenName} {patient.FamilyName}";
    try
    {
        var created = await PatientSeederClient.CreatePatientAsync(http, baseUrl, site, accessToken, patient);
        Console.WriteLine($"Created {name} -> pid={created.Pid} uuid={created.Uuid}");
        report.RecordSuccess(patient, created);
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"FAILED {name}: {ex.Message}");
        report.RecordFailure(patient, ex.Message);
    }
}

report.PrintSummary();

Console.WriteLine();
Console.WriteLine("=== Verifying via FHIR read-back ===");
var verifiedCount = 0;
foreach (var outcome in report.Successes)
{
    var created = outcome.Created!;
    var exists = await PatientSeederClient.PatientExistsAsync(http, baseUrl, site, accessToken, created.Uuid);
    Console.WriteLine($"{(exists ? "OK  " : "MISS")} {outcome.Patient.GivenName} {outcome.Patient.FamilyName} (uuid={created.Uuid})");
    if (exists)
    {
        verifiedCount++;
    }
}

Console.WriteLine();
Console.WriteLine($"{verifiedCount}/{report.Successes.Count} created patients verified readable via FHIR.");
