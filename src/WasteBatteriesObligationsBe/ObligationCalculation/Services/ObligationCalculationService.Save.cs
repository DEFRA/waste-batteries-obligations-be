using FluentValidation;
using FluentValidation.Results;
using MongoDB.Bson;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;
using WasteBatteriesObligationsBe.Utils.Logging;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Services;

// Create rejects a current version. AddVersion requires one and appends the next version.
// The new version points at the one it replaces. The previous document keeps its figure.
// The same transaction clears isCurrent and sets updatedAtUtc.
public partial class ObligationCalculationService
{
    public async Task<ObligationCalculationModel> CreateAsync(
        ObligationCalculationBase calculation,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(calculation, cancellationToken);
        if (prepared.Current is not null)
        {
            prepared.Failures.Add(CalculationFailure(ObligationCalculationValidationMessages.CurrentCalculationExists));
        }

        if (prepared.Failures.Count > 0)
        {
            throw new ValidationException(prepared.Failures);
        }

        var calculatedAtUtc = timeProvider.UtcNow;
        var stored = mapper.ToNewCalculation(prepared.Calculation, calculatedAtUtc);

        try
        {
            // The id and the timestamp are allocated above. The transaction callback can run more than once.
            await ExecuteSaveAsync(
                async (transaction, transactionCancellationToken) =>
                {
                    await repository.CreateAsync(transaction.Session, stored, transactionCancellationToken);
                    return stored;
                },
                cancellationToken);
        }
        catch (DuplicateKeyOnSaveException)
        {
            throw new ValidationException(
            [
                CalculationFailure(ObligationCalculationValidationMessages.CurrentCalculationExists)
            ]);
        }

        logger.StoredObligationCalculation(
            stored.Id,
            stored.Version,
            stored.SchemeOrgId,
            stored.CompliancePeriodYear);
        return stored;
    }

    public async Task<ObligationCalculationModel> AddVersionAsync(
        ObligationCalculationBase calculation,
        CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(calculation, cancellationToken);
        if (CanLookUpCurrentCalculation(prepared.Calculation) && prepared.Current is null)
        {
            prepared.Failures.Add(CalculationFailure(ObligationCalculationValidationMessages.CurrentCalculationMissing));
        }

        if (prepared.Failures.Count > 0)
        {
            throw new ValidationException(prepared.Failures);
        }

        var current = prepared.Current!;
        var calculatedAtUtc = timeProvider.UtcNow;
        var stored = mapper.ToNewCalculation(
            prepared.Calculation,
            calculatedAtUtc,
            version: current.Version + 1,
            supersedesCalculationId: current.Id);

        try
        {
            await ExecuteSaveAsync(
                async (transaction, transactionCancellationToken) =>
                {
                    await WriteVersionAsync(
                        transaction,
                        current.Id,
                        stored,
                        calculatedAtUtc,
                        transactionCancellationToken);
                    return stored;
                },
                cancellationToken);
        }
        catch (DuplicateKeyOnSaveException)
        {
            throw new ValidationException(
            [
                CalculationFailure(ObligationCalculationValidationMessages.CurrentCalculationChanged)
            ]);
        }

        logger.StoredObligationCalculationVersion(
            stored.Id,
            stored.Version,
            current.Id,
            stored.SchemeOrgId,
            stored.CompliancePeriodYear);
        return stored;
    }

    // Trims the calculation, checks its shape, and reads the current version when the key is usable.
    // A shape failure is logged as an error: the caller is event processing, not a person correcting a form.
    private async Task<PreparedCalculation> PrepareAsync(
        ObligationCalculationBase calculation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        calculation = calculation.Trimmed();
        var result = await validator.ValidateAsync(calculation, cancellationToken);
        var failures = new List<ValidationFailure>(result.Errors);
        if (failures.Count > 0)
        {
            logger.ObligationCalculationInvalid(ValidationErrors(failures));
        }

        ObligationCalculationModel? current = null;
        if (CanLookUpCurrentCalculation(calculation))
        {
            current = await repository.FindCurrentAsync(calculation.SchemeOrgId, calculation.CompliancePeriodYear, cancellationToken);
        }

        return new PreparedCalculation(calculation, failures, current);
    }

    private static bool CanLookUpCurrentCalculation(ObligationCalculationBase calculation) =>
        ObjectId.TryParse(calculation.SchemeOrgId, out _) && CalendarYear.Contains(calculation.CompliancePeriodYear);

    private static string ValidationErrors(IReadOnlyList<ValidationFailure> failures) =>
        string.Join("; ", failures.Select(failure => $"{failure.PropertyName}: {failure.ErrorMessage}"));

    // The unique index allows one current version for the scheme and year, so the current row is cleared
    // before the next version is inserted. Both writes use the caller's transaction.
    private async Task WriteVersionAsync(
        IMongoTransaction transaction,
        string currentId,
        ObligationCalculationModel calculation,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var superseded = await repository.SupersedeCurrentAsync(
            transaction.Session,
            currentId,
            updatedAtUtc,
            cancellationToken);

        if (!superseded)
        {
            throw new ValidationException(
            [
                CalculationFailure(ObligationCalculationValidationMessages.CurrentCalculationChanged)
            ]);
        }

        await repository.CreateAsync(transaction.Session, calculation, cancellationToken);
    }

    private sealed record PreparedCalculation(
        ObligationCalculationBase Calculation,
        List<ValidationFailure> Failures,
        ObligationCalculationModel? Current);
}
