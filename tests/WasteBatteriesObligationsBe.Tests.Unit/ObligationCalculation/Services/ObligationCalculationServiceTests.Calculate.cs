using FluentValidation;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;
using WasteBatteriesObligationsBe.Tests.Common;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Services;

public partial class ObligationCalculationServiceTests
{
    [Fact]
    public async Task CalculateAsync_WhenDataIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = Service(
            Substitute.For<IObligationCalculationRepository>(),
            new CapturingLogger<ObligationCalculationService>());

        // Act
        Task<ObligationCalculationModel> Act() => service.CalculateAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }

    [Fact]
    public async Task CalculateAsync_WhenThereAreNoSubmissions_LogsTheErrorAndDoesNotWrite()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var logger = new CapturingLogger<ObligationCalculationService>();
        var service = Service(repository, logger, unitOfWork);

        // Act
        Task<ObligationCalculationModel> Act() => service.CalculateAsync(
            Data(2026, []),
            TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == CalculationDataValidationMessages.SubmissionsRequired);
        await repository.DidNotReceive().FindCurrentAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<IMongoTransaction, CancellationToken, Task<ObligationCalculationModel>>>(),
            Arg.Any<CancellationToken>());
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(8, entry.EventId.Id);
    }

    [Fact]
    public async Task CalculateAsync_WhenTheYearsAreComplete_StoresTheAverageAndABlankSchemeName()
    {
        // Arrange
        // (4,000 + 5,000 + 6,000) ÷ 3 × 45% = 2,250.000
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var service = Service(repository, new CapturingLogger<ObligationCalculationService>(), TransactionFor<ObligationCalculationModel>(session));
        var request = Data(2026,
        [
            ..Year(schemeOrgId, 2024, quarters: 4, tonnes: "4000.000"),
            ..Year(schemeOrgId, 2025, quarters: 4, tonnes: "5000.000"),
            ..Year(schemeOrgId, 2026, quarters: 4, tonnes: "6000.000")
        ]);

        // Act
        var calculation = await service.CalculateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(string.Empty, calculation.SchemeName);
        Assert.Equal(schemeOrgId, calculation.SchemeOrgId);
        Assert.Equal(2026, calculation.CompliancePeriodYear);
        Assert.Equal(1, calculation.Version);
        Assert.Null(calculation.SupersedesCalculationId);
        Assert.Equal("2250.000", calculation.ObligationTonnes);
        Assert.Equal("45.00", calculation.Parameters.Target.Value);
        Assert.Equal(ObligationTarget.Percentage, calculation.Parameters.Target.Type);
        Assert.Equal(3, calculation.Parameters.WindowYears);
        Assert.Equal(ObligationParameters.YearsWithData, calculation.Parameters.DenominatorRule);
        Assert.Equal("evt-1009", calculation.Trigger.EventId);
        AssertYear(calculation.PlacedOnMarketByYear[0], 2024, "4000.000", 4, "4000.000", annualised: false);
        AssertYear(calculation.PlacedOnMarketByYear[1], 2025, "5000.000", 4, "5000.000", annualised: false);
        AssertYear(calculation.PlacedOnMarketByYear[2], 2026, "6000.000", 4, "6000.000", annualised: false);
    }

    [Fact]
    public async Task CalculateAsync_WhenTheComplianceYearHasTwoQuarters_AnnualisesOnlyThatYear()
    {
        // Arrange
        // 2024 has 3 quarters and is not scaled. 2026 is multiplied by 2.
        // (4,000 + 2,000 + 8,000) ÷ 3 × 45% = 2,100.000
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var service = ReadyService();
        var request = Data(2026,
        [
            ..Year(schemeOrgId, 2024, quarters: 3, tonnes: "4000.000"),
            ..Year(schemeOrgId, 2025, quarters: 4, tonnes: "2000.000"),
            ..Year(schemeOrgId, 2026, quarters: 2, tonnes: "4000.000")
        ]);

        // Act
        var calculation = await service.CalculateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("2100.000", calculation.ObligationTonnes);
        AssertYear(calculation.PlacedOnMarketByYear[0], 2024, "4000.000", 3, "4000.000", annualised: false);
        AssertYear(calculation.PlacedOnMarketByYear[1], 2025, "2000.000", 4, "2000.000", annualised: false);
        AssertYear(calculation.PlacedOnMarketByYear[2], 2026, "4000.000", 2, "8000.000", annualised: true);
    }

    [Fact]
    public async Task CalculateAsync_WhenTheComplianceYearHasThreeQuarters_RoundsTheStoredFigures()
    {
        // Arrange
        // 1,000 × 4 ÷ 3 recurs. The stored annualised figure is 1,333.333.
        // The obligation uses the unrounded value: (4,000 + 5,000 + 1,333.333...) ÷ 3 × 45% = 1,550.000
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var service = ReadyService();
        var request = Data(2026,
        [
            ..Year(schemeOrgId, 2024, quarters: 4, tonnes: "4000.000"),
            ..Year(schemeOrgId, 2025, quarters: 4, tonnes: "5000.000"),
            ..Year(schemeOrgId, 2026, quarters: 3, tonnes: "1000.000")
        ]);

        // Act
        var calculation = await service.CalculateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("1550.000", calculation.ObligationTonnes);
        AssertYear(calculation.PlacedOnMarketByYear[2], 2026, "1000.000", 3, "1333.333", annualised: true);
    }

    [Fact]
    public async Task CalculateAsync_WhenAnEarlierYearIsMissing_DividesByTheYearsWithData()
    {
        // Arrange
        // 2024 has no submission, so it is not a year with data. (2,000 + 8,000) ÷ 2 × 45% = 2,250.000
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var service = ReadyService();
        var request = Data(2026,
        [
            ..Year(schemeOrgId, 2025, quarters: 4, tonnes: "2000.000"),
            ..Year(schemeOrgId, 2026, quarters: 4, tonnes: "8000.000")
        ]);

        // Act
        var calculation = await service.CalculateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("2250.000", calculation.ObligationTonnes);
        AssertYear(calculation.PlacedOnMarketByYear[0], 2024, "0.000", 0, "0.000", annualised: false);
        Assert.Empty(calculation.PlacedOnMarketByYear[0].SourceSubmissionIds);
        AssertYear(calculation.PlacedOnMarketByYear[1], 2025, "2000.000", 4, "2000.000", annualised: false);
    }

    [Fact]
    public async Task CalculateAsync_WhenAnEarlierYearIsAZeroReturn_CountsThatYear()
    {
        // Arrange
        // 2024 was filed with nil tonnage, so the calculation divides by 3.
        // (0 + 2,000 + 8,000) ÷ 3 × 45% = 1,500.000
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var service = ReadyService();
        var request = Data(2026,
        [
            ..Year(schemeOrgId, 2024, quarters: 4, tonnes: "0.000"),
            ..Year(schemeOrgId, 2025, quarters: 4, tonnes: "2000.000"),
            ..Year(schemeOrgId, 2026, quarters: 4, tonnes: "8000.000")
        ]);

        // Act
        var calculation = await service.CalculateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("1500.000", calculation.ObligationTonnes);
        AssertYear(calculation.PlacedOnMarketByYear[0], 2024, "0.000", 4, "0.000", annualised: false);
        Assert.NotEmpty(calculation.PlacedOnMarketByYear[0].SourceSubmissionIds);
    }

    [Fact]
    public async Task CalculateAsync_WhenOnlyTheComplianceYearIsAZeroReturn_StoresZero()
    {
        // Arrange
        // 0 ÷ 1 × 45% = 0.000. The missing years stay in the window and are not counted.
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var service = ReadyService();

        // Act
        var calculation = await service.CalculateAsync(
            Data(2026, Year(schemeOrgId, 2026, quarters: 4, tonnes: "0.000")),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("0.000", calculation.ObligationTonnes);
        Assert.Equal(3, calculation.PlacedOnMarketByYear.Count);
        AssertYear(calculation.PlacedOnMarketByYear[2], 2026, "0.000", 4, "0.000", annualised: false);
    }

    [Fact]
    public async Task CalculateAsync_WhenTwoSubmissionsShareAQuarter_CountsTheQuarterOnce()
    {
        // Arrange
        // 600 + 400 in Quarter 1 is 1,000 tonnes and one quarter, so the year scales by 4.
        // 4,000 × 45% = 1,800.000
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var firstId = ObjectId.GenerateNewId().ToString();
        var secondId = ObjectId.GenerateNewId().ToString();
        var service = ReadyService();
        var request = Data(2026,
        [
            Submission(schemeOrgId, 2026, PomSubmissionForCalculation.Quarter1, "600.000", "400.000") with { Id = firstId },
            Submission(schemeOrgId, 2026, PomSubmissionForCalculation.Quarter1, "0.000") with { Id = secondId }
        ]);

        // Act
        var calculation = await service.CalculateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("1800.000", calculation.ObligationTonnes);
        AssertYear(calculation.PlacedOnMarketByYear[2], 2026, "1000.000", 1, "4000.000", annualised: true);
        Assert.Equal([firstId, secondId], calculation.PlacedOnMarketByYear[2].SourceSubmissionIds);
    }

    [Fact]
    public async Task CalculateAsync_WhenTheConfiguredTargetIs50Percent_UsesThatPercentage()
    {
        // Arrange
        // One year of 1,000 tonnes. 1,000 × 50% = 500.000
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var repository = Substitute.For<IObligationCalculationRepository>();
        var service = Service(
            repository,
            new CapturingLogger<ObligationCalculationService>(),
            TransactionFor<ObligationCalculationModel>(Substitute.For<IClientSessionHandle>()),
            config: CalculationConfig(targetPercentage: "50.00"));

        // Act
        var calculation = await service.CalculateAsync(
            Data(2026, Year(schemeOrgId, 2026, quarters: 4, tonnes: "1000.000")),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("500.000", calculation.ObligationTonnes);
        Assert.Equal("50.00", calculation.Parameters.Target.Value);
        Assert.Equal(3, calculation.PlacedOnMarketByYear.Count);
    }

    [Fact]
    public async Task CalculateAsync_WhenACurrentVersionExists_StoresTheNextVersion()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var current = Stored(Content() with { SchemeOrgId = schemeOrgId }, 4);
        repository
            .FindCurrentAsync(schemeOrgId, 2026, Arg.Any<CancellationToken>())
            .Returns(current);
        repository
            .SupersedeCurrentAsync(session, current.Id, s_utc, Arg.Any<CancellationToken>())
            .Returns(true);
        var service = Service(
            repository,
            new CapturingLogger<ObligationCalculationService>(),
            TransactionFor<ObligationCalculationModel>(session));

        // Act
        var calculation = await service.CalculateAsync(
            Data(2026, Year(schemeOrgId, 2026, quarters: 4, tonnes: "1000.000")),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, calculation.Version);
        Assert.Equal(current.Id, calculation.SupersedesCalculationId);
        Assert.Equal("450.000", calculation.ObligationTonnes);
    }

    [Fact]
    public async Task CalculateAsync_WhenIdsAndTonnesAreNotCanonical_StoresTheCanonicalValues()
    {
        // Arrange
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var submissionId = ObjectId.GenerateNewId().ToString();
        var service = ReadyService();
        var request = Data(2026,
        [
            new PomSubmissionForCalculation
            {
                Id = $"  {submissionId.ToUpperInvariant()}  ",
                SchemeOrgId = $"  {schemeOrgId.ToUpperInvariant()}  ",
                CompliancePeriodYear = 2026,
                CompliancePeriodQuarter = "  Quarter 1  ",
                Tonnes = ["  01000.000  "]
            }
        ]);

        // Act
        var calculation = await service.CalculateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(schemeOrgId, calculation.SchemeOrgId);
        Assert.Equal(submissionId, calculation.PlacedOnMarketByYear[2].SourceSubmissionIds[0]);
        Assert.Equal("1000.000", calculation.PlacedOnMarketByYear[2].Tonnes);
        Assert.Equal("4000.000", calculation.PlacedOnMarketByYear[2].AnnualisedTonnes);
    }

    private static ObligationCalculationService ReadyService()
    {
        var repository = Substitute.For<IObligationCalculationRepository>();
        return Service(
            repository,
            new CapturingLogger<ObligationCalculationService>(),
            TransactionFor<ObligationCalculationModel>(Substitute.For<IClientSessionHandle>()));
    }

    private static CalculationData Data(int year, IEnumerable<PomSubmissionForCalculation> submissions) => new()
    {
        CompliancePeriodYear = year,
        Submissions = submissions.ToList(),
        Trigger = new ObligationTrigger
        {
            Type = ObligationTrigger.Event,
            EventId = "evt-1009",
            CorrelationId = "corr-1009"
        }
    };

    // The year's tonnes sit on the first quarter. Later quarters are zero returns, so the quarter still counts.
    private static List<PomSubmissionForCalculation> Year(string schemeOrgId, int year, int quarters, string tonnes)
    {
        var submissions = new List<PomSubmissionForCalculation>(quarters);
        for (var quarter = 1; quarter <= quarters; quarter++)
        {
            submissions.Add(Submission(
                schemeOrgId,
                year,
                PomSubmissionForCalculation.Quarters[quarter - 1],
                quarter == 1 ? tonnes : "0.000"));
        }

        return submissions;
    }

    private static PomSubmissionForCalculation Submission(string schemeOrgId, int year, string quarter, params string[] tonnes) => new()
    {
        Id = ObjectId.GenerateNewId().ToString(),
        SchemeOrgId = schemeOrgId,
        CompliancePeriodYear = year,
        CompliancePeriodQuarter = quarter,
        Tonnes = [.. tonnes]
    };

    private static void AssertYear(
        PlacedOnMarketYear year,
        int expectedYear,
        string tonnes,
        int quarters,
        string annualisedTonnes,
        bool annualised)
    {
        Assert.Equal(expectedYear, year.Year);
        Assert.Equal(tonnes, year.Tonnes);
        Assert.Equal(quarters, year.QuartersOfData);
        Assert.Equal(annualisedTonnes, year.AnnualisedTonnes);
        Assert.Equal(annualised, year.IsAnnualisedEstimate);
    }
}
