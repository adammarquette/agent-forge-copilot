using System.Globalization;
using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Agent;
using MarqSpec.AgentForge.Api.Agenda;
using MarqSpec.AgentForge.Api.Session;
using MarqSpec.AgentForge.Integration.OpenEmr.Fhir;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.UnitTests.Api.Agenda;

public sealed class AgendaRosterServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly IOpenEmrFhirClient _fhirClient = A.Fake<IOpenEmrFhirClient>();
    private readonly IAgendaPatientSummaryRunner _summaryRunner = A.Fake<IAgendaPatientSummaryRunner>();
    private readonly IScopedAccessTokenProvider _tokenProvider = A.Fake<IScopedAccessTokenProvider>();
    private readonly AgendaSessionContext _session = new("agenda-token", "default", "dr-jones");

    private AgendaRosterService BuildSut(int maxConcurrentSummaries = 4) => new(
        _fhirClient, _summaryRunner, _tokenProvider, new FixedTimeProvider(Now),
        Options.Create(new AgendaOptions { MaxConcurrentSummaries = maxConcurrentSummaries }),
        NullLogger<AgendaRosterService>.Instance);

    public AgendaRosterServiceTests()
    {
        A.CallTo(() => _summaryRunner.RunAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string site, string patientId, string _, CancellationToken _) =>
                Task.FromResult(new AgentTurnResult($"summary for {patientId}", ConversationState.Start(site, patientId), [], [])));
    }

    private static AppointmentRecord Appointment(string id, string patientId, DateTimeOffset? start, string status = "booked", string provider = "dr-jones") =>
        new(new ClinicalSourceRef("Appointment", id), patientId, $"Practitioner/{provider}", status, start);

    [Fact]
    public async Task BuildAgendaAsync_MultipleAppointmentsOutOfOrder_ReturnsRowsOrderedByScheduledStart()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(
            [
                Appointment("1", "patient-late", Now.AddHours(3)),
                Appointment("2", "patient-soon", Now.AddMinutes(30)),
                Appointment("3", "patient-mid", Now.AddHours(1)),
            ]));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.Rows.Select(r => r.PatientId).Should().Equal("patient-soon", "patient-mid", "patient-late");
    }

    [Fact]
    public async Task BuildAgendaAsync_AppointmentNotForThisProvider_ExcludesIt()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(
            [
                Appointment("1", "mine", Now.AddHours(1), provider: "dr-jones"),
                Appointment("2", "not-mine", Now.AddHours(1), provider: "dr-someone-else"),
            ]));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.Rows.Should().ContainSingle().Which.PatientId.Should().Be("mine");
    }

    [Fact]
    public async Task BuildAgendaAsync_AppointmentAtOrBeforeNow_ExcludesIt()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(
            [
                Appointment("1", "already-seen", Now.AddHours(-1)),
                Appointment("2", "right-now", Now),
                Appointment("3", "still-upcoming", Now.AddMinutes(1)),
            ]));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.Rows.Should().ContainSingle().Which.PatientId.Should().Be("still-upcoming");
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("noshow")]
    [InlineData("entered-in-error")]
    public async Task BuildAgendaAsync_CancelledNoShowOrEnteredInErrorAppointment_ExcludesIt(string status)
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(
                [Appointment("1", "patient-1", Now.AddHours(1), status: status)]));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAgendaAsync_AppointmentWithNoPatientId_ExcludesIt()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(
                [new AppointmentRecord(new ClinicalSourceRef("Appointment", "1"), null, "Practitioner/dr-jones", "booked", Now.AddHours(1))]));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAgendaAsync_OnePatientsSummaryThrows_OtherRowsStillSucceedAndTheFailedRowIsMarked()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(
            [
                Appointment("1", "patient-ok-1", Now.AddMinutes(10)),
                Appointment("2", "patient-fails", Now.AddMinutes(20)),
                Appointment("3", "patient-ok-2", Now.AddMinutes(30)),
            ]));
        A.CallTo(() => _summaryRunner.RunAsync("default", "patient-fails", "dr-jones", A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("tool dispatch failed"));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.Rows.Should().HaveCount(3);
        result.Rows.Should().Contain(r => r.PatientId == "patient-ok-1" && !r.Failed && r.Summary == "summary for patient-ok-1");
        result.Rows.Should().Contain(r => r.PatientId == "patient-ok-2" && !r.Failed && r.Summary == "summary for patient-ok-2");
        var failedRow = result.Rows.Single(r => r.PatientId == "patient-fails");
        failedRow.Failed.Should().BeTrue();
        failedRow.Summary.Should().BeNull();
        failedRow.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildAgendaAsync_Always_SetsTheAccessTokenOnceBeforeFanningOut()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(
                [Appointment("1", "patient-1", Now.AddMinutes(10))]));

        await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        _tokenProvider.AccessToken.Should().Be("agenda-token");
    }

    [Fact]
    public async Task BuildAgendaAsync_Always_ReturnsAsOfEqualToTheCapturedNow()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>([]));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.AsOf.Should().Be(Now);
    }

    [Fact]
    public async Task BuildAgendaAsync_MorePatientsThanTheConcurrencyBound_NeverRunsMoreThanTheBoundConcurrently()
    {
        const int concurrencyBound = 2;
        var current = 0;
        var maxObserved = 0;
        var gate = new Lock();
        A.CallTo(() => _summaryRunner.RunAsync(A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(async (string site, string patientId, string _, CancellationToken ct) =>
            {
                lock (gate)
                {
                    current++;
                    maxObserved = Math.Max(maxObserved, current);
                }

                await Task.Delay(50, ct);

                lock (gate)
                {
                    current--;
                }

                return new AgentTurnResult($"summary for {patientId}", ConversationState.Start(site, patientId), [], []);
            });
        var appointments = Enumerable.Range(1, 6)
            .Select(i => Appointment(i.ToString(CultureInfo.InvariantCulture), $"patient-{i.ToString(CultureInfo.InvariantCulture)}", Now.AddMinutes(i)))
            .ToArray();
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>(appointments));

        await BuildSut(concurrencyBound).BuildAgendaAsync(_session, CancellationToken.None);

        maxObserved.Should().BeLessThanOrEqualTo(concurrencyBound);
    }

    [Fact]
    public async Task BuildAgendaAsync_Always_ResolvesEachRowsPatientDisplayNameFromDemographics()
    {
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>([Appointment("1", "patient-1", Now.AddMinutes(30))]));
        A.CallTo(() => _fhirClient.GetPatientAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult<PatientRecord?>(new PatientRecord(new ClinicalSourceRef("Patient", "patient-1"), "Jane Roe", null, null)));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        result.Rows.Should().ContainSingle().Which.DisplayName.Should().Be("Jane Roe");
    }

    [Fact]
    public async Task BuildAgendaAsync_PatientDemographicsReadThrows_DisplayNameDegradesToNullWithoutFailingTheRow()
    {
        // A name lookup failure must not blank the whole row - the summary still shows, and the UI
        // falls back to "Patient <id>" for the missing name.
        A.CallTo(() => _fhirClient.GetAppointmentsAsync("default", "eq2026-07-11", A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<AppointmentRecord>>([Appointment("1", "patient-1", Now.AddMinutes(30))]));
        A.CallTo(() => _fhirClient.GetPatientAsync("default", "patient-1", A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("FHIR 500"));

        var result = await BuildSut().BuildAgendaAsync(_session, CancellationToken.None);

        var row = result.Rows.Should().ContainSingle().Subject;
        row.DisplayName.Should().BeNull();
        row.Failed.Should().BeFalse();
        row.Summary.Should().Be("summary for patient-1");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
