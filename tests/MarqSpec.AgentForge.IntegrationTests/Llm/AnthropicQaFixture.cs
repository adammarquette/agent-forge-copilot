using MarqSpec.AgentForge.Llm;
using MarqSpec.AgentForge.Llm.Anthropic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Refit;

namespace MarqSpec.AgentForge.IntegrationTests.Llm;

/// <summary>
/// Shared fixture wiring the real <see cref="AnthropicLlmProvider"/> against the actual Anthropic
/// API. Nothing here is mocked (tests/AGENTS.md) - configured from environment variables only,
/// never a hard-coded key.
/// </summary>
public sealed class AnthropicQaFixture
{
    /// <summary>Environment variable prefix: LlmQa__ApiKey, LlmQa__Model, etc.</summary>
    public const string SectionName = "LlmQa";

    /// <summary>The real provider, ready to call.</summary>
    public ILlmProvider Provider { get; }

    /// <summary>The resolved options, for tests that need the raw values (e.g. the invalid-key test).</summary>
    public LlmProviderOptions Options { get; }

    public AnthropicQaFixture()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var section = configuration.GetSection(SectionName);

        var apiKey = section["ApiKey"];
        var model = section["Model"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                $"LLM integration tests require {SectionName}__ApiKey and {SectionName}__Model " +
                "environment variables. These tests exercise the real Anthropic API, not mocks " +
                "(ENGINEERING_STANDARDS.md §8.2 / tests/AGENTS.md) - there is no mock fallback to fail over to.");
        }

        Options = new LlmProviderOptions
        {
            ApiKey = apiKey,
            Model = model,
            InputPricePerMillionTokensUsd = ParseDecimalOrZero(section["InputPricePerMillionTokensUsd"]),
            OutputPricePerMillionTokensUsd = ParseDecimalOrZero(section["OutputPricePerMillionTokensUsd"]),
        };

        Provider = BuildProvider(Options);
    }

    /// <summary>
    /// Builds a provider against a deliberately-invalid key, for the authentication-boundary
    /// test - independent of whether <see cref="Options"/>' key is itself valid.
    /// </summary>
    public static ILlmProvider BuildProviderWithInvalidKey(string model) => BuildProvider(new LlmProviderOptions
    {
        ApiKey = "sk-ant-invalid-key-for-testing",
        Model = model,
        InputPricePerMillionTokensUsd = 0,
        OutputPricePerMillionTokensUsd = 0,
    });

    // Intentionally returns the abstraction, not AnthropicLlmProvider: these tests exercise
    // ILlmProvider, the same seam production code depends on (ARCHITECTURE.md D12).
#pragma warning disable CA1859
    private static ILlmProvider BuildProvider(LlmProviderOptions options)
    {
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);
        var authHandler = new AnthropicAuthHandler(optionsWrapper)
        {
            InnerHandler = new HttpClientHandler(),
        };
        var httpClient = new HttpClient(authHandler) { BaseAddress = new Uri(options.BaseUrl) };
        var api = RestService.For<IAnthropicMessagesApi>(httpClient);

        return new AnthropicLlmProvider(api, optionsWrapper);
    }
#pragma warning restore CA1859

    private static decimal ParseDecimalOrZero(string? value) =>
        decimal.TryParse(value, out var parsed) ? parsed : 0m;
}
