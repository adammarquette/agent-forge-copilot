using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MarqSpec.AgentForge.Llm;

namespace MarqSpec.AgentForge.UnitTests.Llm;

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
    public void Timeouts_Defaults_AreTunedForLlmSynthesis()
    {
        // Regression (issue #77): the framework's 10s/30s HTTP defaults are too tight for the heaviest
        // agenda synthesis prompt, forcing a deterministic-fallback degrade. These defaults give the LLM
        // call room to complete without over-extending the demo.
        var options = new LlmProviderOptions
        {
            ApiKey = "test-key",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
        };

        options.AttemptTimeoutSeconds.Should().Be(60);
        options.TotalRequestTimeoutSeconds.Should().Be(150);
    }

    [Fact]
    public void Validate_NonPositiveAttemptTimeout_ProducesError()
    {
        var options = new LlmProviderOptions
        {
            ApiKey = "test-key",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
            AttemptTimeoutSeconds = 0,
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(LlmProviderOptions.AttemptTimeoutSeconds)));
    }

    [Fact]
    public void Validate_TotalTimeoutBelowAttemptTimeout_ProducesError()
    {
        // The standard resilience handler requires the total budget to be >= a single attempt, or it
        // throws at startup - validate here so a misconfiguration fails fast with a clear message.
        var options = new LlmProviderOptions
        {
            ApiKey = "test-key",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
            AttemptTimeoutSeconds = 60,
            TotalRequestTimeoutSeconds = 30,
        };

        var results = Validate(options);

        results.Should().ContainSingle(r => r.MemberNames.Contains(nameof(LlmProviderOptions.TotalRequestTimeoutSeconds)));
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
