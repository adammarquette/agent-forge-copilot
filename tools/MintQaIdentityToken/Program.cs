using GauntletAI.AgentForge.Integration.OpenEmr.Auth;
using GauntletAI.AgentForge.MintQaIdentityToken;
using Refit;

var baseUrl = Environment.GetEnvironmentVariable("MintToken__BaseUrl") ?? "https://openemr-uubp-development.up.railway.app";
var site = Environment.GetEnvironmentVariable("MintToken__Site") ?? "default";
var identityAPatientUuid = Environment.GetEnvironmentVariable("OpenEmrQa__TestPatientId");

Console.WriteLine($"Minting a second, patient-scoped QA identity token against {baseUrl} (site: {site}).");
Console.WriteLine("This is identity B for GitLab issue #27 (CrossIdentityAuthorizationTests): a real,");
Console.WriteLine("distinct SMART standalone-launch login, scoped to whichever patient you select/log in");
Console.WriteLine("as during the browser step below.");

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
Console.WriteLine($"Granted scope: {token.Scope}");
Console.WriteLine($"Expires in:    {(token.ExpiresIn is { } seconds ? $"{seconds}s (~{seconds / 60} min)" : "not advertised")}");
Console.WriteLine($"Patient claim: {token.Patient ?? "(none - the server did not return a patient context)"}");

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

if (!string.IsNullOrWhiteSpace(identityAPatientUuid) &&
    !string.Equals(identityAPatientUuid, token.Patient, StringComparison.OrdinalIgnoreCase))
{
    var canReadIdentityA = await FhirPatientProbe.CanReadAsync(http, baseUrl, site, token.AccessToken, identityAPatientUuid);
    Console.WriteLine(
        $"Isolation check - blocked from identity A's patient ({identityAPatientUuid}): " +
        $"{(canReadIdentityA ? "FAILED (leaked!)" : "OK (blocked)")}");
}
else
{
    Console.WriteLine("Isolation check skipped - set OpenEmrQa__TestPatientId in the environment to also");
    Console.WriteLine("verify this token cannot read identity A's patient before wiring it into CI.");
}

Console.WriteLine();
Console.WriteLine("=== Paste into GitLab (Settings -> CI/CD -> Variables, protected + masked) ===");
Console.WriteLine($"OpenEmrQa__SecondTestAccessToken = {token.AccessToken}");
Console.WriteLine($"OpenEmrQa__SecondTestPatientId   = {token.Patient}");
