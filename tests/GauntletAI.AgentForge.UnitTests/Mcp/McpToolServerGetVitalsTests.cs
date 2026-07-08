using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Mcp;

public sealed class McpToolServerGetVitalsTests
{
    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly ICorrelationIdAccessor _correlationIdAccessor = A.Fake<ICorrelationIdAccessor>();
    private readonly McpToolServer _sut;

    public McpToolServerGetVitalsTests()
    {
        A.CallTo(() => _correlationIdAccessor.CorrelationId).Returns("corr-1");
        _sut = new McpToolServer(_fhirClient, _correlationIdAccessor, NullLogger<McpToolServer>.Instance);
    }

    [Fact]
    public async Task GetVitalsAsync_ValidRequest_QueriesVitalSignsCategoryAndReturnsResults()
    {
        var vitals = new List<ObservationRecord>
        {
            new(new ClinicalSourceRef("Observation", "2"), "vital-signs", "Heart rate", 72, "beats/minute", null, null, null, "final"),
        };
        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "vital-signs", null, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ObservationRecord>>(vitals));

        var result = await _sut.GetVitalsAsync(
            new GetVitalsRequest { Site = "default", PatientId = "1" }, CancellationToken.None);

        result.Vitals.Should().BeEquivalentTo(vitals);
    }

    [Fact]
    public async Task GetVitalsAsync_SinceDateProvided_PassesItThroughToFhirClient()
    {
        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "vital-signs", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ObservationRecord>>([]));

        await _sut.GetVitalsAsync(
            new GetVitalsRequest { Site = "default", PatientId = "1", SinceDate = "ge2026-01-01" }, CancellationToken.None);

        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "vital-signs", "ge2026-01-01", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetVitalsAsync_MalformedSinceDate_ThrowsContractExceptionWithoutCallingFhirClient()
    {
        var act = () => _sut.GetVitalsAsync(
            new GetVitalsRequest { Site = "default", PatientId = "1", SinceDate = "not-a-fhir-date-filter" },
            CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }
}
