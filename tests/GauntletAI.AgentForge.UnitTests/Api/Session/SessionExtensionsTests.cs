using FluentAssertions;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.UnitTests.Api.Session;

public sealed class SessionExtensionsTests
{
    private readonly InMemoryTestSession _session = new();

    [Fact]
    public void SavePatientSession_ThenTryGetPatientSession_RoundTripsAllFields()
    {
        var context = new PatientSessionContext("token-abc", "default", "123", "dr-jones");

        _session.SavePatientSession(context);
        var result = _session.TryGetPatientSession();

        result.Should().Be(context);
    }

    [Fact]
    public void TryGetPatientSession_NothingSaved_ReturnsNull() =>
        _session.TryGetPatientSession().Should().BeNull();

    [Fact]
    public void TryGetPatientSession_PartiallyPopulatedSession_ReturnsNullRatherThanABrokenObject()
    {
        // Guards against ever handing a caller a PatientSessionContext with a missing field silently
        // defaulted (e.g. an empty PatientId) - the whole product is single-patient-scoped, so a
        // half-populated session must read as "not authenticated," never as authenticated-but-wrong.
        _session.SetString("patient-session.access-token", "token-abc");

        _session.TryGetPatientSession().Should().BeNull();
    }

    [Fact]
    public void TryGetPatientSession_MissingClinicianIdentity_ReturnsNullRatherThanAnUnattributableSession()
    {
        // FR-AUTH-4: every access must be attributable to who made it - a session missing the
        // clinician identity is just as invalid as one missing the patient id.
        _session.SetString("patient-session.access-token", "token-abc");
        _session.SetString("patient-session.site", "default");
        _session.SetString("patient-session.patient-id", "123");

        _session.TryGetPatientSession().Should().BeNull();
    }

    [Fact]
    public void ClearPatientSession_AfterSave_TryGetReturnsNullAfterward()
    {
        _session.SavePatientSession(new PatientSessionContext("token-abc", "default", "123", "dr-jones"));

        _session.ClearPatientSession();

        _session.TryGetPatientSession().Should().BeNull();
    }

    [Fact]
    public void SaveAgendaSession_ThenTryGetAgendaSession_RoundTripsAllFields()
    {
        var context = new AgendaSessionContext("token-abc", "default", "dr-jones");

        _session.SaveAgendaSession(context);
        var result = _session.TryGetAgendaSession();

        result.Should().Be(context);
    }

    [Fact]
    public void TryGetAgendaSession_NothingSaved_ReturnsNull() =>
        _session.TryGetAgendaSession().Should().BeNull();

    [Fact]
    public void TryGetAgendaSession_PartiallyPopulatedSession_ReturnsNullRatherThanABrokenObject()
    {
        _session.SetString("agenda-session.access-token", "token-abc");

        _session.TryGetAgendaSession().Should().BeNull();
    }

    [Fact]
    public void ClearAgendaSession_AfterSave_TryGetReturnsNullAfterward()
    {
        _session.SaveAgendaSession(new AgendaSessionContext("token-abc", "default", "dr-jones"));

        _session.ClearAgendaSession();

        _session.TryGetAgendaSession().Should().BeNull();
    }

    [Fact]
    public void SaveAgendaSession_AndSavePatientSession_DoNotCollideInTheSameUnderlyingSession()
    {
        // Distinct key prefixes: a clinician mid-flow on both a single-patient launch and an
        // agenda launch in the same browser session must not have one clobber the other.
        _session.SaveAgendaSession(new AgendaSessionContext("agenda-token", "default", "dr-jones"));
        _session.SavePatientSession(new PatientSessionContext("patient-token", "default", "123", "dr-jones"));

        _session.TryGetAgendaSession().Should().Be(new AgendaSessionContext("agenda-token", "default", "dr-jones"));
        _session.TryGetPatientSession().Should().Be(new PatientSessionContext("patient-token", "default", "123", "dr-jones"));
    }
}
