using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using GauntletAI.AgentForge.Mcp;

namespace GauntletAI.AgentForge.UnitTests.Mcp;

public sealed class McpToolContractTests
{
    [Fact]
    public void Validate_ValidRequest_DoesNotThrow()
    {
        var request = new ValidTestRequest { RequiredField = "value" };

        var act = () => McpToolContract.Validate("test_tool", request);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_InvalidRequest_ThrowsWithToolNameAndValidationErrors()
    {
        var request = new ValidTestRequest { RequiredField = string.Empty };

        var act = () => McpToolContract.Validate("test_tool", request);

        var exception = act.Should().Throw<McpToolContractException>().Which;
        exception.Message.Should().Contain("test_tool");
        exception.ValidationErrors.Should().NotBeEmpty();
    }

    private sealed class ValidTestRequest
    {
        [Required(AllowEmptyStrings = false)]
        public required string RequiredField { get; init; }
    }
}
