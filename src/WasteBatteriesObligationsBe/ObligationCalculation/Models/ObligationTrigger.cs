using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
using WasteBatteriesObligationsBe.Utils.Text;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Models;

// What started this calculation. An event carries the event id. A manual run carries the actor and reason.
[BsonIgnoreExtraElements]
[BsonNoId]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ObligationTrigger
{
    public const string Event = "event";

    public const string Manual = "manual";

    public string Type { get; init; } = string.Empty;

    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EventId { get; init; }

    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CorrelationId { get; init; }

    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Actor { get; init; }

    [BsonIgnoreIfNull]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    public ObligationTrigger Trimmed() => this with
    {
        Type = StringValues.TrimToEmpty(Type),
        EventId = StringValues.TrimToNull(EventId),
        CorrelationId = StringValues.TrimToNull(CorrelationId),
        Actor = StringValues.TrimToNull(Actor),
        Reason = StringValues.TrimToNull(Reason)
    };
}
