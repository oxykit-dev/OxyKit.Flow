using System.Text.Json;
using FluentValidation;
using OxyKit.Flow.Services;

namespace OxyKit.Flow.Features.Jobs;

public sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Render).NotNull().WithMessage("render is required.");
        RuleFor(x => x.Render!.Input)
            .NotEmpty()
            .When(x => x.Render is not null)
            .WithMessage("render.input is required.");
        RuleFor(x => x.Render!.Output)
            .NotEmpty()
            .When(x => x.Render is not null)
            .WithMessage("render.output is required.");
        RuleFor(x => x.Render!.Body)
            .Must(b => b.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
            .When(x => x.Render is not null)
            .WithMessage("render.body is required.");
        RuleFor(x => x.Outputs)
            .NotEmpty()
            .WithMessage("At least one output is required.");
        When(
            x => x.Outputs is not null,
            () =>
            {
                RuleForEach(x => x.Outputs)
                    .ChildRules(output =>
                    {
                        output
                            .RuleFor(o => o.TemplateId)
                            .Matches("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")
                            .When(o => !string.IsNullOrWhiteSpace(o.TemplateId))
                            .WithMessage(
                                "templateId must be a simple name (letters, digits, '-', '_')."
                            );
                        output
                            .RuleFor(o => o.Data)
                            .Must(d => d is { ValueKind: JsonValueKind.Object })
                            .When(o => !string.IsNullOrWhiteSpace(o.TemplateId))
                            .WithMessage("data must be a JSON object when templateId is set.");
                    });
            }
        );
        RuleFor(x => x.WebhookUrl)
            .Must(url => JobWebhookDispatcher.ValidateWebhookUrl(url) is null)
            .When(x => !string.IsNullOrWhiteSpace(x.WebhookUrl))
            .WithMessage("webhookUrl must be an absolute http or https URL.");
    }
}
