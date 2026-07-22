using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Auth;

public sealed class PkceGeneratorTests
{
    [Fact]
    public void Generate_Default_ProducesVerifierWithinRfc7636LengthBounds()
    {
        var challenge = PkceGenerator.Generate();

        challenge.CodeVerifier.Length.Should().BeInRange(43, 128);
    }

    [Fact]
    public void Generate_Default_VerifierContainsOnlyRfc7636UnreservedCharacters()
    {
        var challenge = PkceGenerator.Generate();

        challenge.CodeVerifier.Should().MatchRegex("^[A-Za-z0-9\\-._~]+$");
    }

    [Fact]
    public void Generate_Default_ChallengeMethodIsS256()
    {
        var challenge = PkceGenerator.Generate();

        challenge.CodeChallengeMethod.Should().Be("S256");
    }

    [Fact]
    public void Generate_Default_ChallengeIsSha256Base64UrlOfVerifier()
    {
        var challenge = PkceGenerator.Generate();

        var expectedHash = SHA256.HashData(Encoding.ASCII.GetBytes(challenge.CodeVerifier));
        var expectedChallenge = Convert.ToBase64String(expectedHash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        challenge.CodeChallenge.Should().Be(expectedChallenge);
    }

    [Fact]
    public void Generate_CalledTwice_ProducesDifferentVerifiers()
    {
        var first = PkceGenerator.Generate();
        var second = PkceGenerator.Generate();

        first.CodeVerifier.Should().NotBe(second.CodeVerifier);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(129)]
    public void Generate_VerifierLengthOutsideRfc7636Bounds_Throws(int verifierLength)
    {
        var act = () => PkceGenerator.Generate(verifierLength);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
