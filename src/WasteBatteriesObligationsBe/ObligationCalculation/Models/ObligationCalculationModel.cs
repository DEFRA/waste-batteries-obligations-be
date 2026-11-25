using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Models;

// One immutable indicative obligation for a scheme and compliance year.
// A later calculation is a new document. The previous document keeps its figure.
// isCurrent and updatedAtUtc are the only fields a later version changes.
// supersedesCalculationId is set on the new document and then left alone.
[BsonIgnoreExtraElements]
public sealed record ObligationCalculationModel : ObligationCalculationBase
{
    // The shape this service writes. A new shape is a code change, and this version is incremented with it.
    public const int CurrentSchemaVersion = 1;

    public const int InitialVersion = 1;

    [BsonConstructor]
    [JsonConstructor]
    [SetsRequiredMembers]
    public ObligationCalculationModel()
    {
        Id = null!;
    }

    // : base(calculation) uses the compiler copy constructor, so the shared fields are not assigned again.
    [SetsRequiredMembers]
    internal ObligationCalculationModel(
        ObligationCalculationBase calculation,
        string id,
        DateTime calculatedAtUtc,
        int version,
        string? supersedesCalculationId = null)
        : base(calculation ?? throw new ArgumentNullException(nameof(calculation)))
    {
        if (calculation.PlacedOnMarketByYear is not { Count: > 0 } || calculation.PlacedOnMarketByYear.Exists(year => year is null))
        {
            throw new ArgumentException(
                "A calculation contains at least one placed-on-market year.",
                nameof(calculation));
        }

        Id = id;
        SchemaVersion = CurrentSchemaVersion;
        Version = version;
        SupersedesCalculationId = supersedesCalculationId;
        IsCurrent = true;
        CalculatedAtUtc = calculatedAtUtc;
        UpdatedAtUtc = null;
    }

    // Exposed as "id" in the API. Stored as ObjectId _id.
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public required string Id { get; init; }

    public int SchemaVersion { get; init; }

    // Starts at 1 and increments per scheme and compliance year.
    public int Version { get; init; }

    // Null on the first version. A later version points at the calculation it replaces.
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SupersedesCalculationId { get; init; }

    // True on the latest version only. A newer version sets this to false.
    public bool IsCurrent { get; init; } = true;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CalculatedAtUtc { get; init; }

    // Set only when a newer version clears isCurrent.
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? UpdatedAtUtc { get; init; }
}
