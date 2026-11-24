using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.Tests.Unit.Utils.Mongo;

public class StringDecimal128SerializerTests
{
    public StringDecimal128SerializerTests()
    {
        MongoConventions.Register();
    }

    [Theory]
    [InlineData("2100.500", "2100.500")]
    [InlineData("  02100.500  ", "2100.500")]
    [InlineData("000.000", "0.000")]
    [InlineData("45.00", "45.00")]
    [InlineData("045.00", "45.00")]
    [InlineData("twelve", "twelve")]
    [InlineData("  ", "")]
    public void CanonicalOrOriginal_WhenCalled_UsesTheStoredDecimalText(string value, string canonical)
    {
        Assert.Equal(canonical, StringDecimal128Serializer.CanonicalOrOriginal(value));
    }

    [Fact]
    public void ToBsonDocument_WhenTonnesHaveScale_RoundTripsAsDecimal128()
    {
        // Arrange
        var year = new PlacedOnMarketYear
        {
            Year = 2026,
            Tonnes = "2100.500",
            QuartersOfData = 3,
            AnnualisedTonnes = "1333.333",
            IsAnnualisedEstimate = true
        };

        // Act
        var document = year.ToBsonDocument();
        var stored = BsonSerializer.Deserialize<PlacedOnMarketYear>(document);

        // Assert
        Assert.Equal(BsonType.Decimal128, document["tonnes"].BsonType);
        Assert.Equal("2100.500", document["tonnes"].AsDecimal128.ToString());
        Assert.Equal(BsonType.Decimal128, document["annualisedTonnes"].BsonType);
        Assert.Equal("1333.333", document["annualisedTonnes"].AsDecimal128.ToString());
        Assert.Equal("2100.500", stored.Tonnes);
        Assert.Equal("1333.333", stored.AnnualisedTonnes);
        Assert.True(stored.IsAnnualisedEstimate);
    }

    [Fact]
    public void ToBsonDocument_WhenTargetHasTwoDecimalPlaces_RoundTripsAsDecimal128()
    {
        // Arrange
        var target = new ObligationTarget
        {
            Type = ObligationTarget.Percentage,
            Value = "45.00"
        };

        // Act
        var document = target.ToBsonDocument();
        var stored = BsonSerializer.Deserialize<ObligationTarget>(document);

        // Assert
        Assert.Equal(BsonType.Decimal128, document["value"].BsonType);
        Assert.Equal("45.00", document["value"].AsDecimal128.ToString());
        Assert.Equal("45.00", stored.Value);
    }

    [Fact]
    public void ToBsonDocument_WhenTheEstimateFlagIsFalse_OmitsIt()
    {
        // Arrange
        var year = new PlacedOnMarketYear
        {
            Tonnes = "0.000",
            AnnualisedTonnes = "0.000"
        };

        // Act
        var document = year.ToBsonDocument();

        // Assert
        Assert.False(document.Contains("isAnnualisedEstimate"));
    }

    [Fact]
    public void Deserialize_WhenTonnesIsNotDecimal128_Throws()
    {
        // Arrange
        var document = new BsonDocument
        {
            { "year", 2026 },
            { "tonnes", "2100.500" },
            { "quartersOfData", 4 },
            { "annualisedTonnes", Decimal128.Parse("2100.500") }
        };

        // Act
        PlacedOnMarketYear Act() => BsonSerializer.Deserialize<PlacedOnMarketYear>(document);

        // Assert
        Assert.ThrowsAny<Exception>(Act);
    }

    [Fact]
    public void ToBsonDocument_WhenTonnesIsNotADecimal_Throws()
    {
        // Arrange
        var year = new PlacedOnMarketYear
        {
            Tonnes = "twelve",
            AnnualisedTonnes = "0.000"
        };

        // Act
        BsonDocument Act() => year.ToBsonDocument();

        // Assert
        Assert.ThrowsAny<Exception>(Act);
    }
}
