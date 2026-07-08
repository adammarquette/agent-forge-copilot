using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Api.Mcp;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Api.Mcp;

public sealed class McpToolServerGetLabsTests
{
    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly ICorrelationIdAccessor _correlationIdAccessor = A.Fake<ICorrelationIdAccessor>();
    private readonly McpToolServer _sut;

    public McpToolServerGetLabsTests()
    {
        A.CallTo(() => _correlationIdAccessor.CorrelationId).Returns("corr-1");
        _sut = new McpToolServer(_fhirClient, _correlationIdAccessor, NullLogger<McpToolServer>.Instance);
    }

    [Fact]
    public async Task GetLabsAsync_ValidRequest_QueriesLaboratoryCategoryAndReturnsResults()
    {
        var labs = new List<ObservationRecord>
        {
            new(new ClinicalSourceRef("Observation", "1"), "laboratory", "INR", 2.3, "ratio", 2.0, 3.0, null, "final"),
        };
        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "laboratory", null, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ObservationRecord>>(labs));

        var result = await _sut.GetLabsAsync(
            new GetLabsRequest { Site = "default", PatientId = "1" }, CancellationToken.None);

        result.Labs.Should().BeEquivalentTo(labs);
    }

    [Fact]
    public async Task GetLabsAsync_SinceDateProvided_PassesItThroughToFhirClient()
    {
        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "laboratory", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ObservationRecord>>([]));

        await _sut.GetLabsAsync(
            new GetLabsRequest { Site = "default", PatientId = "1", SinceDate = "ge2026-01-01" }, CancellationToken.None);

        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "laboratory", "ge2026-01-01", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLabsAsync_MalformedSinceDate_ThrowsContractExceptionWithoutCallingFhirClient()
    {
        var act = () => _sut.GetLabsAsync(
            new GetLabsRequest { Site = "default", PatientId = "1", SinceDate = "not-a-fhir-date-filter" },
            CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetLabsAsync_InvalidRequest_ThrowsContractExceptionWithoutCallingFhirClient()
    {
        var act = () => _sut.GetLabsAsync(
            new GetLabsRequest { Site = "default", PatientId = string.Empty }, CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }
}
