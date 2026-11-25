using FluentValidation;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Validators;

// Shape of the submissions a calculation is asked to use. The window length comes from config,
// so a window of 3 accepts the compliance year and the two preceding years.
// The service trims the data before this runs.
public sealed class CalculationDataValidator : AbstractValidator<CalculationData>
{
    private readonly int _windowYears;

    public CalculationDataValidator(IOptions<ObligationCalculationConfig> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _windowYears = options.Value.WindowYears;

        ClassLevelCascadeMode = CascadeMode.Continue;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(data => data.CompliancePeriodYear)
            .InclusiveBetween(CalendarYear.Minimum, CalendarYear.Maximum)
            .WithMessage(CalculationDataValidationMessages.CompliancePeriodYearRange);

        RuleFor(data => data.CompliancePeriodYear)
            .Must(WindowFits)
            .When(data => CalendarYear.Contains(data.CompliancePeriodYear))
            .WithMessage(CalculationDataValidationMessages.WindowOutsideCalendar);

        RuleFor(data => data.Submissions)
            .NotEmpty()
            .WithMessage(CalculationDataValidationMessages.SubmissionsRequired);

        When(data => data.Submissions is not null, () =>
        {
            RuleFor(data => data.Submissions)
                .Must(AllSameScheme)
                .When(data => data.Submissions is { Count: > 0 })
                .WithMessage(data => ListItemValidation.SchemeMismatch(data.Submissions));

            RuleFor(data => data.Submissions)
                .Must(IdsAreUnique)
                .When(data => data.Submissions is { Count: > 0 })
                .WithMessage(data => ListItemValidation.SubmissionRepeated(data.Submissions));

            RuleForEach(data => data.Submissions)
                .Where(submission => submission is null)
                .Must(_ => false)
                .WithMessage(CalculationDataValidationMessages.SubmissionRequired)
                .ForSubmission();

            RuleForEach(data => data.Submissions)
                .Where(submission => submission is not null)
                .Must((data, submission) => YearIsInWindow(data.CompliancePeriodYear, submission.CompliancePeriodYear))
                .When(data => WindowFits(data.CompliancePeriodYear))
                .WithMessage(CalculationDataValidationMessages.SubmissionYearOutsideWindow)
                .ForSubmission();

            RuleForEach(data => data.Submissions)
                .Where(submission => submission is not null)
                .Cascade(CascadeMode.Continue)
                .ChildRules(ValidateSubmission);
        });

        RuleFor(data => data.Trigger)
            .NotNull()
            .WithMessage(ObligationCalculationValidationMessages.TriggerRequired)
            .DependentRules(() =>
            {
                RuleFor(data => data.Trigger.Type)
                    .Must(type => type is ObligationTrigger.Event or ObligationTrigger.Manual)
                    .WithMessage(ObligationCalculationValidationMessages.TriggerType);

                When(data => data.Trigger.Type == ObligationTrigger.Event, () =>
                {
                    Required(RuleFor(data => data.Trigger.EventId), ObligationCalculationValidationMessages.EventIdRequired);
                    Required(RuleFor(data => data.Trigger.CorrelationId), ObligationCalculationValidationMessages.CorrelationIdRequired);
                    RuleFor(data => data.Trigger.Actor)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.ActorNotAllowed);
                    RuleFor(data => data.Trigger.Reason)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.ReasonNotAllowed);
                });

                When(data => data.Trigger.Type == ObligationTrigger.Manual, () =>
                {
                    Required(RuleFor(data => data.Trigger.Actor), ObligationCalculationValidationMessages.ActorRequired);
                    Required(RuleFor(data => data.Trigger.Reason), ObligationCalculationValidationMessages.ReasonRequired);
                    RuleFor(data => data.Trigger.EventId)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.EventIdNotAllowed);
                    RuleFor(data => data.Trigger.CorrelationId)
                        .Must(string.IsNullOrEmpty)
                        .WithMessage(ObligationCalculationValidationMessages.CorrelationIdNotAllowed);
                });
            });
    }

    private static void ValidateSubmission(InlineValidator<PomSubmissionForCalculation> submission)
    {
        submission.ClassLevelCascadeMode = CascadeMode.Continue;
        submission.RuleLevelCascadeMode = CascadeMode.Stop;

        Required(submission.RuleFor(item => item.Id), CalculationDataValidationMessages.SubmissionIdRequired)
            .Must(BeAnObjectId)
            .WithMessage(CalculationDataValidationMessages.SubmissionIdObjectId)
            .ForSubmission();

        Required(submission.RuleFor(item => item.SchemeOrgId), CalculationDataValidationMessages.SchemeOrgIdRequired)
            .Must(BeAnObjectId)
            .WithMessage(CalculationDataValidationMessages.SchemeOrgIdObjectId)
            .ForSubmission();

        Required(submission.RuleFor(item => item.CompliancePeriodQuarter), CalculationDataValidationMessages.QuarterRequired)
            .Must(quarter => PomSubmissionForCalculation.Quarters.Contains(quarter, StringComparer.Ordinal))
            .WithMessage(CalculationDataValidationMessages.QuarterUnknown)
            .ForSubmission();

        submission.RuleFor(item => item.Tonnes)
            .NotEmpty()
            .WithMessage(CalculationDataValidationMessages.TonnesRequired)
            .ForSubmission();

        submission.When(item => item.Tonnes is not null, () =>
        {
            submission.RuleForEach(item => item.Tonnes)
                .Must(tonnes => ObligationCalculationPatterns.TonnesPattern().IsMatch(tonnes ?? string.Empty))
                .WithMessage(CalculationDataValidationMessages.TonnesFormat)
                .ForSubmission();
        });
    }

    private static IRuleBuilderOptions<T, string?> Required<T>(IRuleBuilder<T, string?> rule, string message) =>
        rule.Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage(message);

    private static bool BeAnObjectId(string? value) => ObjectId.TryParse(value, out _);

    private bool WindowFits(int complianceYear)
    {
        if (!CalendarYear.Contains(complianceYear) || _windowYears < 1)
        {
            return false;
        }

        var oldest = complianceYear - (long)_windowYears + 1;
        return oldest >= CalendarYear.Minimum;
    }

    // A window of 3 accepts the compliance year, the year before, and the year before that.
    private bool YearIsInWindow(int complianceYear, int submissionYear)
    {
        var oldest = complianceYear - (long)_windowYears + 1;
        return submissionYear <= complianceYear && submissionYear >= oldest;
    }

    private static bool AllSameScheme(IReadOnlyList<PomSubmissionForCalculation> submissions)
    {
        string? schemeOrgId = null;
        foreach (var submission in submissions)
        {
            if (submission is null)
            {
                continue;
            }

            if (schemeOrgId is null)
            {
                schemeOrgId = submission.SchemeOrgId;
                continue;
            }

            if (!string.Equals(schemeOrgId, submission.SchemeOrgId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return schemeOrgId is not null;
    }

    private static bool IdsAreUnique(IReadOnlyList<PomSubmissionForCalculation> submissions)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var submission in submissions)
        {
            if (submission is null)
            {
                continue;
            }

            if (!seen.Add(submission.Id))
            {
                return false;
            }
        }

        return true;
    }
}
