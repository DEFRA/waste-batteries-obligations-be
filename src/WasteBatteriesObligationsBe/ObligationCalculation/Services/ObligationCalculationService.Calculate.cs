using FluentValidation;
using System.Globalization;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.Utils.Logging;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Services;

// One obligation version from the current POM submissions for a scheme.
// The target, window and denominator are copied from config onto that version.
public partial class ObligationCalculationService
{
    // Stored tonnes are rounded to 3 decimal places, rounded up if midpoint (e.g. 1.2345 then it is rounded to 1.235).
    // The obligation uses the unrounded annualised figures, then that same rounding.
    private const MidpointRounding TonnesRounding = MidpointRounding.AwayFromZero;

    private const int TonnesScale = 3;

    public async Task<ObligationCalculationModel> CalculateAsync(
        CalculationData data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        data = data.Trimmed();
        var result = await calculationDataValidator.ValidateAsync(data, cancellationToken);
        if (!result.IsValid)
        {
            // The future caller is event processing, which cannot correct the payload.
            logger.ObligationCalculationInputInvalid(ValidationErrors(result.Errors));
            throw new ValidationException(result.Errors);
        }

        var calculation = ToCalculation(data);
        var current = await repository.FindCurrentAsync(
            calculation.SchemeOrgId,
            calculation.CompliancePeriodYear,
            cancellationToken);

        return current is null
            ? await CreateAsync(calculation, cancellationToken)
            : await AddVersionAsync(calculation, cancellationToken);
    }

    // annualisedTonnes for the compliance year is tonnes × 4 ÷ quarters, or zero when the year is missing.
    // Earlier years keep their tonnes, however many quarters they have.
    // The obligation is that average, across the years that have a submission, multiplied by the target percentage (e.g. 45%) from config.
    private ObligationCalculationBase ToCalculation(CalculationData data)
    {
        var config = calculationOptions.Value;
        var complianceYear = data.CompliancePeriodYear;
        var placed = new List<PlacedOnMarketYear>(config.WindowYears);
        decimal unroundedAnnualisedTotal = 0;
        var yearsWithData = 0;

        for (var offset = config.WindowYears - 1; offset >= 0; offset--)
        {
            var year = complianceYear - offset;
            var submissions = data.Submissions
                .Where(submission => submission.CompliancePeriodYear == year)
                .ToList();
            var (entry, unroundedAnnualised, hasData) = PlaceYear(year, complianceYear, submissions);
            placed.Add(entry);
            if (hasData)
            {
                yearsWithData++;
                unroundedAnnualisedTotal += unroundedAnnualised;
            }
        }

        if (yearsWithData == 0)
        {
            throw new InvalidOperationException("A calculation needs a year with a submission.");
        }

        var target = decimal.Parse(config.TargetPercentage, NumberStyles.Number, CultureInfo.InvariantCulture);
        var obligation = unroundedAnnualisedTotal / yearsWithData * target / 100m;

        return new ObligationCalculationBase
        {
            SchemeOrgId = data.Submissions[0].SchemeOrgId,
            // TEMPORARY: Manage Account will supply the scheme name. Until that call exists, the snapshot is blank.
            SchemeName = string.Empty,
            CompliancePeriodYear = complianceYear,
            ObligationTonnes = FormatTonnes(obligation),
            PlacedOnMarketByYear = placed,
            Parameters = new ObligationParameters
            {
                Target = new ObligationTarget
                {
                    Type = ObligationTarget.Percentage,
                    Value = config.TargetPercentage
                },
                WindowYears = config.WindowYears,
                DenominatorRule = config.DenominatorRule
            },
            Trigger = data.Trigger
        };
    }

    private static (PlacedOnMarketYear Entry, decimal UnroundedAnnualised, bool HasData) PlaceYear(
        int year,
        int complianceYear,
        List<PomSubmissionForCalculation> submissions)
    {
        if (submissions.Count == 0)
        {
            return (new PlacedOnMarketYear
            {
                Year = year,
                Tonnes = "0.000",
                QuartersOfData = 0,
                AnnualisedTonnes = "0.000"
            }, 0m, false);
        }

        decimal tonnes = 0;
        foreach (var submission in submissions)
        {
            foreach (var value in submission.Tonnes)
            {
                tonnes += decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
            }
        }

        // Two members filing the same quarter are still one quarter of data.
        var quarters = submissions
            .Select(submission => submission.CompliancePeriodQuarter)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var unroundedAnnualised = year == complianceYear
            ? tonnes * 4m / quarters
            : tonnes;
        var storedTonnes = FormatTonnes(tonnes);
        var storedAnnualised = FormatTonnes(unroundedAnnualised);

        return (new PlacedOnMarketYear
        {
            Year = year,
            Tonnes = storedTonnes,
            QuartersOfData = quarters,
            AnnualisedTonnes = storedAnnualised,
            IsAnnualisedEstimate = !string.Equals(storedTonnes, storedAnnualised, StringComparison.Ordinal),
            SourceSubmissionIds = submissions.ConvertAll(submission => submission.Id)
        }, unroundedAnnualised, true);
    }

    private static string FormatTonnes(decimal value) =>
        decimal.Round(value, TonnesScale, TonnesRounding).ToString("0.000", CultureInfo.InvariantCulture);
}
