using System.Net.Http.Headers;

namespace MarqSpec.AgentForge.MintQaIdentityToken;

/// <summary>
/// Confirms whether a bearer token can read a given patient's FHIR record - the exact entitlement
/// property <c>CrossIdentityAuthorizationTests</c> exercises, checked here before the token ever
/// reaches GitLab CI/CD variables.
/// </summary>
public static class FhirPatientProbe
{
    public static async Task<bool> CanReadAsync(
        HttpClient http, string baseUrl, string site, string accessToken, string patientUuid,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/apis/{site}/fhir/Patient/{patientUuid}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }
}
