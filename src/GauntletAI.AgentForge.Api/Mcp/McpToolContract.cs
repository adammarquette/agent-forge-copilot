using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// Validates a tool request against its DataAnnotations contract before any FHIR call is made
/// (NFR-CONTRACT-1 - invalid input rejected at the contract boundary with a structured error).
/// </summary>
public static class McpToolContract
{
    /// <summary>Validates <paramref name="request"/>, throwing <see cref="McpToolContractException"/> if invalid.</summary>
    public static void Validate<TRequest>(string toolName, TRequest request)
        where TRequest : notnull
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, context, results, validateAllProperties: true))
        {
            throw new McpToolContractException(
                toolName,
                [.. results.Select(r => r.ErrorMessage ?? "Invalid value.")]);
        }
    }
}
