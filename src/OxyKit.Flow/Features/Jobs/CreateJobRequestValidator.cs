using System.Text.Json;
using FluentValidation;

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
    }
}
