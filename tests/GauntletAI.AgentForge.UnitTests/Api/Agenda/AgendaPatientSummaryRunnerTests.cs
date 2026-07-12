using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Api.Agenda;
using GauntletAI.AgentForge.Api.Session;
using GauntletAI.AgentForge.Verification;
using Microsoft.Extensions.DependencyInjection;

namespace GauntletAI.AgentForge.UnitTests.Api.Agenda;

public sealed class AgendaPatientSummaryRunnerTests
{
    [Fact]
    public async Task RunAsync_Always_SetsClinicianIdentityInItsOwnScopeBeforeInvokingTheOrchestrator()
    {
        // The access token flows correctly across a new DI scope on its own (it's a static
        // AsyncLocal, not a per-scope value - ScopedAccessTokenProvider's own doc comment), but
        // IScopedClinicianIdentityAccessor is a genuine per-DI-scope value (ScopedClinicianIdentityAccessor
        // is a plain property, no AsyncLocal). A runner that creates a fresh scope per patient but
        // forgets to set the identity inside that new scope would silently break FR-AUTH-4 audit
        // attribution for every agenda summary - this test guards exactly that.
        var clinicianIdentityAccessor = new ScopedClinicianIdentityAccessor();
        string? identityAtOrchestratorCallTime = null;
        var orchestrator = A.Fake<IAgentOrchestrator>();
        A.CallTo(() => orchestrator.StartAgendaSummaryAsync("default", "patient-1", A<CancellationToken>._))
            .Invokes(() => identityAtOrchestratorCallTime = clinicianIdentityAccessor.ClinicianIdentity)
            .Returns(Task.FromResult(new AgentTurnResult(
                "summary", ConversationState.Start("default", "patient-1"), [], [])));

        var services = new ServiceCollection();
        services.AddScoped<IAgentOrchestrator>(_ => orchestrator);
        services.AddScoped<IScopedClinicianIdentityAccessor>(_ => clinicianIdentityAccessor);
        using var provider = services.BuildServiceProvider();
        var sut = new AgendaPatientSummaryRunner(provider.GetRequiredService<IServiceScopeFactory>());

        await sut.RunAsync("default", "patient-1", "dr-jones", CancellationToken.None);

        identityAtOrchestratorCallTime.Should().Be("dr-jones");
    }

    [Fact]
    public async Task RunAsync_OrchestratorSucceeds_ReturnsItsResultUnchanged()
    {
        var expected = new AgentTurnResult(
            "Stable, no changes.",
            ConversationState.Start("default", "patient-1"),
            [new DomainConstraintFlag("rule-1", "flagged", [])],
            []);
        var orchestrator = A.Fake<IAgentOrchestrator>();
        A.CallTo(() => orchestrator.StartAgendaSummaryAsync("default", "patient-1", A<CancellationToken>._))
            .Returns(Task.FromResult(expected));

        var services = new ServiceCollection();
        services.AddScoped<IAgentOrchestrator>(_ => orchestrator);
        services.AddScoped<IScopedClinicianIdentityAccessor, ScopedClinicianIdentityAccessor>();
        using var provider = services.BuildServiceProvider();
        var sut = new AgendaPatientSummaryRunner(provider.GetRequiredService<IServiceScopeFactory>());

        var result = await sut.RunAsync("default", "patient-1", "dr-jones", CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task RunAsync_CalledTwiceForDifferentPatients_EachCallGetsTheRightIdentityDespiteASharedScopedAccessorType()
    {
        // A fresh DI scope per call is the point: each call must observe only its own
        // clinician-identity value, never a value left over from a previous call's scope.
        var identitiesSeen = new List<string?>();
        var orchestrator = A.Fake<IAgentOrchestrator>();
        A.CallTo(() => orchestrator.StartAgendaSummaryAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string site, string patientId, CancellationToken _) =>
                Task.FromResult(new AgentTurnResult("summary", ConversationState.Start(site, patientId), [], [])));

        var services = new ServiceCollection();
        services.AddScoped<IAgentOrchestrator>(sp =>
        {
            identitiesSeen.Add(sp.GetRequiredService<IScopedClinicianIdentityAccessor>().ClinicianIdentity);
            return orchestrator;
        });
        services.AddScoped<IScopedClinicianIdentityAccessor, ScopedClinicianIdentityAccessor>();
        using var provider = services.BuildServiceProvider();
        var sut = new AgendaPatientSummaryRunner(provider.GetRequiredService<IServiceScopeFactory>());

        await sut.RunAsync("default", "patient-1", "dr-jones", CancellationToken.None);
        await sut.RunAsync("default", "patient-2", "dr-smith", CancellationToken.None);

        identitiesSeen.Should().Equal("dr-jones", "dr-smith");
    }
}
