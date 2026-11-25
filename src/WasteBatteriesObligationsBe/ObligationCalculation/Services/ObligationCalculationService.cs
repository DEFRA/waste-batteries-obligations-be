using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.Utils.Logging;
using WasteBatteriesObligationsBe.Utils.Mongo;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Services;

public interface IObligationCalculationService
{
    Task<ObligationCalculationModel> CreateAsync(
        ObligationCalculationBase calculation,
        CancellationToken cancellationToken = default);

    Task<ObligationCalculationModel> AddVersionAsync(
        ObligationCalculationBase calculation,
        CancellationToken cancellationToken = default);

    // Builds the obligation from the submissions and stores it.
    // The first calculation for the scheme and year is version 1. A later call stores the next version.
    Task<ObligationCalculationModel> CalculateAsync(
        CalculationData calculation,
        CancellationToken cancellationToken = default);
}

// Creates and new versions are in the Save partial.
// The calculation from POM submissions is in the Calculate partial.
// These helpers are shared by the writes.
public partial class ObligationCalculationService(
    IObligationCalculationRepository repository,
    IUnitOfWork unitOfWork,
    ITimeProvider timeProvider,
    IObligationCalculationMapper mapper,
    IValidator<ObligationCalculationBase> validator,
    IValidator<CalculationData> calculationDataValidator,
    IOptions<ObligationCalculationConfig> calculationOptions,
    ILogger<ObligationCalculationService> logger) : IObligationCalculationService
{
    private static ValidationFailure CalculationFailure(string message) =>
        new("ObligationCalculation", message);

    // Validation and a duplicate key are the caller's response. Any other failure is rethrown and logged.
    private async Task<T> ExecuteSaveAsync<T>(
        Func<IMongoTransaction, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(work, cancellationToken);
        }
        catch (Exception exception) when (exception is not ValidationException and not DuplicateKeyOnSaveException)
        {
            logger.FailedToSaveObligationCalculation(exception);
            throw;
        }
    }
}
