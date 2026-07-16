using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Agent;
using GauntletAI.AgentForge.Agents;

namespace GauntletAI.AgentForge.UnitTests.Agent;

public sealed class EvidenceToolTests
{
    private readonly IEvidenceRetriever _retriever = A.Fake<IEvidenceRetriever>();
    private readonly EvidenceTool _sut;

    public EvidenceToolTests() => _sut = new EvidenceTool(_retriever);

    [Fact]
    public async Task GetAsync_SnippetsFound_ProjectsThemToCitableGuidelineRecords()
    {
        var snippets = new List<EvidenceSnippet>
        {
            new() { DocumentId = "anticoag-2026", Section = "Warfarin INR target", ChunkId = "chunk-1", Text = "Target INR 2.0 to 3.0.", Score = 0.9 },
        };
        A.CallTo(() => _retriever.RetrieveAsync("inr target", A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<EvidenceSnippet>>(snippets));

        var result = await _sut.GetAsync("inr target", CancellationToken.None);

        result.Snippets.Should().ContainSingle();
        result.Snippets[0].ResourceType.Should().Be("Guideline");
        result.Snippets[0].Id.Should().Be("chunk-1");
        result.Snippets[0].DocumentId.Should().Be("anticoag-2026");
        result.Snippets[0].Section.Should().Be("Warfarin INR target");
        result.Snippets[0].Text.Should().Be("Target INR 2.0 to 3.0.");
    }

    [Fact]
    public async Task GetAsync_NoSnippets_ReturnsEmptyResult()
    {
        A.CallTo(() => _retriever.RetrieveAsync(A<string>._, A<int>._, A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<EvidenceSnippet>>([]));

        var result = await _sut.GetAsync("nothing on file", CancellationToken.None);

        result.Snippets.Should().BeEmpty();
    }
}
