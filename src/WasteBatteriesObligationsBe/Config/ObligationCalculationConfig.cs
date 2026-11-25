using System.ComponentModel.DataAnnotations;

namespace WasteBatteriesObligationsBe.Config;

// Policy values copied onto a calculation. A later change does not alter a version already stored.
public sealed class ObligationCalculationConfig
{
    public const string SectionName = "ObligationCalculation";

    // "45.00" means 45%. Stored on the calculation with the percentage type.
    [Required]
    public string TargetPercentage { get; init; } = string.Empty;

    // The compliance year and the preceding years. Three is the statutory window.
    [Range(1, 100)]
    public int WindowYears { get; init; }

    // yearsWithData divides by the years in the window that have a submission.
    [Required]
    public string DenominatorRule { get; init; } = string.Empty;
}
