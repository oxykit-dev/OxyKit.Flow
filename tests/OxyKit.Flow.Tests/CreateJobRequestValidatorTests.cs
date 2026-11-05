using System.Text.Json;
using FluentAssertions;
using OxyKit.Flow.Features.Jobs;
using OxyKit.Flow.Outputs;

namespace OxyKit.Flow.Tests;

public sealed class CreateJobRequestValidatorTests
{
    private readonly CreateJobRequestValidator _validator = new();

    [Fact]
    public void MailTemplate_RequiresObjectData()
    {
        var result = _validator.Validate(Sample(templateId: "welcome", data: null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MailTemplate_AcceptsObjectData()
    {
        var data = JsonSerializer.SerializeToElement(new { name = "Ada" });
        var result = _validator.Validate(Sample(templateId: "welcome", data: data));

        result.IsValid.Should().BeTrue();
    }

    private static CreateJobRequest Sample(string? templateId, JsonElement? data) =>
        new()
        {
            Name = "validator",
            Render = new RenderSpec
            {
                Input = "table",
                Output = "html",
                Body = JsonSerializer.SerializeToElement(new { sheets = Array.Empty<object>() }),
            },
            Outputs =
            [
                new DeliveryRequest
                {
                    Provider = "mail",
                    Destination = "ops@example.com",
                    TemplateId = templateId,
                    Data = data,
                },
            ],
        };
}
