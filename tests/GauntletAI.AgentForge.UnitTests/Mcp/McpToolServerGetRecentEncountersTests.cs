using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GauntletAI.AgentForge.UnitTests.Mcp;

public sealed class McpToolServerGetRecentEncountersTests
{
    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly ICorrelationIdAccessor _correlationIdAccessor = A.Fake<ICorrelationIdAccessor>();
    private readonly McpToolServer _sut;

    public McpToolServerGetRecentEncountersTests()
    {
        A.CallTo(() => _correlationIdAccessor.CorrelationId).Returns("corr-1");
        _sut = new McpToolServer(_fhirClient, _correlationIdAccessor, NullLogger<McpToolServer>.Instance);
    }

    [Fact]
    public async Task GetRecentEncountersAsync_MoreEncountersThanCount_ReturnsOnlyMostRecentCountOfThem()
    {
        var encounters = new List<EncounterRecord>
        {
            Encounter("1", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Encounter("2", new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)),
            Encounter("3", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)),
        };
        A.CallTo(() => _fhirClient.GetEncountersAsync("default", "1", null, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<EncounterRecord>>(encounters));

        var result = await _sut.GetRecentEncountersAsync(
            new GetRecentEncountersRequest { Site = "default", PatientId = "1", Count = 2 }, CancellationToken.None);

        result.Encounters.Should().HaveCount(2);
        result.Encounters[0].Source.Id.Should().Be("2");
        result.Encounters[1].Source.Id.Should().Be("3");
    }

    [Fact]
    public async Task GetRecentEncountersAsync_DefaultCount_IsThree()
    {
        new GetRecentEncountersRequest { Site = "default", PatientId = "1" }.Count.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task GetRecentEncountersAsync_CountOutsideBounds_ThrowsContractExceptionWithoutCallingFhirClient(int count)
    {
        var act = () => _sut.GetRecentEncountersAsync(
            new GetRecentEncountersRequest { Site = "default", PatientId = "1", Count = count }, CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }

    private static EncounterRecord Encounter(string id, DateTimeOffset periodStart) =>
        new(new ClinicalSourceRef("Encounter", id), "Office Visit", "finished", periodStart, null, null);
}
