using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Mapping;

public class ObligationCalculationMapperTests
{
    private static readonly DateTime s_calculatedAt = new(2026, 11, 20, 10, 0, 1, DateTimeKind.Utc);

    [Fact]
    public void ToNewCalculation_WhenCalculationIsComplete_MapsTheFirstVersion()
    {
        // Arrange
        var calculation = Calculation();

        // Act
        var stored = Mapper().ToNewCalculation(calculation, s_calculatedAt);

        // Assert
        Assert.Equal(calculation.SchemeOrgId, stored.SchemeOrgId);
        Assert.Equal(calculation.SchemeName, stored.SchemeName);
        Assert.Equal(calculation.CompliancePeriodYear, stored.CompliancePeriodYear);
        Assert.Equal(calculation.ObligationTonnes, stored.ObligationTonnes);
        Assert.Equal(calculation.PlacedOnMarketByYear, stored.PlacedOnMarketByYear);
        Assert.Equal(calculation.Parameters, stored.Parameters);
        Assert.Equal(calculation.Trigger, stored.Trigger);
        Assert.Equal(s_calculatedAt, stored.CalculatedAtUtc);
        Assert.Equal(ObligationCalculationModel.CurrentSchemaVersion, stored.SchemaVersion);
        Assert.Equal(ObligationCalculationModel.InitialVersion, stored.Version);
        Assert.True(stored.IsCurrent);
        Assert.Null(stored.SupersedesCalculationId);
        Assert.Null(stored.UpdatedAtUtc);
        Assert.True(ObjectId.TryParse(stored.Id, out _));
    }

    [Fact]
    public void ToNewCalculation_WhenVersionAndPredecessorAreSupplied_SetsTheSuppliedVersionAndPredecessor()
    {
        // Arrange
        var previousId = ObjectId.GenerateNewId().ToString();

        // Act
        var stored = Mapper().ToNewCalculation(
            Calculation(),
            s_calculatedAt,
            version: 5,
            supersedesCalculationId: previousId);

        // Assert
        Assert.Equal(5, stored.Version);
        Assert.Equal(previousId, stored.SupersedesCalculationId);
        Assert.True(stored.IsCurrent);
        Assert.Null(stored.UpdatedAtUtc);
    }

    [Fact]
    public void ToNewCalculation_WhenThereAreNoYears_ThrowsArgumentException()
    {
        // Arrange
        var calculation = Calculation() with { PlacedOnMarketByYear = [] };

        // Act
        ObligationCalculationModel Act() => Mapper().ToNewCalculation(calculation, s_calculatedAt);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Equal("calculation", exception.ParamName);
    }

    [Fact]
    public void ToNewCalculation_WhenCalculationIsNull_ThrowsArgumentNullException()
    {
        // Act
        ObligationCalculationModel Act() => Mapper().ToNewCalculation(null!, s_calculatedAt);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(Act);
        Assert.Equal("calculation", exception.ParamName);
    }

    [Fact]
    public void ToNewCalculation_WhenCalculationIsMapped_RoundTripsThroughBson()
    {
        // Arrange
        MongoConventions.Register();
        var calculation = Calculation();
        var stored = Mapper().ToNewCalculation(calculation, s_calculatedAt, version: 2, supersedesCalculationId: ObjectId.GenerateNewId().ToString());

        // Act
        var document = stored.ToBsonDocument();
        var read = BsonSerializer.Deserialize<ObligationCalculationModel>(document);

        // Assert
        Assert.Equal(BsonType.Decimal128, document["obligationTonnes"].BsonType);
        Assert.Equal(stored.Id, read.Id);
        Assert.Equal(stored.SchemeOrgId, read.SchemeOrgId);
        Assert.Equal(stored.SchemeName, read.SchemeName);
        Assert.Equal(stored.ObligationTonnes, read.ObligationTonnes);
        Assert.Equal(stored.Version, read.Version);
        Assert.Equal(stored.SupersedesCalculationId, read.SupersedesCalculationId);
        Assert.Equal(stored.IsCurrent, read.IsCurrent);
        Assert.Equal(stored.CalculatedAtUtc, read.CalculatedAtUtc);
        Assert.Equal(stored.PlacedOnMarketByYear[0].Tonnes, read.PlacedOnMarketByYear[0].Tonnes);
    }

    private static ObligationCalculationMapper Mapper() => new();

    private static ObligationCalculationBase Calculation() => new()
    {
        SchemeOrgId = ObjectId.GenerateNewId().ToString(),
        SchemeName = "IronWave Compliance",
        CompliancePeriodYear = 2026,
        ObligationTonnes = "2100.500",
        PlacedOnMarketByYear =
        [
            new PlacedOnMarketYear { Year = 2026, Tonnes = "1000.000", QuartersOfData = 4, AnnualisedTonnes = "1000.000" }
        ],
        Parameters = new ObligationParameters
        {
            Target = new ObligationTarget { Type = ObligationTarget.Percentage, Value = "45.00" },
            WindowYears = 1,
            DenominatorRule = ObligationParameters.YearsWithData
        },
        Trigger = new ObligationTrigger { Type = ObligationTrigger.Manual, Actor = "regulator.user@example.com", Reason = "Sample" }
    };
}
