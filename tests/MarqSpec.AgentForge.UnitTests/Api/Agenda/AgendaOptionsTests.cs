using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MarqSpec.AgentForge.Api.Agenda;

namespace MarqSpec.AgentForge.UnitTests.Api.Agenda;

public sealed class AgendaOptionsTests
{
    [Fact]
    public void MaxConcurrentSummaries_Default_LimitsPeakOpenEmrContention()
    {
        // Regression (issue #80): the roster fan-out at 4 concurrent summaries overwhelmed the slow
        // staging OpenEMR, tripping FHIR timeouts. The lowered default eases peak contention while
        // still summarizing several patients at once.
        var options = new AgendaOptions();

        options.MaxConcurrentSummaries.Should().Be(3);
    }

    [Fact]
    public void Validate_PositiveConcurrency_ProducesNoErrors()
    {
        var options = new AgendaOptions { MaxConcurrentSummaries = 3 };

        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_NonPositiveConcurrency_ProducesError()
    {
        var options = new AgendaOptions { MaxConcurrentSummaries = 0 };

        Validate(options).Should().ContainSingle(r => r.MemberNames.Contains(nameof(AgendaOptions.MaxConcurrentSummaries)));
    }

    private static List<ValidationResult> Validate(AgendaOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }
}
