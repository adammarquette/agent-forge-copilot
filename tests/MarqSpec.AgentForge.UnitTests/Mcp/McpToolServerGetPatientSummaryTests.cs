using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Mcp;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using MarqSpec.AgentForge.Integration.OpenEmr.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarqSpec.AgentForge.UnitTests.Mcp;

public sealed class McpToolServerGetPatientSummaryTests
{
    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly ICorrelationIdAccessor _correlationIdAccessor = A.Fake<ICorrelationIdAccessor>();
    private readonly McpToolServer _sut;

    public McpToolServerGetPatientSummaryTests()
    {
        A.CallTo(() => _correlationIdAccessor.CorrelationId).Returns("corr-1");
        _sut = new McpToolServer(_fhirClient, _correlationIdAccessor, NullLogger<McpToolServer>.Instance);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_ValidRequest_ReturnsAggregatedBundleFromAllFourFhirCalls()
    {
        var patient = new PatientRecord(new ClinicalSourceRef("Patient", "1"), "Jane Smith", null, "female");
        var problems = new List<ConditionRecord> { new(new ClinicalSourceRef("Condition", "10"), "AFib", "active", null) };
        var meds = new List<MedicationRecord> { new(new ClinicalSourceRef("MedicationRequest", "20"), "Warfarin", null, "active", null) };
        var allergies = new List<AllergyRecord> { new(new ClinicalSourceRef("AllergyIntolerance", "30"), "Penicillin", "active", null, null) };

        A.CallTo(() => _fhirClient.GetPatientAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<PatientRecord?>(patient));
        A.CallTo(() => _fhirClient.GetConditionsAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<ConditionRecord>>(problems));
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<MedicationRecord>>(meds));
        A.CallTo(() => _fhirClient.GetAllergiesAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<AllergyRecord>>(allergies));

        var result = await _sut.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = "default", PatientId = "1" }, CancellationToken.None);

        result.Patient.Should().Be(patient);
        result.ActiveProblems.Should().BeEquivalentTo(problems);
        result.ActiveMedications.Should().BeEquivalentTo(meds);
        result.Allergies.Should().BeEquivalentTo(allergies);
    }

    [Fact]
    public async Task GetPatientSummaryAsync_InvalidRequest_ThrowsContractExceptionWithoutCallingFhirClient()
    {
        var act = () => _sut.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = "default", PatientId = string.Empty }, CancellationToken.None);

        await act.Should().ThrowAsync<McpToolContractException>();
        A.CallTo(_fhirClient).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetPatientSummaryAsync_PatientNotFound_ReturnsNullPatientButStillOtherData()
    {
        A.CallTo(() => _fhirClient.GetPatientAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<PatientRecord?>(null));
        A.CallTo(() => _fhirClient.GetConditionsAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<ConditionRecord>>([]));
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<MedicationRecord>>([]));
        A.CallTo(() => _fhirClient.GetAllergiesAsync("default", "1", A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<AllergyRecord>>([]));

        var result = await _sut.GetPatientSummaryAsync(
            new GetPatientSummaryRequest { Site = "default", PatientId = "1" }, CancellationToken.None);

        result.Patient.Should().BeNull();
        result.ActiveProblems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPatientSummaryAsync_Always_PassesCancellationTokenToAllFhirCalls()
    {
        A.CallTo(() => _fhirClient.GetPatientAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<PatientRecord?>(null));
        A.CallTo(() => _fhirClient.GetConditionsAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<ConditionRecord>>([]));
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<MedicationRecord>>([]));
        A.CallTo(() => _fhirClient.GetAllergiesAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<AllergyRecord>>([]));
        using var cts = new CancellationTokenSource();

        await _sut.GetPatientSummaryAsync(new GetPatientSummaryRequest { Site = "default", PatientId = "1" }, cts.Token);

        A.CallTo(() => _fhirClient.GetPatientAsync("default", "1", cts.Token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fhirClient.GetConditionsAsync("default", "1", cts.Token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fhirClient.GetMedicationRequestsAsync("default", "1", cts.Token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fhirClient.GetAllergiesAsync("default", "1", cts.Token)).MustHaveHappenedOnceExactly();
    }
}
