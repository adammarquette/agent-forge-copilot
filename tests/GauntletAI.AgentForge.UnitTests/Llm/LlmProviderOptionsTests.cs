using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using GauntletAI.AgentForge.Llm;

namespace GauntletAI.AgentForge.UnitTests.Llm;

public sealed class LlmProviderOptionsTests
{
    [Fact]
    public void Validate_AllRequiredFieldsPresentAndNonNegativePricing_ProducesNoErrors()
    {
        var options = new LlmProviderOptions
        {
            ApiKey = "test-key",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
        };

        var results = Validate(options);

        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DefaultBaseUrl_IsAnthropicApi()
    {
        var options = new LlmProviderOptions
        {
            ApiKey = "test-key",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
        };

        options.BaseUrl.Should().Be("https://api.anthropic.com");
    }

    [Fact]
    public void Validate_NegativeInputPrice_ProducesError()
    {
        var options = new LlmProviderOptions
        {
            ApiKey = "test-key",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = -1.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(LlmProviderOptions.InputPricePerMillionTokensUsd)));
    }

    [Fact]
    public void Validate_NegativeOutputPrice_ProducesError()
    {
        var options = new LlmProviderOptions
        {
            ApiKey = "test-key",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = -1.00m,
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(LlmProviderOptions.OutputPricePerMillionTokensUsd)));
    }

    [Fact]
    public void Validate_MissingApiKey_ProducesError()
    {
        var options = new LlmProviderOptions
        {
            ApiKey = string.Empty,
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(LlmProviderOptions.ApiKey)));
    }

    private static List<ValidationResult> Validate(LlmProviderOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
