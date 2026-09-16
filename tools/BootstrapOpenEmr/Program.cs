// BootstrapOpenEmr — makes DEPLOYMENT.md §4's first-run bootstrap reproducible.
//
// §4 is titled "not config-as-code" because it is live state in OpenEMR's DATABASE, which no
// infrastructure tool can express: the Site Address Override, the EHR-launch skip, the module's
// Launch URI/Issuer/Launch Mode, and the per-client enable flags. Until now that was a manual
// Admin-GUI dance repeated on every fresh stack or volume reset — the single reason a deploy of
// this system was never reproducible end to end.
//
// The retired GitLab deploy job did NOT cover this. It re-asserted Railway *service variables*
// (Llm__ApiKey, BaseUrl, scopes) and said so explicitly: "NOT re-asserted here (cannot be Railway
// config-as-code): ... the OpenEMR-side module Launch URI/Issuer + EHR-launch-skip settings".
// .railway/railway.ts now owns the variable half declaratively; this tool owns the database half.
//
// Idempotent by construction — safe to run on every deploy. Globals use the same upsert the fork's
// own AgentForgeGlobalConfig::save() uses, so this writes exactly what the module's admin page would.
//
// reference: labs.gauntletai.com#140, documentation/DEPLOYMENT.md §4
//
//   dotnet run --project tools/BootstrapOpenEmr -- <frontDoorBaseUrl>
//
// Connection comes from the environment (same names the OpenEMR container itself uses):
//   MYSQL_HOST (default 127.0.0.1) · MYSQL_PORT (3306) · MYSQL_DATABASE (openemr) · MYSQL_USER (root)
//   password: MYSQL_ROOT_PASS / MYSQL_ROOT_PASSWORD for root, MYSQL_PASS / MYSQL_PASSWORD otherwise.
//
// The compose stack publishes only the front door, so MySQL is not reachable from the host by default —
// bring it up with the docker-compose.bootstrap.yml overlay, which adds a loopback-only publish.

using System.Globalization;
using MySqlConnector;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: BootstrapOpenEmr <frontDoorBaseUrl>");
    Console.Error.WriteLine("  e.g. dotnet run --project tools/BootstrapOpenEmr -- http://localhost:8080");
    return 2;
}

var frontDoor = args[0].TrimEnd('/');
if (!Uri.TryCreate(frontDoor, UriKind.Absolute, out var frontDoorUri))
{
    Console.Error.WriteLine($"Not an absolute URL: {frontDoor}");
    return 2;
}

