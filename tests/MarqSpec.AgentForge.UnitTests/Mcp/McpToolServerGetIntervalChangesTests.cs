using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Mcp;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarqSpec.AgentForge.UnitTests.Mcp;

public sealed class McpToolServerGetIntervalChangesTests
{
    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly ICorrelationIdAccessor _correlationIdAccessor = A.Fake<ICorrelationIdAccessor>();
    private readonly McpToolServer _sut;

    public McpToolServerGetIntervalChangesTests()
    {
        A.CallTo(() => _correlationIdAccessor.CorrelationId).Returns("corr-1");
        _sut = new McpToolServer(_fhirClient, _correlationIdAccessor, NullLogger<McpToolServer>.Instance);
    }

    [Fact]
    public async Task GetIntervalChangesAsync_ValidRequest_FiltersMedicationsToThoseAuthoredSinceTheDate()
    {
        var meds = new List<MedicationRecord>
        {
            new(new ClinicalSourceRef("MedicationRequest", "1"), "Old med", null, "active", new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new(new ClinicalSourceRef("MedicationRequest", "2"), "New med", null, "active", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)),
        };
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MedicationRecord>>(meds));
        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "laboratory", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ObservationRecord>>([]));
        A.CallTo(() => _fhirClient.GetEncountersAsync("default", "1", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<EncounterRecord>>([]));

        var result = await _sut.GetIntervalChangesAsync(
            new GetIntervalChangesRequest { Site = "default", PatientId = "1", SinceDate = "ge2026-01-01" },
            CancellationToken.None);

        result.MedicationChanges.Should().ContainSingle().Which.Source.Id.Should().Be("2");
    }

    [Fact]
    public async Task GetIntervalChangesAsync_ValidRequest_PassesSinceDateThroughToLabsAndEncounters()
    {
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MedicationRecord>>([]));
        var labs = new List<ObservationRecord>
        {
            new(new ClinicalSourceRef("Observation", "1"), "laboratory", "INR", 2.3, "ratio", 2.0, 3.0, null, "final"),
        };
        var encounters = new List<EncounterRecord>
        {
            new(new ClinicalSourceRef("Encounter", "1"), "ED Visit", "finished", null, null, null),
        };
        A.CallTo(() => _fhirClient.GetObservationsAsync("default", "1", "laboratory", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ObservationRecord>>(labs));
        A.CallTo(() => _fhirClient.GetEncountersAsync("default", "1", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<EncounterRecord>>(encounters));

        var result = await _sut.GetIntervalChangesAsync(
            new GetIntervalChangesRequest { Site = "default", PatientId = "1", SinceDate = "ge2026-01-01" },
            CancellationToken.None);

        result.NewLabs.Should().BeEquivalentTo(labs);
        result.IntervalEncounters.Should().BeEquivalentTo(encounters);
    }

    [Fact]
    public async Task GetIntervalChangesAsync_MissingSinceDate_ThrowsContractExceptionWithoutCallingFhirClient()
    {
        var act = () => _sut.GetIntervalChangesAsync(
            new GetIntervalChangesRequest { Site = "default", PatientId = "1", SinceDate = string.Empty },
            CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetIntervalChangesAsync_MalformedSinceDate_ThrowsContractExceptionWithoutCallingFhirClient()
    {
        var act = () => _sut.GetIntervalChangesAsync(
            new GetIntervalChangesRequest { Site = "default", PatientId = "1", SinceDate = "last-tuesday" },
            CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }
}
