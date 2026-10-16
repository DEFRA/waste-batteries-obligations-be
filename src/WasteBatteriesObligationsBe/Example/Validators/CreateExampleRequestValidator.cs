using FluentValidation;
using WasteBatteriesObligationsBe.Example.Models;

namespace WasteBatteriesObligationsBe.Example.Validators;

public sealed class CreateExampleRequestValidator : AbstractValidator<CreateExampleRequest>
{
    public CreateExampleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Value).NotEmpty();
        RuleFor(x => x.Counter).GreaterThanOrEqualTo(0);
    }
}
