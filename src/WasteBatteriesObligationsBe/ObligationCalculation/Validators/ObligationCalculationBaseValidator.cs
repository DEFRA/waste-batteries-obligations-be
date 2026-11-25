using FluentValidation;
using MongoDB.Bson;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Validators;

// Shape of a calculation the service is asked to store. Whether a current version must already
// exist is decided by the service, which reads that row once.
public sealed class ObligationCalculationBaseValidator : AbstractValidator<ObligationCalculationBase>
{
    public ObligationCalculationBaseValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Continue;
        RuleLevelCascadeMode = CascadeMode.Stop;

        Required(RuleFor(content => content.SchemeOrgId), ObligationCalculationValidationMessages.SchemeOrgIdRequired)
            .Must(BeAnObjectId)
            .WithMessage(ObligationCalculationValidationMessages.SchemeOrgIdObjectId);

        // TEMPORARY: schemeName may be blank. CalculateAsync stores an empty name until
        // Manage Account can supply it. A name is still accepted when the caller has one.

        RuleFor(content => content.CompliancePeriodYear)
            .InclusiveBetween(CalendarYear.Minimum, CalendarYear.Maximum)
            .WithMessage(ObligationCalculationValidationMessages.CompliancePeriodYearRange);

        Required(RuleFor(content => content.ObligationTonnes), ObligationCalculationValidationMessages.ObligationTonnesFormat)
            .Matches(ObligationCalculationPatterns.TonnesPattern())
            .WithMessage(ObligationCalculationValidationMessages.ObligationTonnesFormat);

        RuleFor(content => content.PlacedOnMarketByYear)
            .NotEmpty()
            .WithMessage(ObligationCalculationValidationMessages.PlacedOnMarketRequired);

        When(content => content.PlacedOnMarketByYear is not null, () =>
        {
            RuleFor(content => content.PlacedOnMarketByYear)
                .Must(YearsMatchWindow)
                .When(content => content.PlacedOnMarketByYear is { Count: > 0 }
                    && content.Parameters is not null
                    && content.Parameters.WindowYears > 0
                    && CalendarYear.Contains(content.CompliancePeriodYear))
                .WithMessage(content => ListItemValidation.PlacedOnMarket(content));

            RuleForEach(content => content.PlacedOnMarketByYear)
                .Where(year => year is not null)
                .Must(EarlierYearIsUnscaled)
                .When(content => CalendarYear.Contains(content.CompliancePeriodYear))
                .WithMessage(ObligationCalculationValidationMessages.EarlierYearScaled)
                .ForPlacedYear();

            RuleForEach(content => content.PlacedOnMarketByYear)
                .Where(year => year is null)
                .Must(_ => false)
                .WithMessage(ObligationCalculationValidationMessages.PlacedOnMarketYearRequired)
                .ForPlacedYear();

            RuleForEach(content => content.PlacedOnMarketByYear)
                .Where(year => year is not null)
                .Cascade(CascadeMode.Continue)
                .ChildRules(ValidateYear);
        });

        RuleFor(content => content.Parameters)
            .NotNull()
            .WithMessage(ObligationCalculationValidationMessages.ParametersRequired)
            .DependentRules(() =>
            {
                RuleFor(content => content.Parameters.Target)
                    .NotNull()
                    .WithMessage(ObligationCalculationValidationMessages.TargetRequired)
                    .DependentRules(() =>
                    {
                        RuleFor(content => content.Parameters.Target.Type)
                            .Equal(ObligationTarget.Percentage)
                            .WithMessage(ObligationCalculationValidationMessages.TargetType);

                        Required(RuleFor(content => content.Parameters.Target.Value), ObligationCalculationValidationMessages.TargetValueFormat)
                            .Matches(ObligationCalculationPatterns.TargetValuePattern())
                            .WithMessage(ObligationCalculationValidationMessages.TargetValueFormat);
                    });

                RuleFor(content => content.Parameters.WindowYears)
                    .GreaterThan(0)
                    .WithMessage(ObligationCalculationValidationMessages.WindowYearsRange);

                RuleFor(content => content.Parameters.DenominatorRule)
                    .Equal(ObligationParameters.YearsWithData)
                    .WithMessage(ObligationCalculationValidationMessages.DenominatorRule);
            });

        RuleFor(content => content.Trigger)
            .NotNull()
            .WithMessage(ObligationCalculationValidationMessages.TriggerRequired)
            .DependentRules(() =>
            {
                RuleFor(content => content.Trigger.Type)
                    .Must(type => type is ObligationTrigger.Event or ObligationTrigger.Manual)
                    .WithMessage(ObligationCalculationValidationMessages.TriggerType);

                When(content => content.Trigger.Type == ObligationTrigger.Event, () =>
                {
                    Required(RuleFor(content => content.Trigger.EventId), ObligationCalculationValidationMessages.EventIdRequired);
                    Required(RuleFor(content => content.Trigger.CorrelationId), ObligationCalculationValidationMessages.CorrelationIdRequired);
                    RuleFor(content => content.Trigger.Actor)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.ActorNotAllowed);
                    RuleFor(content => content.Trigger.Reason)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.ReasonNotAllowed);
                });

