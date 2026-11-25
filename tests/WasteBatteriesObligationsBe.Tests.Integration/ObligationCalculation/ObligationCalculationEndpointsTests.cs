using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WasteBatteriesObligationsBe.Tests.Integration.ObligationCalculation;

public partial class ObligationCalculationEndpointsTests(ObligationCalculationApiFixture fixture)
    : IClassFixture<ObligationCalculationApiFixture>, IAsyncLifetime
{
    // A fixed scheme keeps the sample figures stable for the stored snapshot.
    private const string CreateSchemeOrgId = "674a1b2c3d4e5f6789012345";

    private const string VersionSchemeOrgId = "674a1b2c3d4e5f6789012346";

    private const string ConflictSchemeOrgId = "674a1b2c3d4e5f6789012349";

    private const string PinnedCalculationId = "000000000000000000000001";

    private const string PinnedReplacementId = "000000000000000000000002";

    private const int CompliancePeriodYear = 2026;

    // The clock restarts at the fixed instant, so a save lands on the same timestamp in every test.
    public ValueTask InitializeAsync()
    {
        fixture.Clock.Reset();
        fixture.Logger.Entries.Clear();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task<List<BsonDocument>> Stored(string schemeOrgId, CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("schemeOrgId", ObjectId.Parse(schemeOrgId));
        return await fixture.Calculations
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("version"))
            .ToListAsync(cancellationToken);
    }

    // Generated calculation ids are pinned. The sample figures and source submission ids stay as stored.
    private static string StoredJson(BsonDocument document, string id)
    {
        var snapshot = document.DeepClone().AsBsonDocument;
        snapshot["_id"] = ObjectId.Parse(id);
        return snapshot.ToJson(CanonicalJson);
    }

    // The previous row is first, then the replacement.
    private static string StoredPair(BsonDocument previous, BsonDocument replacement)
    {
        const string previousId = "000000000000000000000001";
        var pinnedPrevious = Pin(previous, previousId);
        var pinnedReplacement = Pin(replacement, "000000000000000000000002");
        if (pinnedReplacement["supersedesCalculationId"].AsObjectId == previous["_id"].AsObjectId)
        {
            pinnedReplacement["supersedesCalculationId"] = ObjectId.Parse(previousId);
        }

        return new BsonArray { pinnedPrevious, pinnedReplacement }.ToJson(CanonicalJson);
    }

    private static BsonDocument Pin(BsonDocument document, string id)
    {
        var pinned = document.DeepClone().AsBsonDocument;
        pinned["_id"] = ObjectId.Parse(id);
        return pinned;
    }

    // The API body, with the generated id (and the id it replaces) pinned so the file stays stable.
    private static string ResponseJson(string json, string id, string? supersedesId = null)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        node["id"] = id;
        if (supersedesId is not null)
        {
            node["supersedesCalculationId"] = supersedesId;
        }

        return Readable(node);
    }

    // traceId is a different value on every request. The rest of the problem is the contract.
    private static string ProblemJson(string json)
    {
        var node = JsonNode.Parse(json)!.AsObject();
        if (node.ContainsKey("traceId"))
        {
            node["traceId"] = "trace-id";
        }

        return Readable(node);
    }

    private static string Readable(JsonNode node) => node.ToJsonString(IndentedJson) + "\n";

    private static JsonWriterSettings CanonicalJson { get; } = new()
    {
        OutputMode = JsonOutputMode.CanonicalExtendedJson,
        Indent = true,
        NewLineChars = "\n"
    };

    private static JsonSerializerOptions IndentedJson { get; } = new()
    {
        WriteIndented = true,
        NewLine = "\n"
    };
}
