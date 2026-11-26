using FluentValidation;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;
using WasteBatteriesObligationsBe.Tests.Common;
using WasteBatteriesObligationsBe.Utils.Mongo;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Services;

public partial class ObligationCalculationServiceTests
{
    private static readonly DateTime s_utc = new DateTime(2026, 11, 20, 10, 0, 1, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WhenThereAreNoYears_ThrowsArgumentException()
    {
        // Arrange
        var content = Content() with { PlacedOnMarketByYear = [] };

        // Act
        ObligationCalculationModel Act() => new(content, ObjectId.GenerateNewId().ToString(), s_utc, 1);

        // Assert
        var exception = Assert.Throws<ArgumentException>(Act);
        Assert.Equal("calculation", exception.ParamName);
    }

    private static ObligationCalculationService Service(
        IObligationCalculationRepository repository,
        CapturingLogger<ObligationCalculationService> logger,
        IUnitOfWork? unitOfWork = null,
        ITimeProvider? time = null,
        ObligationCalculationConfig? config = null,
        IValidator<CalculationData>? calculationDataValidator = null)
    {
        var options = Options.Create(config ?? CalculationConfig());
        return new(
            new ObligationCalculationDependencies(
                repository,
                unitOfWork ?? Substitute.For<IUnitOfWork>(),
                new ObligationCalculationMapper(),
                new ObligationCalculationBaseValidator(),
                calculationDataValidator ?? new CalculationDataValidator(options),
                options),
            time ?? new FixedTime(s_utc),
            logger);
    }

    private static ObligationCalculationConfig CalculationConfig(
        string targetPercentage = "45.00",
        int windowYears = 3) => new()
    {
        TargetPercentage = targetPercentage,
        WindowYears = windowYears,
        DenominatorRule = ObligationParameters.YearsWithData
    };

    private static IUnitOfWork TransactionFor<T>(IClientSessionHandle session)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<IMongoTransaction>();
        transaction.Session.Returns(session);
        unitOfWork
            .ExecuteAsync(
                Arg.Any<Func<IMongoTransaction, CancellationToken, Task<T>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var work = call.Arg<Func<IMongoTransaction, CancellationToken, Task<T>>>();
                return work(transaction, CancellationToken.None);
            });
        return unitOfWork;
    }

    private static ObligationCalculationModel Stored(ObligationCalculationBase content, int version) =>
        new(content, ObjectId.GenerateNewId().ToString(), s_utc, version);

    private static ObligationCalculationBase Content(string tonnes = "2100.500") => new()
    {
        SchemeOrgId = ObjectId.GenerateNewId().ToString(),
        SchemeName = "IronWave Compliance",
        CompliancePeriodYear = 2026,
        ObligationTonnes = tonnes,
        PlacedOnMarketByYear =
        [
            new PlacedOnMarketYear { Year = 2024, Tonnes = "0.000", QuartersOfData = 0, AnnualisedTonnes = "0.000" },
            new PlacedOnMarketYear { Year = 2025, Tonnes = "4000.000", QuartersOfData = 4, AnnualisedTonnes = "4000.000" },
            new PlacedOnMarketYear
            {
                Year = 2026,
                Tonnes = "1000.000",
                QuartersOfData = 3,
                AnnualisedTonnes = "1333.333",
                IsAnnualisedEstimate = true
            }
        ],
        Parameters = new ObligationParameters
        {
            Target = new ObligationTarget { Type = ObligationTarget.Percentage, Value = "45.00" },
            WindowYears = 3,
            DenominatorRule = ObligationParameters.YearsWithData
        },
        Trigger = new ObligationTrigger
        {
            Type = ObligationTrigger.Event,
            EventId = "evt-1009",
            CorrelationId = "corr-1009"
        }
    };
}
