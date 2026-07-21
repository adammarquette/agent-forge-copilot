using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Http;
using MarqSpec.AgentForge.Mcp;
using MarqSpec.AgentForge.UnitTests.TestSupport;

namespace MarqSpec.AgentForge.UnitTests.Mcp;

public sealed class AuditingMcpToolServerTests
{
    private readonly IMcpToolServer _inner = A.Fake<IMcpToolServer>();
    private readonly IClinicianIdentityAccessor _clinicianIdentityAccessor = A.Fake<IClinicianIdentityAccessor>();
    private readonly ICorrelationIdAccessor _correlationIdAccessor = A.Fake<ICorrelationIdAccessor>();
    private readonly CapturingLogger<AuditingMcpToolServer> _logger = new();
    private readonly AuditingMcpToolServer _sut;

    public AuditingMcpToolServerTests()
    {
        A.CallTo(() => _clinicianIdentityAccessor.ClinicianIdentity).Returns("dr-jones");
        A.CallTo(() => _correlationIdAccessor.CorrelationId).Returns("corr-1");
        _sut = new AuditingMcpToolServer(_inner, _clinicianIdentityAccessor, _correlationIdAccessor, _logger);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_ValidRequest_ReturnsInnerServersResult()
    {
        var expected = new PatientSummaryResult(null, [], [], []);
        var request = new GetPatientSummaryRequest { Site = "default", PatientId = "123" };
        A.CallTo(() => _inner.GetPatientSummaryAsync(request, A<CancellationToken>._)).Returns(Task.FromResult(expected));

        var result = await _sut.GetPatientSummaryAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_ValidRequest_RecordsAnAuditEntryWithClinicianPatientToolAndCorrelation()
    {
        var request = new GetPatientSummaryRequest { Site = "default", PatientId = "123" };
        A.CallTo(() => _inner.GetPatientSummaryAsync(request, A<CancellationToken>._))
            .Returns(Task.FromResult(new PatientSummaryResult(null, [], [], [])));

        await _sut.GetPatientSummaryAsync(request, CancellationToken.None);

        _logger.Lines.Should().ContainSingle(line =>
            line.Contains("dr-jones", StringComparison.Ordinal) &&
            line.Contains("123", StringComparison.Ordinal) &&
            line.Contains("get_patient_summary", StringComparison.Ordinal) &&
            line.Contains("corr-1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetPatientSummaryAsync_NoClinicianIdentityAvailable_RecordsAnUnknownMarkerRatherThanThrowing()
    {
        // Defense in depth: SmartLaunchService already refuses to start a session with no
        // identity, but the audit layer itself must degrade gracefully rather than let a missing
        // identity crash the request (NFR-REL-1).
        A.CallTo(() => _clinicianIdentityAccessor.ClinicianIdentity).Returns(null);
        var request = new GetPatientSummaryRequest { Site = "default", PatientId = "123" };
        A.CallTo(() => _inner.GetPatientSummaryAsync(request, A<CancellationToken>._))
            .Returns(Task.FromResult(new PatientSummaryResult(null, [], [], [])));

        await _sut.GetPatientSummaryAsync(request, CancellationToken.None);

        _logger.Lines.Should().ContainSingle(line => line.Contains("unknown", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetLabsAsync_ValidRequest_ReturnsInnerServersResult()
    {
        var expected = new LabsResult([]);
        var request = new GetLabsRequest { Site = "default", PatientId = "123" };
        A.CallTo(() => _inner.GetLabsAsync(request, A<CancellationToken>._)).Returns(Task.FromResult(expected));

        var result = await _sut.GetLabsAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetVitalsAsync_ValidRequest_ReturnsInnerServersResult()
    {
        var expected = new VitalsResult([]);
        var request = new GetVitalsRequest { Site = "default", PatientId = "123" };
        A.CallTo(() => _inner.GetVitalsAsync(request, A<CancellationToken>._)).Returns(Task.FromResult(expected));

        var result = await _sut.GetVitalsAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetRecentEncountersAsync_ValidRequest_ReturnsInnerServersResult()
    {
        var expected = new RecentEncountersResult([]);
        var request = new GetRecentEncountersRequest { Site = "default", PatientId = "123", Count = 3 };
        A.CallTo(() => _inner.GetRecentEncountersAsync(request, A<CancellationToken>._)).Returns(Task.FromResult(expected));

        var result = await _sut.GetRecentEncountersAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetDocumentsAsync_ValidRequest_ReturnsInnerServersResult()
    {
        var expected = new DocumentsResult([]);
        var request = new GetDocumentsRequest { Site = "default", PatientId = "123" };
        A.CallTo(() => _inner.GetDocumentsAsync(request, A<CancellationToken>._)).Returns(Task.FromResult(expected));

        var result = await _sut.GetDocumentsAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetIntervalChangesAsync_ValidRequest_ReturnsInnerServersResult()
    {
        var expected = new IntervalChangesResult([], [], []);
        var request = new GetIntervalChangesRequest { Site = "default", PatientId = "123", SinceDate = "ge2026-01-01" };
        A.CallTo(() => _inner.GetIntervalChangesAsync(request, A<CancellationToken>._)).Returns(Task.FromResult(expected));

        var result = await _sut.GetIntervalChangesAsync(request, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }
}
