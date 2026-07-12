using FluentAssertions;
using GauntletAI.AgentForge.Api.Agenda;

namespace GauntletAI.AgentForge.UnitTests.Api.Agenda;

public sealed class AgendaRosterGateTests
{
    [Fact]
    public void Authorize_PatientIsInTheRoster_ReturnsTrue()
    {
        var roster = new HashSet<string> { "patient-1", "patient-2" };

        AgendaRosterGate.Authorize(roster, "patient-1").Should().BeTrue();
    }

    [Fact]
    public void Authorize_PatientIsNotInTheRoster_ReturnsFalse()
    {
        // The core guard: a client cannot request an arbitrary patientId outside what the agenda
        // itself already returned (ARCHITECTURE.md §19.1 step 5).
        var roster = new HashSet<string> { "patient-1", "patient-2" };

        AgendaRosterGate.Authorize(roster, "someone-elses-patient").Should().BeFalse();
    }

    [Fact]
    public void Authorize_EmptyRoster_ReturnsFalse() =>
        AgendaRosterGate.Authorize(new HashSet<string>(), "patient-1").Should().BeFalse();
}
