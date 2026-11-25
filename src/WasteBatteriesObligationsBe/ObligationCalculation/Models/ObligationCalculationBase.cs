using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using WasteBatteriesObligationsBe.Utils.Mongo;
using WasteBatteriesObligationsBe.Utils.Text;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Models;

// Fields the service is asked to store, and which are stored on the calculation.
// The compiler-generated copy constructor copies these fields onto an ObligationCalculationModel.
public record ObligationCalculationBase
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string SchemeOrgId { get; init; } = string.Empty;

    // Snapshot of the scheme name at calculation time.
    // TEMPORARY: blank until the calculation calls Manage Account for the name.
    public string SchemeName { get; init; } = string.Empty;

    public int CompliancePeriodYear { get; init; }

    [BsonSerializer(typeof(StringDecimal128Serializer))]
    public string ObligationTonnes { get; init; } = string.Empty;

    public List<PlacedOnMarketYear> PlacedOnMarketByYear { get; init; } = [];

    public ObligationParameters Parameters { get; init; } = new();

    public ObligationTrigger Trigger { get; init; } = new();

    public ObligationCalculationBase Trimmed() => this with
    {
        SchemeOrgId = ObjectIds.CanonicalOrOriginal(SchemeOrgId),
        SchemeName = StringValues.TrimToEmpty(SchemeName),
        ObligationTonnes = StringDecimal128Serializer.CanonicalOrOriginal(ObligationTonnes),
        PlacedOnMarketByYear = PlacedOnMarketByYear?.ConvertAll(year => year is null ? null! : year.Trimmed())!,
        Parameters = Parameters?.Trimmed()!,
        Trigger = Trigger?.Trimmed()!
    };
}
