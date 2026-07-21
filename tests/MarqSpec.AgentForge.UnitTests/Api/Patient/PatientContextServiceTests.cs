using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Patient;
using MarqSpec.AgentForge.Api.Session;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarqSpec.AgentForge.UnitTests.Api.Patient;

public sealed class PatientContextServiceTests
{
    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly IScopedAccessTokenProvider _tokenProvider = A.Fake<IScopedAccessTokenProvider>();
    private readonly PatientSessionContext _session = new("patient-token", "default", "patient-1", "dr-jones");

    private PatientContextService BuildSut() =>
        new(_fhirClient, _tokenProvider, NullLogger<PatientContextService>.Instance);

    private static PatientRecord Patient(string name, DateOnly? dob = null, string? gender = null) =>
        new(new ClinicalSourceRef("Patient", "patient-1"), name, dob, gender);

    private static ConditionRecord Condition(string display) =>
        new(new ClinicalSourceRef("Condition", display), display, "active", null);

    private static MedicationRecord Medication(string display) =>
        new(new ClinicalSourceRef("MedicationRequest", display), display, null, "active", null);

    private static AllergyRecord Allergy(string display) =>
        new(new ClinicalSourceRef("AllergyIntolerance", display), display, "active", null, null);

    public PatientContextServiceTests()
    {
        // Default happy path: every fetch returns an empty (reachable) result; individual tests
        // override the ones they care about.
        A.CallTo(() => _fhirClient.GetPatientAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<PatientRecord?>(Patient("Jane Roe")));
        A.CallTo(() => _fhirClient.GetConditionsAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ConditionRecord>>([]));
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MedicationRecord>>([]));
        A.CallTo(() => _fhirClient.GetAllergiesAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AllergyRecord>>([]));
    }

    [Fact]
    public async Task BuildAsync_Always_SetsAccessTokenFromSessionBeforeFetching()
    {
        await BuildSut().BuildAsync(_session, CancellationToken.None);

        _tokenProvider.AccessToken.Should().Be("patient-token");
    }

    [Fact]
    public async Task BuildAsync_Always_ScopesEveryFetchToTheSessionSiteAndPatient()
    {
        await BuildSut().BuildAsync(_session, CancellationToken.None);

        A.CallTo(() => _fhirClient.GetPatientAsync("default", "patient-1", A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => _fhirClient.GetConditionsAsync("default", "patient-1", A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "patient-1", A<CancellationToken>._)).MustHaveHappened();
        A.CallTo(() => _fhirClient.GetAllergiesAsync("default", "patient-1", A<CancellationToken>._)).MustHaveHappened();
    }

    [Fact]
    public async Task BuildAsync_PatientFound_ReturnsDemographicsAndTheSessionPatientId()
    {
        A.CallTo(() => _fhirClient.GetPatientAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<PatientRecord?>(Patient("Jane Roe", new DateOnly(1959, 3, 2), "female")));

        var result = await BuildSut().BuildAsync(_session, CancellationToken.None);

        result.PatientId.Should().Be("patient-1");
        result.DisplayName.Should().Be("Jane Roe");
        result.BirthDate.Should().Be(new DateOnly(1959, 3, 2));
        result.Gender.Should().Be("female");
    }

    [Fact]
    public async Task BuildAsync_PatientNotFound_ReturnsNullDemographicsButStillReportsTheSessionPatientId()
    {
        A.CallTo(() => _fhirClient.GetPatientAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<PatientRecord?>(null));

        var result = await BuildSut().BuildAsync(_session, CancellationToken.None);

        result.PatientId.Should().Be("patient-1");
        result.DisplayName.Should().BeNull();
        // Demographics missing is not a failure of the whole page - the patient is still in context.
        result.Demographics.Reachable.Should().BeTrue();
    }

    [Fact]
    public async Task BuildAsync_ClinicalDataPresent_ReportsReachableWithCounts()
    {
        A.CallTo(() => _fhirClient.GetConditionsAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ConditionRecord>>([Condition("Atrial fibrillation"), Condition("Hyperlipidemia")]));
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<MedicationRecord>>([Medication("Apixaban")]));
        A.CallTo(() => _fhirClient.GetAllergiesAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AllergyRecord>>([Allergy("Penicillin"), Allergy("Sulfa"), Allergy("Latex")]));

        var result = await BuildSut().BuildAsync(_session, CancellationToken.None);

        result.Problems.Should().Be(new ClinicalDataSummary(Reachable: true, Count: 2));
        result.Medications.Should().Be(new ClinicalDataSummary(Reachable: true, Count: 1));
        result.Allergies.Should().Be(new ClinicalDataSummary(Reachable: true, Count: 3));
    }

    [Fact]
    public async Task BuildAsync_OneClinicalFetchThrows_ThatClassIsMarkedUnreachableAndTheRestStillReturn()
    {
        // UC-5 graceful degradation: one failing resource must not blank the whole confirmation.
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "patient-1", A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("FHIR 503"));
        A.CallTo(() => _fhirClient.GetConditionsAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ConditionRecord>>([Condition("Atrial fibrillation")]));

        var result = await BuildSut().BuildAsync(_session, CancellationToken.None);

        result.Medications.Should().Be(new ClinicalDataSummary(Reachable: false, Count: 0));
        result.Problems.Should().Be(new ClinicalDataSummary(Reachable: true, Count: 1));
        result.DisplayName.Should().Be("Jane Roe");
    }

    [Fact]
    public async Task BuildAsync_PatientFetchThrows_MarksDemographicsUnreachableWithoutFailingOtherClasses()
    {
        A.CallTo(() => _fhirClient.GetPatientAsync("default", "patient-1", A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("FHIR timeout"));
        A.CallTo(() => _fhirClient.GetAllergiesAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AllergyRecord>>([Allergy("Penicillin")]));

        var result = await BuildSut().BuildAsync(_session, CancellationToken.None);

        result.Demographics.Reachable.Should().BeFalse();
        result.DisplayName.Should().BeNull();
        result.Allergies.Should().Be(new ClinicalDataSummary(Reachable: true, Count: 1));
    }

    [Fact]
    public async Task BuildAsync_Cancelled_PropagatesCancellationRatherThanDegrading()
    {
        // A cancelled request is the caller giving up, not a dependency failing - it must surface,
        // not be swallowed into a "unreachable" cell.
        A.CallTo(() => _fhirClient.GetConditionsAsync("default", "patient-1", A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());

        var act = () => BuildSut().BuildAsync(_session, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