// The whole aud/one-origin invariant (§2) rests on this value being the front door, so refuse the
// hostnames that look like someone passed a container's own address.
if (frontDoorUri.Host is "openemr" or "agent-forge-api" || frontDoorUri.Host.EndsWith(".railway.internal", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Refusing: '{frontDoorUri.Host}' is a service-internal host, not the front door.");
    Console.Error.WriteLine("site_addr_oath must equal the sidecar's OpenEmr__BaseUrl, or every launch fails the aud check.");
    return 2;
}

var user = Env("MYSQL_USER", "root");

// compose names the root password MYSQL_ROOT_PASSWORD and the openemr user's MYSQL_PASSWORD, so a shell
// that sourced the stack's .env would otherwise hand root the wrong one.
var passwordVars = string.Equals(user, "root", StringComparison.Ordinal)
    ? new[] { "MYSQL_ROOT_PASS", "MYSQL_ROOT_PASSWORD" }
    : new[] { "MYSQL_PASS", "MYSQL_PASSWORD" };

var csb = new MySqlConnectionStringBuilder
{
    Server = Env("MYSQL_HOST", "127.0.0.1"),
    Port = uint.Parse(Env("MYSQL_PORT", "3306"), CultureInfo.InvariantCulture),
    Database = Env("MYSQL_DATABASE", "openemr"),
    UserID = user,
    Password = passwordVars
        .Select(Environment.GetEnvironmentVariable)
        .FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "",
};

Console.WriteLine($"Bootstrapping OpenEMR at {csb.Server}:{csb.Port}/{csb.Database}");
Console.WriteLine($"Front door: {frontDoor}");
Console.WriteLine();

await using var db = new MySqlConnection(csb.ConnectionString);

// OpenEMR's first boot runs its own installer; a bootstrap racing it sees a half-built schema.
if (!await WaitForSchemaAsync(db, TimeSpan.FromMinutes(5)))
{
    Console.Error.WriteLine("Timed out waiting for the OpenEMR schema (globals + oauth_clients).");
    return 1;
}

var changed = 0;
var alreadyCorrect = 0;

// ---- 1. Globals ------------------------------------------------------------------------------
// site_addr_oath defaults to an EMPTY STRING, which PHP's ?? does not treat as unset, so every
// OAuth URL the server computes comes out as a bare path — breaking both the admin "Register New
// App" GUI and JWT audience validation. reference: documentation/DEPLOYMENT.md §4
var globals = new (string Name, string Value, string Why)[]
{
    ("site_addr_oath", frontDoor, "must equal the sidecar's OpenEmr__BaseUrl (aud check)"),
    ("oauth_ehr_launch_authorization_flow_skip", "1", "gates the per-client launch-prompt skip"),
    ("agentforge_launch_uri", $"{frontDoor}/agentforge/launch", "per-patient launch"),
    ("agentforge_agenda_launch_uri", $"{frontDoor}/agentforge/agenda/launch", "roster/agenda launch"),
    ("agentforge_issuer", $"{frontDoor}/apis/default/fhir", "FHIR issuer the launch validates against"),
    ("agentforge_launch_mode", "tab", "iframe mode loses the session cookie cross-origin"),
};

Console.WriteLine("globals:");
foreach (var (name, value, why) in globals)
{
    var current = await ScalarAsync(db, "SELECT gl_value FROM globals WHERE gl_name = @n LIMIT 1", ("@n", name));
    if (string.Equals(current, value, StringComparison.Ordinal))
    {
        Console.WriteLine($"  ok      {name} = {value}");
        alreadyCorrect++;
        continue;
    }

    // Same statement the fork's AgentForgeGlobalConfig::save() issues, so this is indistinguishable
    // from an admin saving the module's config page.
    await ExecuteAsync(db,
        "INSERT INTO `globals` (`gl_name`, `gl_value`) VALUES (@n, @v) ON DUPLICATE KEY UPDATE `gl_value` = @v",
        ("@n", name), ("@v", value));
    Console.WriteLine($"  SET     {name} = {value}   ({(current is null ? "was unset" : $"was '{current}'")}) — {why}");
    changed++;
}

// ---- 2. OAuth clients ------------------------------------------------------------------------
// Freshly-registered clients land DISABLED (the agenda client especially), so registration alone
// is not enough. Registration itself stays in tools/RegisterSmartClients (it is an API concern).
Console.WriteLine();
Console.WriteLine("oauth_clients:");

var clients = await QueryClientsAsync(db);
if (clients.Count == 0)
{
    Console.WriteLine("  none registered yet.");
    Console.WriteLine();
    Console.Error.WriteLine("No AgentForge SMART clients found. Register them first, then re-run this tool:");
    Console.Error.WriteLine($"  dotnet run --project tools/RegisterSmartClients -- {frontDoor}");
    return 1;
}

foreach (var c in clients)
{
    if (c.IsEnabled && c.SkipLaunchFlow)
    {
        Console.WriteLine($"  ok      {c.Name} ({c.ClientId})");
        alreadyCorrect++;
        continue;
    }

    await ExecuteAsync(db,
        "UPDATE oauth_clients SET is_enabled = 1, skip_ehr_launch_authorization_flow = 1 WHERE client_id = @id",
        ("@id", c.ClientId));
    Console.WriteLine($"  ENABLED {c.Name} ({c.ClientId})   (was is_enabled={(c.IsEnabled ? 1 : 0)}, skip={(c.SkipLaunchFlow ? 1 : 0)})");
    changed++;
}

Console.WriteLine();
Console.WriteLine($"Done: {changed} changed, {alreadyCorrect} already correct.");
Console.WriteLine();
Console.WriteLine("Still manual (not database state, so not this tool's job):");
Console.WriteLine("  - the sidecar's OpenEmr__ClientId/Secret + agenda pair (see RegisterSmartClients output)");
Console.WriteLine("  - demo data: the cardio1 provider, then tools/SeedDemoPatients");
return 0;

static string Env(string name, string fallback)
    => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

static async Task<bool> WaitForSchemaAsync(MySqlConnection db, TimeSpan timeout)
{
    var deadline = DateTimeOffset.UtcNow + timeout;
    var announced = false;
    while (DateTimeOffset.UtcNow < deadline)
    {
        try
        {
            if (db.State != System.Data.ConnectionState.Open)
            {
                await db.OpenAsync();
            }

            var tables = await ScalarAsync(db,
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('globals','oauth_clients')");
            if (tables == "2")
            {
                return true;
            }
        }
        catch (MySqlException)
        {
            // Not up yet; OpenEMR's first boot takes minutes on a fresh volume.
        }

        if (!announced)
        {
            Console.WriteLine("Waiting for the OpenEMR schema...");
            announced = true;
        }

        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    return false;
}

static async Task<string?> ScalarAsync(MySqlConnection db, string sql, params (string Name, object Value)[] p)
{
    await using var cmd = new MySqlCommand(sql, db);
    foreach (var (n, v) in p)
    {
        cmd.Parameters.AddWithValue(n, v);
    }

    var result = await cmd.ExecuteScalarAsync();
    return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
}

static async Task ExecuteAsync(MySqlConnection db, string sql, params (string Name, object Value)[] p)
{
    await using var cmd = new MySqlCommand(sql, db);
    foreach (var (n, v) in p)
    {
        cmd.Parameters.AddWithValue(n, v);
    }

    await cmd.ExecuteNonQueryAsync();
}

static async Task<List<OauthClient>> QueryClientsAsync(MySqlConnection db)
{
    var list = new List<OauthClient>();
    await using var cmd = new MySqlCommand(
        "SELECT client_id, client_name, is_enabled, skip_ehr_launch_authorization_flow " +
        "FROM oauth_clients WHERE client_name LIKE 'AgentForge%'", db);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new OauthClient(
            reader.GetString(0),
            reader.GetString(1),
            !reader.IsDBNull(2) && reader.GetInt32(2) == 1,
            !reader.IsDBNull(3) && reader.GetInt32(3) == 1));
    }

    return list;
}

internal sealed record OauthClient(string ClientId, string Name, bool IsEnabled, bool SkipLaunchFlow);
