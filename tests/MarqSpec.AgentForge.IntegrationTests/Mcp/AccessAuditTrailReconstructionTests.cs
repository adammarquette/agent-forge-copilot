using System.Text.RegularExpressions;
using FluentAssertions;
using MarqSpec.AgentForge.IntegrationTests.Support;
using MarqSpec.AgentForge.Mcp;
using Microsoft.Extensions.Logging;

namespace MarqSpec.AgentForge.IntegrationTests.Mcp;

/// <summary>
/// Proves FR-AUTH-4's audit trail is actually reconstructable, not just present: drives the real
/// <see cref="AuditingMcpToolServer"/> in front of the real, QA-deployed <see cref="McpToolServer"/>
/// through a short sequence of accesses attributed to different clinicians and correlation ids, then
/// parses the real captured log stream - not <see cref="AuditingMcpToolServerTests"/>'s fake logger's
/// in-memory list, the actual formatted lines <c>AccessAuditLog</c>'s <c>LoggerMessage</c> source-gen
/// writes - back into structured entries. The unit suite proves one call logs the right fields; this
/// proves the message format itself survives being read back by something that isn't the same code
/// that wrote it, which is the entire point of an audit trail (tests/AGENTS.md).
/// </summary>
[Trait("Metric", "M3-AuthorizationIntegrity")]
public sealed class AccessAuditTrailReconstructionTests : IClassFixture<McpToolServerQaFixture>, IDisposable
{
    private static readonly Regex AuditLinePattern = new(
        @"^ACCESS AUDIT: clinician=(?<clinician>\S+) accessed patient=(?<patient>\S+) via tool=(?<tool>\S+) correlation=(?<correlation>\S+)$",
        RegexOptions.Compiled);

    private readonly McpToolServerQaFixture _toolServerFixture;
    private readonly CapturingLoggerProvider _capturedLogs = new();

    public AccessAuditTrailReconstructionTests(McpToolServerQaFixture toolServerFixture) => _toolServerFixture = toolServerFixture;

    /// <inheritdoc />
    public void Dispose() => _capturedLogs.Dispose();

    [Fact]
    public async Task SequentialAccessesByDifferentClinicians_CapturedLogStream_ReconstructsExactlyWhoAccessedWhatAndWhen()
    {
        var site = _toolServerFixture.OpenEmr.Options.Site;
        var patientId = _toolServerFixture.OpenEmr.Options.TestPatientId!;
        using var loggerFactory = new LoggerFactory([_capturedLogs]);

        var auditorForRequestA = new AuditingMcpToolServer(
            _toolServerFixture.ToolServer,
            new MutableClinicianIdentityAccessor { ClinicianIdentity = "dr-jones" },
            new FixedCorrelationIdAccessor("corr-aaa"),
            loggerFactory.CreateLogger<AuditingMcpToolServer>());
        var auditorForRequestB = new AuditingMcpToolServer(
            _toolServerFixture.ToolServer,
            new MutableClinicianIdentityAccessor { ClinicianIdentity = "dr-smith" },
            new FixedCorrelationIdAccessor("corr-bbb"),
            loggerFactory.CreateLogger<AuditingMcpToolServer>());

        await auditorForRequestA.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = site, PatientId = patientId }, CancellationToken.None);
        await auditorForRequestB.GetLabsAsync(
            new GetLabsRequest { Site = site, PatientId = patientId }, CancellationToken.None);

        var trail = ReconstructAuditTrail(_capturedLogs.Lines);

        trail.Should().ContainInOrder(
            new AuditEntry("dr-jones", patientId, "get_patient_summary", "corr-aaa"),
            new AuditEntry("dr-smith", patientId, "get_labs", "corr-bbb"));
    }

    private static List<AuditEntry> ReconstructAuditTrail(IEnumerable<string> logLines) =>
        logLines
            .Select(line => AuditLinePattern.Match(line))
            .Where(match => match.Success)
            .Select(match => new AuditEntry(
                match.Groups["clinician"].Value,
                match.Groups["patient"].Value,
                match.Groups["tool"].Value,
                match.Groups["correlation"].Value))
            .ToList();

    private sealed record AuditEntry(string Clinician, string PatientId, string Tool, string CorrelationId);
}
