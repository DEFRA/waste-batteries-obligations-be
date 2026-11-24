using FluentValidation;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;
using WasteBatteriesObligationsBe.Tests.Common;
using WasteBatteriesObligationsBe.Utils.Mongo;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Services;

public class ObligationCalculationServiceTests
{
    private static readonly DateTime s_utc = new DateTime(2026, 11, 20, 10, 0, 1, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_WhenContentIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = Service(
            Substitute.For<IObligationCalculationRepository>(),
            new CapturingLogger<ObligationCalculationService>());

        // Act
        Task<ObligationCalculationModel> Act() => service.CreateAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }

    [Fact]
    public async Task CreateAsync_WhenContentIsInvalid_LogsTheErrorAndDoesNotWrite()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var logger = new CapturingLogger<ObligationCalculationService>();
        var service = Service(repository, logger, unitOfWork);
        var content = Content() with { SchemeOrgId = "not-an-id", ObligationTonnes = "1.2" };

        // Act
        Task<ObligationCalculationModel> Act() => service.CreateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.SchemeOrgIdObjectId);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.ObligationTonnesFormat);
        await unitOfWork.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<IMongoTransaction, CancellationToken, Task<ObligationCalculationModel>>>(),
            Arg.Any<CancellationToken>());
        await repository.DidNotReceive().FindCurrentAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(7, entry.EventId.Id);
        Assert.Contains("SchemeOrgId: schemeOrgId must be an ObjectId.", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_WhenStringsHaveSurroundingWhitespace_SavesTheTrimmedCalculation()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var content = Content() with
        {
            SchemeOrgId = $"  {schemeOrgId}  ",
            SchemeName = "  IronWave Compliance  ",
            Trigger = new ObligationTrigger
            {
                Type = ObligationTrigger.Event,
                EventId = " evt-1009 ",
                CorrelationId = " corr-1009 ",
                Actor = "  "
            }
        };
        var service = Service(repository, new CapturingLogger<ObligationCalculationService>(), TransactionFor<ObligationCalculationModel>(session));

        // Act
        var calculation = await service.CreateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(schemeOrgId, calculation.SchemeOrgId);
        Assert.Equal("IronWave Compliance", calculation.SchemeName);
        Assert.Equal("evt-1009", calculation.Trigger.EventId);
        Assert.Equal("corr-1009", calculation.Trigger.CorrelationId);
        Assert.Null(calculation.Trigger.Actor);
    }

    [Fact]
    public async Task CreateAsync_WhenDecimalsAndIdsAreNotCanonical_SavesTheCanonicalValues()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        var submissionId = ObjectId.GenerateNewId().ToString();
        var content = Content() with
        {
            SchemeOrgId = $"  {schemeOrgId.ToUpperInvariant()}  ",
            ObligationTonnes = "  02100.500  ",
            PlacedOnMarketByYear =
            [
                new PlacedOnMarketYear
                {
                    Year = 2024,
                    Tonnes = "000.000",
                    QuartersOfData = 0,
                    AnnualisedTonnes = "0.000"
                },
                new PlacedOnMarketYear
                {
                    Year = 2025,
                    Tonnes = "4000.000",
                    QuartersOfData = 4,
                    AnnualisedTonnes = "4000.000",
                    SourceSubmissionIds = [submissionId.ToUpperInvariant()]
                },
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
                Target = new ObligationTarget { Type = ObligationTarget.Percentage, Value = "045.00" },
                WindowYears = 3,
                DenominatorRule = ObligationParameters.YearsWithData
            }
        };
        var service = Service(repository, new CapturingLogger<ObligationCalculationService>(), TransactionFor<ObligationCalculationModel>(session));

        // Act
        var calculation = await service.CreateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(schemeOrgId, calculation.SchemeOrgId);
        Assert.Equal("2100.500", calculation.ObligationTonnes);
        Assert.Equal("0.000", calculation.PlacedOnMarketByYear[0].Tonnes);
        Assert.Equal(submissionId, calculation.PlacedOnMarketByYear[1].SourceSubmissionIds[0]);
        Assert.Equal("45.00", calculation.Parameters.Target.Value);
    }

    [Fact]
    public async Task CreateAsync_WhenContentIsValid_SavesTheCalculationAndLogsIt()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var logger = new CapturingLogger<ObligationCalculationService>();
        var service = Service(repository, logger, TransactionFor<ObligationCalculationModel>(session), new FixedTime(s_utc));
        var content = Content();

        // Act
        var calculation = await service.CreateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(s_utc, calculation.CalculatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, calculation.CalculatedAtUtc.Kind);
        await repository.Received(1).CreateAsync(
            session,
            Arg.Is<ObligationCalculationModel>(saved =>
                saved.Id == calculation.Id
                && saved.Version == 1
                && saved.SupersedesCalculationId == null
                && saved.IsCurrent
                && saved.SchemaVersion == ObligationCalculationModel.CurrentSchemaVersion
                && saved.SchemeOrgId == content.SchemeOrgId
                && saved.ObligationTonnes == "2100.500"
                && saved.UpdatedAtUtc == null
                && saved.CalculatedAtUtc == s_utc
                && saved.Trigger.EventId == "evt-1009"
                && saved.Trigger.Actor == null
                && saved.PlacedOnMarketByYear[2].IsAnnualisedEstimate
                && saved.Parameters.Target.Value == "45.00"),
            Arg.Any<CancellationToken>());
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(
            $"Stored obligation calculation {calculation.Id} version 1 for scheme {calculation.SchemeOrgId} compliance year 2026",
            entry.Message);
    }

    [Fact]
    public async Task CreateAsync_WhenCurrentVersionExists_ThrowsValidationException()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var content = Content();
        repository
            .FindCurrentAsync(content.SchemeOrgId, content.CompliancePeriodYear, Arg.Any<CancellationToken>())
            .Returns(Stored(content, 1));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = Service(repository, new CapturingLogger<ObligationCalculationService>(), unitOfWork);

        // Act
        Task<ObligationCalculationModel> Act() => service.CreateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.CurrentCalculationExists);
        await unitOfWork.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<IMongoTransaction, CancellationToken, Task<ObligationCalculationModel>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WhenTheVersionIndexRejectsTheInsert_ThrowsValidationException()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        repository
            .CreateAsync(session, Arg.Any<ObligationCalculationModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateKeyOnSaveException(new Exception("duplicate")));
        var service = Service(
            repository,
            new CapturingLogger<ObligationCalculationService>(),
            TransactionFor<ObligationCalculationModel>(session));

        // Act
        Task<ObligationCalculationModel> Act() => service.CreateAsync(Content(), TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.CurrentCalculationExists);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveFails_LogsTheError()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var failure = new InvalidOperationException("database unavailable");
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork
            .ExecuteAsync(
                Arg.Any<Func<IMongoTransaction, CancellationToken, Task<ObligationCalculationModel>>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(failure);
        var logger = new CapturingLogger<ObligationCalculationService>();
        var service = Service(repository, logger, unitOfWork);

        // Act
        Task<ObligationCalculationModel> Act() => service.CreateAsync(Content(), TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        Assert.Same(failure, exception);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(6, entry.EventId.Id);
        Assert.Equal("Failed to save obligation calculation", entry.Message);
    }

    [Fact]
    public async Task AddVersionAsync_WhenContentIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var service = Service(
            Substitute.For<IObligationCalculationRepository>(),
            new CapturingLogger<ObligationCalculationService>());

        // Act
        Task<ObligationCalculationModel> Act() => service.AddVersionAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Act);
    }

    [Fact]
    public async Task AddVersionAsync_WhenCurrentVersionExists_SavesTheNextVersionAndLogsIt()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var content = Content(tonnes: "1800.250");
        var current = Stored(content, 4);
        repository
            .FindCurrentAsync(content.SchemeOrgId, content.CompliancePeriodYear, Arg.Any<CancellationToken>())
            .Returns(current);
        repository
            .SupersedeCurrentAsync(session, current.Id, s_utc, Arg.Any<CancellationToken>())
            .Returns(true);
        var logger = new CapturingLogger<ObligationCalculationService>();
        var service = Service(repository, logger, TransactionFor<ObligationCalculationModel>(session), new FixedTime(s_utc));

        // Act
        var calculation = await service.AddVersionAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, calculation.Version);
        Assert.Equal(current.Id, calculation.SupersedesCalculationId);
        Assert.NotEqual(current.Id, calculation.Id);
        await repository.Received(1).SupersedeCurrentAsync(session, current.Id, s_utc, Arg.Any<CancellationToken>());
        await repository.Received(1).CreateAsync(
            session,
            Arg.Is<ObligationCalculationModel>(saved =>
                saved.Id == calculation.Id
                && saved.Version == 5
                && saved.SupersedesCalculationId == current.Id
                && saved.ObligationTonnes == "1800.250"),
            Arg.Any<CancellationToken>());
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(
            $"Stored obligation calculation {calculation.Id} version 5 superseding {current.Id} for scheme {calculation.SchemeOrgId} compliance year 2026",
            entry.Message);
    }

    [Fact]
    public async Task AddVersionAsync_WhenTheCurrentVersionWasAlreadyReplaced_ThrowsValidationExceptionAndDoesNotInsert()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var content = Content();
        repository
            .FindCurrentAsync(content.SchemeOrgId, content.CompliancePeriodYear, Arg.Any<CancellationToken>())
            .Returns(Stored(content, 1));
        repository
            .SupersedeCurrentAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var logger = new CapturingLogger<ObligationCalculationService>();
        var service = Service(repository, logger, TransactionFor<ObligationCalculationModel>(session));

        // Act
        Task<ObligationCalculationModel> Act() => service.AddVersionAsync(content, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.CurrentCalculationChanged);
        await repository.DidNotReceive().CreateAsync(
            Arg.Any<IClientSessionHandle>(),
            Arg.Any<ObligationCalculationModel>(),
            Arg.Any<CancellationToken>());
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task AddVersionAsync_WhenNoCurrentVersionExists_ThrowsValidationException()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = Service(repository, new CapturingLogger<ObligationCalculationService>(), unitOfWork);

        // Act
        Task<ObligationCalculationModel> Act() => service.AddVersionAsync(Content(), TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.CurrentCalculationMissing);
        await unitOfWork.DidNotReceive().ExecuteAsync(
            Arg.Any<Func<IMongoTransaction, CancellationToken, Task<ObligationCalculationModel>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddVersionAsync_WhenTheInsertConflicts_ThrowsValidationException()
    {
        // Arrange
        var repository = Substitute.For<IObligationCalculationRepository>();
        var session = Substitute.For<IClientSessionHandle>();
        var content = Content();
        repository
            .FindCurrentAsync(content.SchemeOrgId, content.CompliancePeriodYear, Arg.Any<CancellationToken>())
            .Returns(Stored(content, 1));
        repository
            .SupersedeCurrentAsync(Arg.Any<IClientSessionHandle>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        repository
            .CreateAsync(session, Arg.Any<ObligationCalculationModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateKeyOnSaveException(new Exception("duplicate")));
        var service = Service(
            repository,
            new CapturingLogger<ObligationCalculationService>(),
            TransactionFor<ObligationCalculationModel>(session));

        // Act
        Task<ObligationCalculationModel> Act() => service.AddVersionAsync(content, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains(exception.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.CurrentCalculationChanged);
    }

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
        ITimeProvider? time = null) =>
        new(
            repository,
            unitOfWork ?? Substitute.For<IUnitOfWork>(),
            time ?? new FixedTime(s_utc),
            new ObligationCalculationMapper(),
            new ObligationCalculationValidator(),
            logger);

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
