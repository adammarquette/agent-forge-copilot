using FluentAssertions;
using GauntletAI.AgentForge.Mcp;

namespace GauntletAI.AgentForge.UnitTests.Mcp;

public sealed class McpDateFilterTests
{
    [Theory]
    [InlineData("ge2026-01-01", 2026, 1, 1)]
    [InlineData("gt2025-12-31", 2025, 12, 31)]
    [InlineData("le2026-06-15", 2026, 6, 15)]
    public void ExtractDate_ValidFilter_ReturnsUtcDateFromTheDatePortion(string filter, int year, int month, int day)
    {
        var result = McpDateFilter.ExtractDate(filter);

        result.Should().Be(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero));
    }
}
