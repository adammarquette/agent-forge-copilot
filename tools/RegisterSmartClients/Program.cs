using System.Net.Http.Json;
using System.Text.Json;

// Registers the two AgentForge SMART launch clients on a fresh/reseeded OpenEMR with the correct scope
// lists - INCLUDING patient/Binary.read (patient) and user/Binary.read (agenda), which OpenEMR's
// finalizeScopes drops when the client isn't registered for them, breaking click-to-source (gitlab#128).
//
// The OAuth clients are DB-only and vanish on an OpenEMR reseed (documentation/DEPLOYMENT.md §4 "OAuth clients").
// This makes their re-creation reproducible instead of a manual Admin-GUI dance: it POSTs the registrations,
// then prints the new client ids/secrets, the GitLab CI variables to update, and the one SQL statement that
// enables the clients + skips the per-launch authorization prompt.
//
// Usage:
//   dotnet run --project tools/RegisterSmartClients -- <frontDoorBaseUrl> [site]
//   e.g. dotnet run --project tools/RegisterSmartClients -- http://localhost:8080
// The base URL MUST be the reverse-proxy front door (never a service's own host), so the redirect_uri and the
// OAuth aud match the launch (documentation/DEPLOYMENT.md §2). No secrets are read or written by this tool.

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: RegisterSmartClients <frontDoorBaseUrl> [site]");
    return 1;
}

var baseUrl = args[0].TrimEnd('/');
var site = args.Length > 1 ? args[1] : "default";

// Registration scope lists are the SUPERSET the launch flow requests (deploy.yml OpenEmr__Scopes /
// OpenEmrAgenda__Scopes). finalizeScopes only grants a requested scope the client is registered for, so these
// must stay a superset of the deploy.yml lists - keep them in sync when a flow adds a scope.
// reference: .gitlab/ci/deploy.yml, gitlab#128.
ClientSpec[] clients =
[
    new(
        Name: "AgentForge Copilot (patient launch)",
        RedirectPath: "/agentforge/callback",
        CiIdVar: "OpenEmr__ClientId",
        CiSecretVar: "OpenEmr__ClientSecret",
        Scope: string.Join(' ',
            "openid", "fhirUser", "launch", "launch/patient", "api:fhir",
            "patient/Patient.read", "patient/Encounter.read", "patient/Observation.read",
            "patient/DocumentReference.read", "patient/Binary.read", "patient/Condition.read",
            "patient/AllergyIntolerance.read", "patient/MedicationRequest.read", "patient/Procedure.read",
            "patient/DiagnosticReport.read", "api:oemr")),
    new(
        Name: "AgentForge Copilot (roster/agenda launch)",
        RedirectPath: "/agentforge/agenda/callback",
        CiIdVar: "OPenEmrAgenda__ClientId", // reference: DEPLOYMENT.md §3 - the deployed var name carried this casing typo
        CiSecretVar: "OpenEmrAgenda__ClientSecret",
        Scope: string.Join(' ',
            "openid", "fhirUser", "launch", "api:fhir",
            "user/Appointment.read", "user/Patient.read", "user/Encounter.read", "user/Observation.read",
            "user/DocumentReference.read", "user/Binary.read", "user/Condition.read",
            "user/AllergyIntolerance.read", "user/MedicationRequest.read", "user/Procedure.read",
            "user/DiagnosticReport.read")),
];

using var http = new HttpClient();
var registered = new List<(ClientSpec Spec, string ClientId, string? ClientSecret)>();

foreach (var spec in clients)
{
    Console.WriteLine($"Registering '{spec.Name}' ...");
    var body = new
    {
        application_type = "private",
        client_name = spec.Name,
        redirect_uris = new[] { baseUrl + spec.RedirectPath },
        grant_types = new[] { "authorization_code", "refresh_token" },
        response_types = new[] { "code" },
        token_endpoint_auth_method = "client_secret_post",
        scope = spec.Scope,
    };

    using var response = await http.PostAsJsonAsync($"{baseUrl}/oauth2/{site}/registration", body);
    var payload = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"  FAILED: {(int)response.StatusCode} {response.StatusCode} - {payload}");
        return 1;
    }

    using var doc = JsonDocument.Parse(payload);
    var root = doc.RootElement;
    var clientId = root.GetProperty("client_id").GetString()
        ?? throw new InvalidOperationException("Registration response carried no client_id.");
    var clientSecret = root.TryGetProperty("client_secret", out var s) ? s.GetString() : null;
    var grantedScope = root.TryGetProperty("scope", out var sc) ? sc.GetString() ?? string.Empty : string.Empty;

    // finalizeScopes may still trim at registration; fail loudly if the Binary scope didn't survive, since a
    // silent drop is exactly the #128 failure this tool exists to prevent.
    if (!grantedScope.Contains("/Binary.read", StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"  WARNING: registered scope has no *Binary.read - click-to-source will 404.\n  scope: {grantedScope}");
    }

    registered.Add((spec, clientId, clientSecret));
    Console.WriteLine($"  client_id: {clientId}");
}

Console.WriteLine();
Console.WriteLine("=== Next steps (reference: documentation/DEPLOYMENT.md §4) ===");
Console.WriteLine();
Console.WriteLine("1) Set these GitLab CI/CD variables (masked) to the values below, then re-run the deploy:");
foreach (var (spec, clientId, clientSecret) in registered)
{
    Console.WriteLine($"   {spec.CiIdVar} = {clientId}");
    Console.WriteLine($"   {spec.CiSecretVar} = {clientSecret ?? "(none returned - public client)"}");
}

Console.WriteLine();
Console.WriteLine("2) Enable each client + skip the per-launch authorization prompt (run against OpenEMR MySQL):");
var ids = string.Join(",", registered.Select(r => $"'{r.ClientId}'"));
Console.WriteLine($"   UPDATE oauth_clients SET is_enabled = 1, skip_ehr_launch_authorization_flow = 1 WHERE client_id IN ({ids});");
Console.WriteLine();
Console.WriteLine("   Also confirm the global 'OAuth2 EHR-Launch Authorization Flow Skip' setting is enabled");
Console.WriteLine("   (Admin -> Config -> Connectors) - it gates the per-client skip above.");

return 0;

internal sealed record ClientSpec(string Name, string RedirectPath, string CiIdVar, string CiSecretVar, string Scope);
