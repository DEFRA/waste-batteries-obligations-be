using System.Text.Json.Serialization;
using WasteBatteriesObligationsBe.Utils.Mongo;
using WasteBatteriesObligationsBe.Utils.Text;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Models;

// The part of a POM submission the obligation needs. Chemistry and member details are not used:
// the figure is the scheme total, and a quarter counts once anything has been submitted for it.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PomSubmissionForCalculation
{
    public const string Quarter1 = "Quarter 1";

    public const string Quarter2 = "Quarter 2";

    public const string Quarter3 = "Quarter 3";

    public const string Quarter4 = "Quarter 4";

    public static readonly string[] Quarters = [Quarter1, Quarter2, Quarter3, Quarter4];

    public string Id { get; init; } = string.Empty;

    public string SchemeOrgId { get; init; } = string.Empty;

    public int CompliancePeriodYear { get; init; }

    // One of Quarter 1 to Quarter 4. Distinct values in a year are quartersOfData.
    public string CompliancePeriodQuarter { get; init; } = string.Empty;

    // One string per tonnage entry, for example "12.500". The calculation sums them.
    public List<string> Tonnes { get; init; } = [];

    public PomSubmissionForCalculation Trimmed() => this with
    {
        Id = ObjectIds.CanonicalOrOriginal(Id),
        SchemeOrgId = ObjectIds.CanonicalOrOriginal(SchemeOrgId),
        CompliancePeriodQuarter = StringValues.TrimToEmpty(CompliancePeriodQuarter),
        Tonnes = Tonnes?.ConvertAll(StringDecimal128Serializer.CanonicalOrOriginal)!
    };
}
