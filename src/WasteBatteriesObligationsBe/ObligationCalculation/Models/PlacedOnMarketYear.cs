using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Models;

// One year in the calculation window, including a missing year. Tonnes are Decimal128 at rest.
[BsonIgnoreExtraElements]
[BsonNoId]
public sealed record PlacedOnMarketYear
{
    public int Year { get; init; }

    [BsonSerializer(typeof(StringDecimal128Serializer))]
    public string Tonnes { get; init; } = string.Empty;

    public int QuartersOfData { get; init; }

    [BsonSerializer(typeof(StringDecimal128Serializer))]
    public string AnnualisedTonnes { get; init; } = string.Empty;

    // Only stored and returned when the year was scaled up.
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsAnnualisedEstimate { get; init; }

    // The POM submissions behind this year. Empty when the year is missing. Stored, and not returned on the API.
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonIgnore]
    public List<string> SourceSubmissionIds { get; init; } = [];

    public PlacedOnMarketYear Trimmed() => this with
    {
        Tonnes = StringDecimal128Serializer.CanonicalOrOriginal(Tonnes),
        AnnualisedTonnes = StringDecimal128Serializer.CanonicalOrOriginal(AnnualisedTonnes),
        SourceSubmissionIds = SourceSubmissionIds?.ConvertAll(ObjectIds.CanonicalOrOriginal)!
    };
}
