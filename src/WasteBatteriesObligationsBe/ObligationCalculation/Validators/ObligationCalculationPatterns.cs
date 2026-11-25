using System.Text.RegularExpressions;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Validators;

// Decimal strings on a calculation. Tonnes have 3 decimal places. A percentage target has 2.
public static partial class ObligationCalculationPatterns
{
    public const string Tonnes = @"^\d+\.\d{3}$";

    public const string TargetValue = @"^\d+\.\d{2}$";

    [GeneratedRegex(Tonnes, RegexOptions.CultureInvariant)]
    public static partial Regex TonnesPattern();

    [GeneratedRegex(TargetValue, RegexOptions.CultureInvariant)]
    public static partial Regex TargetValuePattern();
}
