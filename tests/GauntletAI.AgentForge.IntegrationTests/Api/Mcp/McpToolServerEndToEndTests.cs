using FluentAssertions;
using GauntletAI.AgentForge.Api.Mcp;

namespace GauntletAI.AgentForge.IntegrationTests.Api.Mcp;

/// <summary>
/// Exercises every MCP tool against the real QA OpenEMR deployment and the real test patient -
/// the actual contract-drift and source-attribution coverage a unit test faking
/// IOpenEmrFhirClient can't provide. Per tests/AGENTS.md, nothing here is mocked.
/// </summary>
public sealed class McpToolServerEndToEndTests : IClassFixture<McpToolServerQaFixture>
{
    private readonly McpToolServerQaFixture _fixture;

    public McpToolServerEndToEndTests(McpToolServerQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetPatientSummaryAsync_RealTestPatient_ReturnsPatientMatchingConfiguredId()
    {
        var result = await _fixture.ToolServer.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.OpenEmr.Options.TestPatientId! },
            CancellationToken.None);

        result.Patient.Should().NotBeNull();
        result.Patient!.Source.Id.Should().Be(_fixture.OpenEmr.Options.TestPatientId);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_RealTestPatient_EveryReturnedFactCarriesAResolvableSourceCitation()
    {
        // Guards FR-VERIF-1's citation-key contract end-to-end: every record this tool returns
        // must resolve to {resourceType}/{id} from a resource the real server actually returned,
        // not a value a mocked mapper test could get away with fabricating.
        var result = await _fixture.ToolServer.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.OpenEmr.Options.TestPatientId! },
            CancellationToken.None);

        foreach (var problem in result.ActiveProblems)
        {
            problem.Source.ResourceType.Should().Be("Condition");
            problem.Source.Id.Should().NotBeNullOrWhiteSpace();
        }

        foreach (var medication in result.ActiveMedications)
        {
            medication.Source.ResourceType.Should().Be("MedicationRequest");
            medication.Source.Id.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GetRecentEncountersAsync_BoundedCount_NeverReturnsMoreThanRequested(int count)
    {
        // Guards minimum-necessary enforcement against real (possibly larger) data - a unit test
        // with a hand-built three-encounter fixture can't prove the bound holds against whatever
        // the real chart actually contains.
        var result = await _fixture.ToolServer.GetRecentEncountersAsync(
            new GetRecentEncountersRequest
            {
                Site = _fixture.OpenEmr.Options.Site,
                PatientId = _fixture.OpenEmr.Options.TestPatientId!,
                Count = count,
            },
            CancellationToken.None);

        result.Encounters.Should().HaveCountLessThanOrEqualTo(count);
    }

    [Fact]
    public async Task GetIntervalChangesAsync_RealTestPatient_ReturnsOnlyLabsOnOrAfterSinceDate()
    {
        const string sinceDateFilter = "ge2020-01-01";
        var sinceDate = McpDateFilter.ExtractDate(sinceDateFilter);

        var result = await _fixture.ToolServer.GetIntervalChangesAsync(
            new GetIntervalChangesRequest
            {
                Site = _fixture.OpenEmr.Options.Site,
                PatientId = _fixture.OpenEmr.Options.TestPatientId!,
                SinceDate = sinceDateFilter,
            },
            CancellationToken.None);

        // Guards that the real server actually honors the date search param the way
        // INTERFACE_CONTROL.md's [CONFIRM] note flags as unverified - a mocked unit test can only
        // prove we sent the right parameter, not that OpenEMR applied it correctly.
        result.NewLabs.Should().OnlyContain(
            lab => lab.EffectiveDateTime == null || lab.EffectiveDateTime >= sinceDate,
            "the server was asked to filter to {0} and onward", sinceDateFilter);
    }
}
