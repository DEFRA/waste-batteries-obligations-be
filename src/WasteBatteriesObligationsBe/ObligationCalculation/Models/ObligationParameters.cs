using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
using WasteBatteriesObligationsBe.Utils.Mongo;
using WasteBatteriesObligationsBe.Utils.Text;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Models;

// A copy of the policy values used for this version. A later change cannot alter it.
[BsonIgnoreExtraElements]
[BsonNoId]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ObligationParameters
{
    // Divide by the years in the window whose quartersOfData is above 0.
    public const string YearsWithData = "yearsWithData";

    public ObligationTarget Target { get; init; } = new();

    public int WindowYears { get; init; }

    public string DenominatorRule { get; init; } = string.Empty;

    public ObligationParameters Trimmed() => this with
    {
        DenominatorRule = StringValues.TrimToEmpty(DenominatorRule),
        Target = Target?.Trimmed()!
    };
}

// target.value is a percentage when type is percentage, so "45.00" means 45%.
[BsonIgnoreExtraElements]
[BsonNoId]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ObligationTarget
{
    public const string Percentage = "percentage";

    public string Type { get; init; } = string.Empty;

    // String decimal at the API, Decimal128 at rest.
    [BsonSerializer(typeof(StringDecimal128Serializer))]
    public string Value { get; init; } = string.Empty;

    public ObligationTarget Trimmed() => this with
    {
        Type = StringValues.TrimToEmpty(Type),
        Value = StringDecimal128Serializer.CanonicalOrOriginal(Value)
    };
}
