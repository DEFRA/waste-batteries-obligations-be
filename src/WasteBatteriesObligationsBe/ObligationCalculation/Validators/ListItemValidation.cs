using System.Globalization;
using FluentValidation;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Validators;

// A list failure names the index and the item key. The field text alone does not say which
// submission or year was rejected.
internal static class ListItemValidation
{
    public static IRuleBuilderOptions<CalculationData, PomSubmissionForCalculation> ForSubmission(
        this IRuleBuilderOptions<CalculationData, PomSubmissionForCalculation> rule) =>
        rule.IdentifiedBy(context =>
        {
            var index = IndexOf(context.PropertyName, "Submissions");
            return $"{Submission(index, context.PropertyValue)}: {context.GetDefaultMessage()}";
        });

    public static IRuleBuilderOptions<PomSubmissionForCalculation, TProperty> ForSubmission<TProperty>(
        this IRuleBuilderOptions<PomSubmissionForCalculation, TProperty> rule) =>
        rule.IdentifiedBy(context =>
        {
            var index = IndexOf(context.PropertyName, "Submissions");
            var where = Submission(index, context.InstanceToValidate);
            var tonnes = NestedIndex(context.PropertyName, "Tonnes");
            if (tonnes is not null)
            {
                where = $"{where}, tonnes[{tonnes}]";
            }

            return $"{where}: {context.GetDefaultMessage()}";
        });

    public static IRuleBuilderOptions<ObligationCalculationBase, PlacedOnMarketYear> ForPlacedYear(
        this IRuleBuilderOptions<ObligationCalculationBase, PlacedOnMarketYear> rule) =>
        rule.IdentifiedBy(context =>
        {
            var index = IndexOf(context.PropertyName, "PlacedOnMarketByYear");
            return $"{PlacedYear(index, context.InstanceToValidate, context.PropertyValue)}: {context.GetDefaultMessage()}";
        });

    public static IRuleBuilderOptions<PlacedOnMarketYear, TProperty> ForPlacedYear<TProperty>(
        this IRuleBuilderOptions<PlacedOnMarketYear, TProperty> rule) =>
        rule.IdentifiedBy(context =>
        {
            var calculation = CalculationOf(context.ParentContext);
            var index = IndexOf(context.PropertyName, "PlacedOnMarketByYear");
            var where = PlacedYear(index, calculation, context.InstanceToValidate);
            var source = NestedIndex(context.PropertyName, "SourceSubmissionIds");
            if (source is not null)
            {
                where = $"{where}, sourceSubmissionIds[{source}]";
            }

            return $"{where}: {context.GetDefaultMessage()}";
        });

    public static string SchemeMismatch(IReadOnlyList<PomSubmissionForCalculation> submissions)
    {
        var described = new List<string>();
        for (var index = 0; index < submissions.Count; index++)
        {
            var submission = submissions[index];
            if (submission is null)
            {
                continue;
            }

            described.Add($"{Submission(index, submission)} has schemeOrgId {Shown(submission.SchemeOrgId)}");
        }

        return $"{CalculationDataValidationMessages.SchemeMismatch} {string.Join("; ", described)}.";
    }

    public static string SubmissionRepeated(IReadOnlyList<PomSubmissionForCalculation> submissions)
    {
        var indexesById = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var index = 0; index < submissions.Count; index++)
        {
            var submission = submissions[index];
            if (submission is null)
            {
                continue;
            }

            var id = submission.Id ?? string.Empty;
            if (!indexesById.TryGetValue(id, out var indexes))
            {
                indexes = [];
                indexesById[id] = indexes;
            }

            indexes.Add(index);
        }

        var repeated = indexesById
            .Where(pair => pair.Value.Count > 1)
            .Select(pair =>
                $"id {Shown(pair.Key)} at {string.Join(", ", pair.Value.Select(index => $"submissions[{index}]"))}");

        return $"{CalculationDataValidationMessages.SubmissionRepeated} {string.Join("; ", repeated)}.";
    }

    public static string PlacedOnMarket(ObligationCalculationBase calculation)
    {
        var expected = new List<int>(calculation.Parameters.WindowYears);
        for (var offset = calculation.Parameters.WindowYears - 1; offset >= 0; offset--)
        {
            expected.Add(calculation.CompliancePeriodYear - offset);
        }

        var entries = calculation.PlacedOnMarketByYear.Select((year, index) =>
            year is null ? $"[{index}] missing" : $"[{index}] year {year.Year}");

        return $"{ObligationCalculationValidationMessages.PlacedOnMarketRequired} "
            + $"Expected {string.Join(", ", expected)}. "
            + $"Entries: {string.Join(", ", entries)} "
            + $"(schemeOrgId {Shown(calculation.SchemeOrgId)}, compliancePeriodYear {calculation.CompliancePeriodYear}).";
    }

    private static IRuleBuilderOptions<T, TProperty> IdentifiedBy<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        Func<FluentValidation.Internal.IMessageBuilderContext<T, TProperty>, string> identify)
    {
        DefaultValidatorOptions.Configurable(rule).MessageBuilder = identify;
        return rule;
    }

    private static string Submission(int index, string? id) => $"submissions[{index}] (id {Shown(id)})";

    private static string Submission(int index, PomSubmissionForCalculation? submission) =>
        submission is null ? $"submissions[{index}]" : Submission(index, submission.Id);

    private static string PlacedYear(int index, ObligationCalculationBase calculation, PlacedOnMarketYear? year)
    {
        var scheme = $"schemeOrgId {Shown(calculation.SchemeOrgId)}, compliancePeriodYear {calculation.CompliancePeriodYear}";
        var key = year is null ? scheme : $"{scheme}, year {year.Year}";
        return $"placedOnMarketByYear[{index}] ({key})";
    }

    private static ObligationCalculationBase CalculationOf(IValidationContext context)
    {
        for (var current = context; current is not null; current = current.ParentContext)
        {
            if (current.InstanceToValidate is ObligationCalculationBase calculation)
            {
                return calculation;
            }
        }

        throw new InvalidOperationException("A placed-on-market year is validated with its calculation.");
    }

    private static int IndexOf(string propertyPath, string collectionName)
    {
        var index = NestedIndex(propertyPath, collectionName) ?? throw new InvalidOperationException($"'{propertyPath}' does not include {collectionName}.");
        return index;
    }

    private static int? NestedIndex(string propertyPath, string collectionName)
    {
        var token = collectionName + "[";
        var start = propertyPath.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += token.Length;
        var end = propertyPath.IndexOf(']', start);
        return int.Parse(propertyPath.AsSpan(start, end - start), CultureInfo.InvariantCulture);
    }

    private static string Shown(string? value) => string.IsNullOrWhiteSpace(value) ? "missing" : value;
}
