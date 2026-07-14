using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Integration.OpenEmr.Standard;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.UnitTests.Integration.OpenEmr.Standard;

/// <summary>
/// Drives <see cref="DocumentReferenceResolver"/>: recover the just-written document's citation id by
/// diffing DocumentReference ids before/after the write. Exactly one new id resolves; zero or many resolve
/// to null (degrade, never guess); a FHIR failure degrades too. reference: agent-forge#43
/// </summary>
public sealed class DocumentReferenceResolverTests
{
    private static DocumentReferenceResolver CreateResolver(IOpenEmrFhirClient fhir) =>
        new(
            fhir,
            Options.Create(new OpenEmrOptions
            {
                BaseUrl = "https://emr.example/",
                Site = "default",
                ClientId = "cid",
                Scopes = [],
            }),
            A.Fake<ILogger<DocumentReferenceResolver>>());

    private static ClinicalDocumentRecord Doc(string id) =>
        new(new ClinicalSourceRef("DocumentReference", id), "Lab report", "current", null, null);

    private static void ArrangeDocuments(IOpenEmrFhirClient fhir, params ClinicalDocumentRecord[] docs) =>
        A.CallTo(() => fhir.GetDocumentReferencesAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(docs);

    [Fact]
    public async Task SnapshotAsync_ReturnsExistingReferenceIds()
    {
        var fhir = A.Fake<IOpenEmrFhirClient>();
        ArrangeDocuments(fhir, Doc("a"), Doc("b"));

        var snapshot = await CreateResolver(fhir).SnapshotAsync("p-1");

        snapshot.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public async Task SnapshotAsync_WhenNoDocuments_ReturnsEmpty()
    {
        var fhir = A.Fake<IOpenEmrFhirClient>();
        ArrangeDocuments(fhir);

        var snapshot = await CreateResolver(fhir).SnapshotAsync("p-1");

        snapshot.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveNewAsync_WhenExactlyOneNewAppeared_ReturnsIt()
    {
        var fhir = A.Fake<IOpenEmrFhirClient>();
        ArrangeDocuments(fhir, Doc("a"), Doc("b"));
        var before = new HashSet<string> { "a" };

        var resolved = await CreateResolver(fhir).ResolveNewAsync("p-1", before);

        resolved.Should().NotBeNull();
        resolved!.ResourceType.Should().Be("DocumentReference");
        resolved.Id.Should().Be("b");
    }

    [Fact]
    public async Task ResolveNewAsync_WhenNothingNew_ReturnsNull()
    {
        var fhir = A.Fake<IOpenEmrFhirClient>();
        ArrangeDocuments(fhir, Doc("a"), Doc("b"));
        var before = new HashSet<string> { "a", "b" };

        var resolved = await CreateResolver(fhir).ResolveNewAsync("p-1", before);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveNewAsync_WhenMultipleNew_ReturnsNull()
    {
        var fhir = A.Fake<IOpenEmrFhirClient>();
        ArrangeDocuments(fhir, Doc("a"), Doc("b"), Doc("c"));
        var before = new HashSet<string> { "a" };

        var resolved = await CreateResolver(fhir).ResolveNewAsync("p-1", before);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveNewAsync_WhenLookupFails_ReturnsNull()
    {
        var fhir = A.Fake<IOpenEmrFhirClient>();
        A.CallTo(() => fhir.GetDocumentReferencesAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Throws(new HttpRequestException("fhir down"));

        var resolved = await CreateResolver(fhir).ResolveNewAsync("p-1", new HashSet<string>());

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveNewAsync_WhenCancelled_Propagates()
    {
        var fhir = A.Fake<IOpenEmrFhirClient>();
        A.CallTo(() => fhir.GetDocumentReferencesAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        var act = () => CreateResolver(fhir).ResolveNewAsync("p-1", new HashSet<string>());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
