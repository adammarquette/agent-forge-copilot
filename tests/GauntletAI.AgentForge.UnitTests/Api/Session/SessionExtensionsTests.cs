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
        var context = new PatientSessionContext("token-abc", "default", "123");

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
    public void ClearPatientSession_AfterSave_TryGetReturnsNullAfterward()
    {
        _session.SavePatientSession(new PatientSessionContext("token-abc", "default", "123"));

        _session.ClearPatientSession();

        _session.TryGetPatientSession().Should().BeNull();
    }
}
