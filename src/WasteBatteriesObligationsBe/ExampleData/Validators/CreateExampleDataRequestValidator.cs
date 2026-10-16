using FluentValidation;
using WasteBatteriesObligationsBe.ExampleData.Models;

namespace WasteBatteriesObligationsBe.ExampleData.Validators;

public sealed class CreateExampleDataRequestValidator : AbstractValidator<CreateExampleDataRequest>
{
    public CreateExampleDataRequestValidator()
    {
        RuleFor(x => x.ExampleText).NotEmpty().MaximumLength(255);
    }
}
