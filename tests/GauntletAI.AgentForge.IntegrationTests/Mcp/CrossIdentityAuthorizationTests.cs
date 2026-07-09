using FluentAssertions;
using GauntletAI.AgentForge.Mcp;
using Refit;

namespace GauntletAI.AgentForge.IntegrationTests.Mcp;

/// <summary>
/// Confirms entitlement is per-identity against two real, distinct SMART EHR launches - the exact
/// case ARCHITECTURE.md §5.6 flags as <c>[PROVISIONAL: confirm scope granularity ... against
/// OpenEMR's ACL model]</c>. AUDIT.md separately warns that OpenEMR's own app-level RBAC does
/// <i>not</i> restrict reads by patient panel out of the box - so §5.3's claim that "the Co-Pilot
/// cannot exceed the user's own access" rests entirely on the SMART launch's OAuth
/// <c>patient/*.read</c> scope being enforced at the FHIR API layer specifically. A unit test can't
/// touch this: it fakes <see cref="Integration.OpenEmr.Fhir.IOpenEmrFhirClient"/>, so it can only
/// ever prove the tool layer passes arguments through correctly, never that the real server actually
/// honors them. This is the one place in the suite that can actually confirm or refute the
/// PROVISIONAL note.
/// </summary>
[Trait("Metric", "M3-AuthorizationIntegrity")]
public sealed class CrossIdentityAuthorizationTests : IClassFixture<CrossIdentityAuthorizationQaFixture>
{
    private readonly CrossIdentityAuthorizationQaFixture _fixture;

    public CrossIdentityAuthorizationTests(CrossIdentityAuthorizationQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetPatientSummaryAsync_EachIdentityQueryingItsOwnPatient_ReturnsThatIdentitysRealData()
    {
        // Control for the two tests below: the boundary isn't "every cross-identity call fails" -
        // each identity's own, legitimate launch context must still work.
        var requestA = new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.PatientIdA };
        var requestB = new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.PatientIdB };

        var resultA = await _fixture.ToolServerA.GetPatientSummaryAsync(requestA, CancellationToken.None);
        var resultB = await _fixture.ToolServerB.GetPatientSummaryAsync(requestB, CancellationToken.None);

        resultA.Patient.Should().NotBeNull("identity A's own SMART launch context must still resolve their own patient");
        resultB.Patient.Should().NotBeNull("identity B's own SMART launch context must still resolve their own patient");
        resultA.Patient!.Source.Id.Should().Be(_fixture.PatientIdA);
        resultB.Patient!.Source.Id.Should().Be(_fixture.PatientIdB);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_IdentityAsTokenAgainstIdentityBsPatient_NeverReturnsIdentityBsRealData()
    {
        var request = new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.PatientIdB };

        try
        {
            var result = await _fixture.ToolServerA.GetPatientSummaryAsync(request, CancellationToken.None);

            result.Patient.Should().BeNull(
                "OpenEMR returned identity B's real patient record to identity A's token instead of rejecting the " +
                "read - a direct breach of ARCHITECTURE.md §5.3 (\"the Co-Pilot cannot exceed the user's own " +
                "access\") and exactly the gap §5.6 flags PROVISIONAL");
        }
        catch (ApiException)
        {
            // Correctly rejected - OpenEMR's SMART patient-scope enforcement held for this identity/patient pair.
        }
    }

    [Fact]
    public async Task GetPatientSummaryAsync_IdentityBsTokenAgainstIdentityAsPatient_NeverReturnsIdentityAsRealData()
    {
        var request = new GetPatientSummaryRequest { Site = _fixture.OpenEmr.Options.Site, PatientId = _fixture.PatientIdA };

        try
        {
            var result = await _fixture.ToolServerB.GetPatientSummaryAsync(request, CancellationToken.None);

            result.Patient.Should().BeNull(
                "OpenEMR returned identity A's real patient record to identity B's token instead of rejecting the " +
                "read - a direct breach of ARCHITECTURE.md §5.3 (\"the Co-Pilot cannot exceed the user's own " +
                "access\") and exactly the gap §5.6 flags PROVISIONAL");
        }
        catch (ApiException)
        {
            // Correctly rejected - OpenEMR's SMART patient-scope enforcement held for this identity/patient pair.
        }
    }
}
