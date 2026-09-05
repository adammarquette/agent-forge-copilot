using MarqSpec.AgentForge.Integration.OpenEmr.Auth;
using MarqSpec.AgentForge.MintQaIdentityToken;
using Refit;

var baseUrl = Environment.GetEnvironmentVariable("MintToken__BaseUrl") ?? "http://localhost:8080";
var site = Environment.GetEnvironmentVariable("MintToken__Site") ?? "default";
var identityAPatientUuid = Environment.GetEnvironmentVariable("OpenEmrQa__TestPatientId");
var identityBPatientUuid = Environment.GetEnvironmentVariable("OpenEmrQa__SecondTestPatientId");

Console.WriteLine($"Minting a patient-scoped QA identity token against {baseUrl} (site: {site}).");
Console.WriteLine("For GitLab issue #27 (CrossIdentityAuthorizationTests): a real, SMART standalone-launch");
Console.WriteLine("login, scoped to whichever patient you select/log in as during the browser step below.");
Console.WriteLine("Log in as Ada Testpatient for identity A, or any other patient for identity B.");

using var authHttpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
var authApi = RestService.For<IOpenEmrAuthApi>(authHttpClient);
var authClient = new OpenEmrAuthClient(authApi);

string clientId;
string? clientSecret;
TokenResponse token;
try
{
    (clientId, clientSecret) = await TokenBootstrap.ResolveClientAsync(authClient, site);
    token = await TokenBootstrap.AcquireTokenAsync(authClient, baseUrl, site, clientId, clientSecret);
}
catch (ApiException ex)
{
    Console.WriteLine();
    Console.WriteLine($"OpenEMR rejected the request: {(int)ex.StatusCode} {ex.StatusCode}");
    Console.WriteLine(ex.Content ?? "(no response body)");
    return;
}

Console.WriteLine();
Console.WriteLine("=== Token minted ===");
Console.WriteLine($"Granted scope:  {token.Scope}");
Console.WriteLine($"Expires in:     {(token.ExpiresIn is { } seconds ? $"{seconds}s (~{seconds / 60} min)" : "not advertised")}");
Console.WriteLine($"Patient claim:  {token.Patient ?? "(none - the server did not return a patient context)"}");
Console.WriteLine($"Refresh token:  {(token.RefreshToken is null ? "(none - offline_access was not granted; this deployment may not support it)" : "present - see below")}");

if (string.IsNullOrEmpty(token.Patient))
{
    Console.WriteLine();
    Console.WriteLine("WARNING: no patient context came back. A token with no patient claim is not usable as");
    Console.WriteLine("identity B - CrossIdentityAuthorizationTests needs a token genuinely restricted to one");
    Console.WriteLine("patient, not a broad user-role grant. Check whether OpenEMR presented a patient picker");
    Console.WriteLine("during login/consent on this deployment.");
    return;
}

using var http = new HttpClient();
var canReadOwnPatient = await FhirPatientProbe.CanReadAsync(http, baseUrl, site, token.AccessToken, token.Patient);
Console.WriteLine($"Self-check      - can read its own patient ({token.Patient}): {(canReadOwnPatient ? "OK" : "FAILED")}");

var isIdentityA = !string.IsNullOrWhiteSpace(identityAPatientUuid) &&
    string.Equals(identityAPatientUuid, token.Patient, StringComparison.OrdinalIgnoreCase);
var otherPatientUuid = isIdentityA ? identityBPatientUuid : identityAPatientUuid;
var otherPatientLabel = isIdentityA ? "identity B's" : "identity A's";

if (!string.IsNullOrWhiteSpace(otherPatientUuid))
{
    var canReadOtherPatient = await FhirPatientProbe.CanReadAsync(http, baseUrl, site, token.AccessToken, otherPatientUuid);
    Console.WriteLine(
        $"Isolation check - blocked from {otherPatientLabel} patient ({otherPatientUuid}): " +
        $"{(canReadOtherPatient ? "FAILED (leaked!)" : "OK (blocked)")}");
}
else
{
    Console.WriteLine("Isolation check skipped - set OpenEmrQa__TestPatientId and/or OpenEmrQa__SecondTestPatientId");
    Console.WriteLine("in the environment to also verify this token cannot read the other identity's patient.");
}

Console.WriteLine();
Console.WriteLine("=== Paste into GitLab (Settings -> CI/CD -> Variables, protected + masked) ===");
if (token.RefreshToken is not null)
{
    Console.WriteLine("This deployment granted a refresh token - use it instead of the raw access token so");
    Console.WriteLine("OpenEmrQaFixture can mint a fresh access token every run instead of it expiring hourly");
    Console.WriteLine("(GitLab issue #29). OpenEmrQa__CrossIdentityClientId is SHARED - set it once, reuse for");
    Console.WriteLine("both identity A and B (set MintToken__ClientId to this same value on future runs so both");
    Console.WriteLine("logins use the same registered client, which the refresh grant requires).");
    Console.WriteLine();
    Console.WriteLine($"OpenEmrQa__CrossIdentityClientId = {clientId}");
    if (!string.IsNullOrEmpty(clientSecret))
    {
        Console.WriteLine($"OpenEmrQa__CrossIdentityClientSecret = {clientSecret}");
    }

    if (isIdentityA)
    {
        Console.WriteLine($"OpenEmrQa__CrossIdentityRefreshTokenA = {token.RefreshToken}");
    }
    else
    {
        Console.WriteLine($"OpenEmrQa__CrossIdentityRefreshTokenB = {token.RefreshToken}");
        Console.WriteLine($"OpenEmrQa__SecondTestPatientId        = {token.Patient}");
    }
}
else if (isIdentityA)
{
    Console.WriteLine($"OpenEmrQa__CrossIdentityTestAccessTokenA = {token.AccessToken}");
}
else
{
    Console.WriteLine($"OpenEmrQa__SecondTestAccessToken = {token.AccessToken}");
    Console.WriteLine($"OpenEmrQa__SecondTestPatientId   = {token.Patient}");
}
