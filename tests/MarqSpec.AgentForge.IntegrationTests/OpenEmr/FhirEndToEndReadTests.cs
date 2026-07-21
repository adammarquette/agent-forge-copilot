using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.IntegrationTests.Support;

namespace MarqSpec.AgentForge.IntegrationTests.OpenEmr;

/// <summary>
/// End-to-end FHIR reads against a live QA server, authenticated with a pre-obtained test token
/// (see <see cref="QaOpenEmrOptions.TestAccessToken"/>). This is the real payload-parsing
/// contract-drift coverage the unit-tier mappers can't provide on their own - a hand-built JSON
/// fixture in a unit test can only prove the mapper handles the shape we assumed; only a real
/// server response proves the assumption was right.
/// </summary>
/// <remarks>
/// Obtaining a valid access token requires completing the interactive SMART authorization-code +
/// PKCE flow once against a real test patient, which needs a browser and a logged-in clinician -
/// it can't be automated from a test runner. Whoever stands up the QA environment does this once
/// and stores the result as a CI/CD variable; these tests fail with a clear message rather than
/// silently passing when it isn't configured, per the "meaningful check, not an unconditional
/// pass" policy applied elsewhere in this suite (NFR-REL-2).
/// </remarks>
public sealed class FhirEndToEndReadTests : IClassFixture<OpenEmrQaFixture>
{
    private readonly OpenEmrQaFixture _fixture;

    public FhirEndToEndReadTests(OpenEmrQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetPatientAsync_ConfiguredTestToken_ReturnsRealPatientRecord()
    {
        RequireTestAccessToken();
        var client = new OpenEmrFhirClient(_fixture.FhirApi);

        var record = await client.GetPatientAsync(
            _fixture.Options.Site, _fixture.Options.TestPatientId!, CancellationToken.None);

        record.Should().NotBeNull();
        record!.Source.ResourceType.Should().Be("Patient");
        record.Source.Id.Should().Be(_fixture.Options.TestPatientId);
    }

    [Fact]
    public async Task GetConditionsAsync_ConfiguredTestToken_ParsesRealSearchSetBundleWithoutError()
    {
        RequireTestAccessToken();
        var client = new OpenEmrFhirClient(_fixture.FhirApi);

        var act = () => client.GetConditionsAsync(
            _fixture.Options.Site, _fixture.Options.TestPatientId!, CancellationToken.None);

        await act.Should().NotThrowAsync(
            "a real Condition search-set Bundle from a live server must parse without a FhirParsingException");
    }

    [Fact]
    public async Task GetObservationsAsync_ConfiguredTestTokenAndLaboratoryCategory_ParsesRealSearchSetBundleWithoutError()
    {
        RequireTestAccessToken();
        var client = new OpenEmrFhirClient(_fixture.FhirApi);

        var act = () => client.GetObservationsAsync(
            _fixture.Options.Site, _fixture.Options.TestPatientId!, "laboratory", null, CancellationToken.None);

        await act.Should().NotThrowAsync(
            "a real Observation search-set Bundle from a live server must parse without a FhirParsingException - " +
            "this is exactly the category-search-param [CONFIRM] item from INTERFACE_CONTROL.md");
    }

    private void RequireTestAccessToken()
    {
        if (string.IsNullOrEmpty(_fixture.Options.TestAccessToken) || string.IsNullOrEmpty(_fixture.Options.TestPatientId))
        {
            throw new InvalidOperationException(
                $"This test requires {QaOpenEmrOptions.SectionName}__TestAccessToken and " +
                $"{QaOpenEmrOptions.SectionName}__TestPatientId - a token obtained once via the interactive " +
                "SMART launch flow against a real test patient in the QA environment, stored as a CI/CD variable.");
        }
    }
}
