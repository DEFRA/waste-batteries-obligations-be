using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.Tests.Integration.Utils;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.Tests.Integration.ObligationCalculation;

public partial class ObligationCalculationEndpointsTests
{
    [Fact]
    public async Task Indexes_WhenApplicationHasStarted_DefinesExpectedIndexes()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var indexes = await fixture.Calculations.Indexes.List(cancellationToken).ToListAsync(cancellationToken);

        // Assert
        var ordered = indexes.OrderBy(index => index["name"].AsString, StringComparer.Ordinal).ToList();
        Snapshot.Match(new BsonArray(ordered).ToJson(CanonicalJson));
    }

    [Fact]
    public async Task CreateAsync_WhenTwoCurrentVersionsShareASchemeAndYear_ThrowsDuplicateKeyOnSaveException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = fixture.Services.GetRequiredService<IObligationCalculationRepository>();
        var client = fixture.Services.GetRequiredService<IMongoDbClientFactory>().GetClient();
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        var calculation = Calculation(ObjectId.GenerateNewId().ToString());
        var duplicate = Calculation(calculation.SchemeOrgId);
        await repository.CreateAsync(session, calculation, cancellationToken);

        // Act
        Task Act() => repository.CreateAsync(session, duplicate, cancellationToken);

        // Assert
        await Assert.ThrowsAsync<DuplicateKeyOnSaveException>(Act);
    }

    [Fact]
    public async Task CreateAsync_WhenTheSessionOrCalculationIsMissing_Throws()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = fixture.Services.GetRequiredService<IObligationCalculationRepository>();
        var client = fixture.Services.GetRequiredService<IMongoDbClientFactory>().GetClient();
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        var calculation = Calculation(ObjectId.GenerateNewId().ToString());

        // Act
        Task MissingSession() => repository.CreateAsync(null!, calculation, cancellationToken);
        Task MissingCalculation() => repository.CreateAsync(session, null!, cancellationToken);

        // Assert
        var missingSession = await Assert.ThrowsAsync<ArgumentNullException>(MissingSession);
        var missingCalculation = await Assert.ThrowsAsync<ArgumentNullException>(MissingCalculation);
        Assert.Equal("session", missingSession.ParamName);
        Assert.Equal("calculation", missingCalculation.ParamName);
    }

    [Fact]
    public async Task SupersedeCurrentAsync_WhenTheSessionIsMissing_Throws()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = fixture.Services.GetRequiredService<IObligationCalculationRepository>();

        // Act
        Task Act() => repository.SupersedeCurrentAsync(null!, ObjectId.GenerateNewId().ToString(), DateTime.UtcNow, cancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(Act);
        Assert.Equal("session", exception.ParamName);
    }

    [Fact]
    public async Task FindCurrentAsync_WhenSchemeOrgIdIsMissing_Throws()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = fixture.Services.GetRequiredService<IObligationCalculationRepository>();

        // Act
        Task Act() => repository.FindCurrentAsync(null!, CompliancePeriodYear, cancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(Act);
        Assert.Equal("schemeOrgId", exception.ParamName);
    }

    [Fact]
    public async Task FindBySchemeYearAsync_WhenSchemeOrgIdIsMissing_Throws()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = fixture.Services.GetRequiredService<IObligationCalculationRepository>();

        // Act
        Task Act() => repository.FindBySchemeYearAsync(null!, CompliancePeriodYear, cancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(Act);
        Assert.Equal("schemeOrgId", exception.ParamName);
    }

    [Fact]
    public async Task SupersedeCurrentAsync_WhenTheCalculationIsMissing_ReturnsFalse()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var repository = fixture.Services.GetRequiredService<IObligationCalculationRepository>();
        var client = fixture.Services.GetRequiredService<IMongoDbClientFactory>().GetClient();
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);

        // Act
        var superseded = await repository.SupersedeCurrentAsync(
            session,
            ObjectId.GenerateNewId().ToString(),
            DateTime.UtcNow,
            cancellationToken);

        // Assert
        Assert.False(superseded);
    }

    private static ObligationCalculationModel Calculation(string schemeOrgId)
    {
        var content = new ObligationCalculationBase
        {
            SchemeOrgId = schemeOrgId,
            SchemeName = "Sample scheme",
            CompliancePeriodYear = CompliancePeriodYear,
            ObligationTonnes = "2100.500",
            PlacedOnMarketByYear =
            [
                new PlacedOnMarketYear { Year = CompliancePeriodYear, Tonnes = "1.000", QuartersOfData = 4, AnnualisedTonnes = "1.000" }
            ],
            Parameters = new ObligationParameters
            {
                Target = new ObligationTarget { Type = ObligationTarget.Percentage, Value = "45.00" },
                WindowYears = 1,
                DenominatorRule = ObligationParameters.YearsWithData
            },
            Trigger = new ObligationTrigger { Type = ObligationTrigger.Manual, Actor = "regulator.user@example.com", Reason = "Sample" }
        };
        return new ObligationCalculationModel(content, ObjectId.GenerateNewId().ToString(), DateTime.UtcNow, 1);
    }
}
