using Microsoft.Extensions.Options;
using MongoDB.Bson;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Validators;

public class CalculationDataValidatorTests
{
    private readonly CalculationDataValidator _validator = new(Options.Create(Config()));

    [Fact]
    public async Task ValidateAsync_WhenTheDataIsComplete_ReturnsValid()
    {
        // Arrange
        var request = Data(Submission(2026));

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenThereAreNoSubmissions_RejectsTheData()
    {
        // Arrange
        var request = Data();

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == CalculationDataValidationMessages.SubmissionsRequired);
    }

    [Fact]
    public async Task ValidateAsync_WhenTheSchemesDiffer_RejectsTheData()
    {
        // Arrange
        var first = Submission(2026);
        var second = Submission(2026, schemeOrgId: ObjectId.GenerateNewId().ToString());
        var request = Data(first, second);

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            "All submissions must be for the same scheme. "
            + $"submissions[0] (id {first.Id}) has schemeOrgId {first.SchemeOrgId}; "
            + $"submissions[1] (id {second.Id}) has schemeOrgId {second.SchemeOrgId}.");
    }

    [Fact]
    public async Task ValidateAsync_WhenAYearIsOutsideTheWindow_RejectsTheSubmission()
    {
        // Arrange
        // A window of 3 accepts 2026, 2025 and 2024. 2023 is the year before that.
        var outside = Submission(2023);
        var request = Data(outside, Submission(2024), Submission(2025), Submission(2026));

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            $"submissions[0] (id {outside.Id}): {CalculationDataValidationMessages.SubmissionYearOutsideWindow}");
    }

    [Fact]
    public async Task ValidateAsync_WhenTheWindowIsTwoYears_RejectsTheYearBeforeThat()
    {
        // Arrange
        var validator = new CalculationDataValidator(Options.Create(Config(windowYears: 2)));
        var outside = Submission(2024);
        var request = Data(Submission(2026), outside);

        // Act
        var result = await validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            $"submissions[1] (id {outside.Id}): {CalculationDataValidationMessages.SubmissionYearOutsideWindow}");
    }

    [Fact]
    public async Task ValidateAsync_WhenThePrecedingYearsAreInsideTheWindow_ReturnsValid()
    {
        // Arrange
        var request = Data(Submission(2024), Submission(2025), Submission(2026));

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenTheSameSubmissionIsRepeated_RejectsTheData()
    {
        // Arrange
        var submission = Submission(2026);
        var request = Data(submission, submission);

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            $"Each submission can be included once. id {submission.Id} at submissions[0], submissions[1].");
    }

    [Fact]
    public async Task ValidateAsync_WhenASubmissionIsNull_RejectsThatIndex()
    {
        // Arrange
        var request = Data(Submission(2026), null!);

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            $"submissions[1]: {CalculationDataValidationMessages.SubmissionRequired}");
    }

    [Fact]
    public async Task ValidateAsync_WhenTheQuarterIsNotAComplianceQuarter_RejectsTheSubmission()
    {
        // Arrange
        var submission = Submission(2026) with { CompliancePeriodQuarter = "Q1" };
        var request = Data(submission);

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            $"submissions[0] (id {submission.Id}): {CalculationDataValidationMessages.QuarterUnknown}");
    }

    [Fact]
    public async Task ValidateAsync_WhenTonnesHaveTheWrongScale_RejectsTheSubmission()
    {
        // Arrange
        var submission = Submission(2026) with { Tonnes = ["1.000", "1.5"] };
        var request = Data(submission);

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            $"submissions[0] (id {submission.Id}), tonnes[1]: {CalculationDataValidationMessages.TonnesFormat}");
    }

    [Fact]
    public async Task ValidateAsync_WhenAnEventTriggerOmitsTheEventId_RejectsTheTrigger()
    {
        // Arrange
        var request = Data(Submission(2026)) with
        {
            Trigger = new ObligationTrigger
            {
                Type = ObligationTrigger.Event,
                CorrelationId = "corr-1"
            }
        };

        // Act
        var result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.ErrorMessage == ObligationCalculationValidationMessages.EventIdRequired);
    }

    private static CalculationData Data(params PomSubmissionForCalculation[] submissions) => new()
    {
        CompliancePeriodYear = 2026,
        Submissions = [.. submissions],
        Trigger = new ObligationTrigger
        {
            Type = ObligationTrigger.Manual,
            Actor = "regulator.user@example.com",
            Reason = "Check the calculation"
        }
    };

    private static PomSubmissionForCalculation Submission(int year, string? schemeOrgId = null) => new()
    {
        Id = ObjectId.GenerateNewId().ToString(),
        SchemeOrgId = schemeOrgId ?? s_schemeOrgId,
        CompliancePeriodYear = year,
        CompliancePeriodQuarter = PomSubmissionForCalculation.Quarter1,
        Tonnes = ["1.000"]
    };

    private static ObligationCalculationConfig Config(int windowYears = 3) => new()
    {
        TargetPercentage = "45.00",
        WindowYears = windowYears,
        DenominatorRule = ObligationParameters.YearsWithData
    };

    private static readonly string s_schemeOrgId = ObjectId.GenerateNewId().ToString();
}
