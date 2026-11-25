using MongoDB.Bson;
using WasteBatteriesObligationsBe.Utils.Text;

namespace WasteBatteriesObligationsBe.Utils.Mongo;

// ObjectId text is canonical, so a different hex case is the same id after a later read.
internal static class ObjectIds
{
    public static string CanonicalOrOriginal(string? value)
    {
        var trimmed = StringValues.TrimToEmpty(value);
        return ObjectId.TryParse(trimmed, out var objectId) ? objectId.ToString() : trimmed;
    }
}
