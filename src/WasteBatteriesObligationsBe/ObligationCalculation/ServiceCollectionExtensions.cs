using FluentValidation;
using System.Diagnostics.CodeAnalysis;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;

namespace WasteBatteriesObligationsBe.ObligationCalculation;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObligationCalculationServices(this IServiceCollection services)
    {
        services.AddSingleton<IObligationCalculationRepository, ObligationCalculationRepository>();
        services.AddSingleton<IObligationCalculationMapper, ObligationCalculationMapper>();
        services.AddScoped<IValidator<ObligationCalculationBase>, ObligationCalculationValidator>();
        services.AddScoped<IObligationCalculationService, ObligationCalculationService>();
        return services;
    }
}
