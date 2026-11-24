using MongoDB.Bson;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Validators;

public class ObligationCalculationValidatorTests
{
    private readonly ObligationCalculationValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_WhenCalculationIsComplete_ReturnsValid()
    {
        // Arrange
        var content = Content();

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenWindowDoesNotMatchTheYears_RejectsTheList()
    {
        // Arrange
        var content = Content();
        content.PlacedOnMarketByYear.RemoveAt(0);

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.PlacedOnMarketRequired);
    }

    [Fact]
    public async Task ValidateAsync_WhenTheAnnualisedFlagDisagrees_RejectsTheYear()
    {
        // Arrange
        var content = Content();
        content.PlacedOnMarketByYear[2] = content.PlacedOnMarketByYear[2] with { IsAnnualisedEstimate = false };

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.AnnualisedEstimateMismatch);
    }

    [Fact]
    public async Task ValidateAsync_WhenAYearIsRepeated_RejectsTheList()
    {
        // Arrange
        var content = Content();
        content.PlacedOnMarketByYear.Add(content.PlacedOnMarketByYear[2]);

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.PlacedOnMarketRequired);
    }

    [Fact]
    public async Task ValidateAsync_WhenAMissingYearHasTonnes_RejectsTheYear()
    {
        // Arrange
        var content = Content();
        content.PlacedOnMarketByYear[0] = content.PlacedOnMarketByYear[0] with { Tonnes = "1.000" };

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.MissingYearBlank);
    }

    [Fact]
    public async Task ValidateAsync_WhenAMissingYearNamesASubmission_RejectsTheYear()
    {
        // Arrange
        var content = Content();
        content.PlacedOnMarketByYear[0] = content.PlacedOnMarketByYear[0] with
        {
            SourceSubmissionIds = [ObjectId.GenerateNewId().ToString()]
        };

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.MissingYearBlank);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnEarlierYearIsScaled_RejectsTheYear()
    {
        // Arrange
        var content = Content();
        content.PlacedOnMarketByYear[1] = content.PlacedOnMarketByYear[1] with
        {
            AnnualisedTonnes = "8000.000",
            IsAnnualisedEstimate = true
        };

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.EarlierYearScaled);
    }

    [Fact]
    public async Task ValidateAsync_WhenAManualTriggerCarriesAnEventId_RejectsTheTrigger()
    {
        // Arrange
        var content = Content() with
        {
            Trigger = new ObligationTrigger
            {
                Type = ObligationTrigger.Manual,
                Actor = "regulator.user@example.com",
                Reason = "Sample recalculation",
                EventId = "evt-1"
            }
        };

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.EventIdNotAllowed);
    }

    [Fact]
    public async Task ValidateAsync_WhenSchemeAndTonnesAreWrong_ReturnsEveryError()
    {
        // Arrange
        var content = Content() with { SchemeOrgId = " ", ObligationTonnes = "2100.5" };

        // Act
        var result = await _validator.ValidateAsync(content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.SchemeOrgIdRequired);
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.ObligationTonnesFormat);
    }

    private static ObligationCalculationBase Content() => new()
    {
        SchemeOrgId = ObjectId.GenerateNewId().ToString(),
        SchemeName = "IronWave Compliance",
        CompliancePeriodYear = 2026,
        ObligationTonnes = "2100.500",
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
