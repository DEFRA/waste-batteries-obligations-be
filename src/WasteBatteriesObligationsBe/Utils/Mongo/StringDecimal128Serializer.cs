using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace WasteBatteriesObligationsBe.Utils.Mongo;

// Tonnages and other decimals are a string at the API and Decimal128 at rest, so JSON never builds a float.
// The string is parsed only at this boundary.
public sealed class StringDecimal128Serializer : SerializerBase<string>
{
    // The form stored and read back. Leading zeros are removed and the scale is kept,
    // so "02100.500" and a later read of that value are the same string.
    public static string CanonicalOrOriginal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        try
        {
            return Decimal128.Parse(trimmed).ToString();
        }
        catch (FormatException)
        {
            return trimmed;
        }
    }

    public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();
        if (bsonType != BsonType.Decimal128)
        {
            throw CreateCannotDeserializeFromBsonTypeException(bsonType);
        }

        return context.Reader.ReadDecimal128().ToString();
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
    {
        context.Writer.WriteDecimal128(Decimal128.Parse(value));
    }
}
