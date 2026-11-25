using FluentValidation;
using Microsoft.Extensions.Options;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Services;

// A grouping of the obligation calculation dependencies.
public sealed class ObligationCalculationDependencies(
    IObligationCalculationRepository repository,
    IUnitOfWork unitOfWork,
    IObligationCalculationMapper mapper,
    IValidator<ObligationCalculationBase> validator,
    IValidator<CalculationData> calculationDataValidator,
    IOptions<ObligationCalculationConfig> config)
{
    public IObligationCalculationRepository Repository { get; } = repository;

    public IUnitOfWork UnitOfWork { get; } = unitOfWork;

    public IObligationCalculationMapper Mapper { get; } = mapper;

    public IValidator<ObligationCalculationBase> Validator { get; } = validator;

    public IValidator<CalculationData> CalculationDataValidator { get; } = calculationDataValidator;

    public IOptions<ObligationCalculationConfig> Config { get; } = config;
}