                When(content => content.Trigger.Type == ObligationTrigger.Manual, () =>
                {
                    Required(RuleFor(content => content.Trigger.Actor), ObligationCalculationValidationMessages.ActorRequired);
                    Required(RuleFor(content => content.Trigger.Reason), ObligationCalculationValidationMessages.ReasonRequired);
                    RuleFor(content => content.Trigger.EventId)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.EventIdNotAllowed);
                    RuleFor(content => content.Trigger.CorrelationId)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.CorrelationIdNotAllowed);
                });
            });
    }

    private static void ValidateYear(InlineValidator<PlacedOnMarketYear> year)
    {
        year.ClassLevelCascadeMode = CascadeMode.Continue;
        year.RuleLevelCascadeMode = CascadeMode.Stop;

        year.RuleFor(item => item.Year)
            .InclusiveBetween(CalendarYear.Minimum, CalendarYear.Maximum)
            .WithMessage(ObligationCalculationValidationMessages.YearRange)
            .ForPlacedYear();

        Required(year.RuleFor(item => item.Tonnes), ObligationCalculationValidationMessages.TonnesFormat)
            .Matches(ObligationCalculationPatterns.TonnesPattern())
            .WithMessage(ObligationCalculationValidationMessages.TonnesFormat)
            .ForPlacedYear();

        Required(year.RuleFor(item => item.AnnualisedTonnes), ObligationCalculationValidationMessages.AnnualisedTonnesFormat)
            .Matches(ObligationCalculationPatterns.TonnesPattern())
            .WithMessage(ObligationCalculationValidationMessages.AnnualisedTonnesFormat)
            .ForPlacedYear();

        year.RuleFor(item => item.QuartersOfData)
            .InclusiveBetween(0, 4)
            .WithMessage(ObligationCalculationValidationMessages.QuartersRange)
            .ForPlacedYear();

        year.RuleFor(item => item)
            .Must(AnnualisedFlagMatches)
            .When(item => TonnesMatch(item.Tonnes) && TonnesMatch(item.AnnualisedTonnes))
            .WithName("IsAnnualisedEstimate")
            .WithMessage(ObligationCalculationValidationMessages.AnnualisedEstimateMismatch)
            .ForPlacedYear();

        year.RuleFor(item => item)
            .Must(MissingYearIsBlank)
            .When(item => item.QuartersOfData == 0 && TonnesMatch(item.Tonnes) && TonnesMatch(item.AnnualisedTonnes))
            .WithName("PlacedOnMarketByYear")
            .WithMessage(ObligationCalculationValidationMessages.MissingYearBlank)
            .ForPlacedYear();

        year.RuleFor(item => item.SourceSubmissionIds)
            .NotNull()
            .WithMessage(ObligationCalculationValidationMessages.SourceSubmissionIdsRequired)
            .ForPlacedYear();

        year.When(item => item.SourceSubmissionIds is not null, () =>
        {
            year.RuleForEach(item => item.SourceSubmissionIds)
                .Must(BeAnObjectId)
                .WithMessage(ObligationCalculationValidationMessages.SourceSubmissionIdObjectId)
                .ForPlacedYear();
        });
    }

    private static IRuleBuilderOptions<T, string?> Required<T>(IRuleBuilder<T, string?> rule, string message) =>
        rule.Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(message);

    private static bool BeAnObjectId(string? value) => ObjectId.TryParse(value, out _);

    private static bool YearsMatchWindow(ObligationCalculationBase calculation, List<PlacedOnMarketYear> years)
    {
        if (calculation.Parameters is null || years.Count != calculation.Parameters.WindowYears)
        {
            return false;
        }

        var expected = new HashSet<int>();
        for (var offset = 0; offset < calculation.Parameters.WindowYears; offset++)
        {
            expected.Add(calculation.CompliancePeriodYear - offset);
        }

        // A repeated year is still one year of the window, so the sets would otherwise match.
        if (years.Any(year => year is null))
        {
            return false;
        }

        var actual = years.Select(year => year.Year).ToHashSet();
        return actual.Count == years.Count && actual.SetEquals(expected);
    }

    // Earlier years are not scaled. The compliance year is the only one that can be annualised.
    // A year whose tonnes are not yet 3 decimal places is left to the tonnes rules.
    private static bool EarlierYearIsUnscaled(ObligationCalculationBase calculation, PlacedOnMarketYear year)
    {
        if (year.Year == calculation.CompliancePeriodYear
            || !TonnesMatch(year.Tonnes)
            || !TonnesMatch(year.AnnualisedTonnes))
        {
            return true;
        }

        return !year.IsAnnualisedEstimate
            && string.Equals(year.Tonnes, year.AnnualisedTonnes, StringComparison.Ordinal);
    }

    // A missing year is stored as zero. It is not a zero return, which has quarters and counts in the average.
    private static bool MissingYearIsBlank(PlacedOnMarketYear year) =>
        year.Tonnes == "0.000"
        && year.AnnualisedTonnes == "0.000"
        && !year.IsAnnualisedEstimate
        && year.SourceSubmissionIds is { Count: 0 };

    private static bool AnnualisedFlagMatches(PlacedOnMarketYear year) =>
        year.IsAnnualisedEstimate != string.Equals(year.Tonnes, year.AnnualisedTonnes, StringComparison.Ordinal);

    private static bool TonnesMatch(string? value) =>
        ObligationCalculationPatterns.TonnesPattern().IsMatch(value ?? string.Empty);
}
