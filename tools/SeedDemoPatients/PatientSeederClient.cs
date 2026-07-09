using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GauntletAI.AgentForge.SeedDemoPatients;

/// <summary>Result of a successful patient create: the two identifiers OpenEMR assigns.</summary>
public sealed record CreatedPatient(int Pid, string Uuid);

/// <summary>
/// Creates and reads back <c>Patient</c> FHIR resources against QA OpenEMR
/// (<c>POST /apis/{site}/fhir/Patient</c>) - the only FHIR resource with a working create route on
/// this deployment (confirmed against the sibling OpenEMR fork's source; GitLab issue #26).
/// </summary>
public static class PatientSeederClient
{
    public static async Task<CreatedPatient> CreatePatientAsync(
        HttpClient http, string baseUrl, string site, string accessToken, DemoPatient patient,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            resourceType = "Patient",
            name = new[] { new { use = "official", family = patient.FamilyName, given = new[] { patient.GivenName } } },
            gender = patient.Gender,
            birthDate = patient.BirthDate,
            active = true,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/apis/{site}/fhir/Patient")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Creating patient '{patient.GivenName} {patient.FamilyName}' failed: " +
                $"{(int)response.StatusCode} {response.StatusCode} - {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        return new CreatedPatient(root.GetProperty("pid").GetInt32(), root.GetProperty("uuid").GetString()!);
    }

    /// <summary>Reads a patient back by FHIR uuid, for post-seed verification.</summary>
    public static async Task<bool> PatientExistsAsync(
        HttpClient http, string baseUrl, string site, string accessToken, string uuid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/apis/{site}/fhir/Patient/{uuid}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }
}
