using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Fhir;

public sealed class OpenEmrFhirClientTests
{
    private const string PatientJson = """{ "resourceType": "Patient", "id": "1", "name": [ { "family": "Smith" } ] }""";
    private const string EmptyBundleJson = """{ "resourceType": "Bundle" }""";

    [Fact]
    public async Task GetPatientAsync_ValidPatient_ReturnsMappedRecord()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.ReadAsync("default", "Patient", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(PatientJson));
        var client = new OpenEmrFhirClient(api);

        var result = await client.GetPatientAsync("default", "1", CancellationToken.None);

        result!.Source.Citation.Should().Be("Patient/1");
    }

    [Fact]
    public async Task GetMedicationRequestsAsync_ValidBundle_ScopesSearchToPatientAndReturnsMappedRecords()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchMedicationRequestsAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        var result = await client.GetMedicationRequestsAsync("default", "1", CancellationToken.None);

        result.Should().BeEmpty();
        A.CallTo(() => api.SearchMedicationRequestsAsync("default", "1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetMedicationDispensesAsync_ValidBundle_ScopesSearchToPatient()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchMedicationDispensesAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetMedicationDispensesAsync("default", "1", CancellationToken.None);

        A.CallTo(() => api.SearchMedicationDispensesAsync("default", "1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetConditionsAsync_ValidBundle_ScopesSearchToPatient()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchConditionsAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetConditionsAsync("default", "1", CancellationToken.None);

        A.CallTo(() => api.SearchConditionsAsync("default", "1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetObservationsAsync_CategoryAndSinceProvided_PassesBothThrough()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchObservationsAsync("default", "1", "laboratory", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetObservationsAsync("default", "1", "laboratory", "ge2026-01-01", CancellationToken.None);

        A.CallTo(() => api.SearchObservationsAsync("default", "1", "laboratory", "ge2026-01-01", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetAllergiesAsync_ValidBundle_ScopesSearchToPatient()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchAllergyIntolerancesAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetAllergiesAsync("default", "1", CancellationToken.None);

        A.CallTo(() => api.SearchAllergyIntolerancesAsync("default", "1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetEncountersAsync_SinceProvided_PassesDateThrough()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchEncountersAsync("default", "1", "ge2026-01-01", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetEncountersAsync("default", "1", "ge2026-01-01", CancellationToken.None);

        A.CallTo(() => api.SearchEncountersAsync("default", "1", "ge2026-01-01", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetProceduresAsync_ValidBundle_ScopesSearchToPatient()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchProceduresAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetProceduresAsync("default", "1", CancellationToken.None);

        A.CallTo(() => api.SearchProceduresAsync("default", "1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetDiagnosticReportsAsync_ValidBundle_ScopesSearchToPatient()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchDiagnosticReportsAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetDiagnosticReportsAsync("default", "1", CancellationToken.None);

        A.CallTo(() => api.SearchDiagnosticReportsAsync("default", "1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetDocumentReferencesAsync_ValidBundle_ScopesSearchToPatient()
    {
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchDocumentReferencesAsync("default", "1", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        await client.GetDocumentReferencesAsync("default", "1", CancellationToken.None);

        A.CallTo(() => api.SearchDocumentReferencesAsync("default", "1", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetAppointmentsAsync_DateProvided_PassesDateThroughWithNoPatientScoping()
    {
        // Deliberately not patient-scoped (ARCHITECTURE.md §19.2a) - unlike every other method on
        // this client, there is no patientId parameter at all here.
        var api = A.Fake<IOpenEmrFhirApi>();
        A.CallTo(() => api.SearchAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult(EmptyBundleJson));
        var client = new OpenEmrFhirClient(api);

        var result = await client.GetAppointmentsAsync("default", "eq2026-07-11", CancellationToken.None);

        result.Should().BeEmpty();
        A.CallTo(() => api.SearchAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
