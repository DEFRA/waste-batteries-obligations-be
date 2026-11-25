using FluentValidation;
using System.Diagnostics.CodeAnalysis;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.ObligationCalculation.Mapping;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;

namespace WasteBatteriesObligationsBe.ObligationCalculation;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObligationCalculationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ObligationCalculationConfig>()
            .Bind(configuration.GetRequiredSection(ObligationCalculationConfig.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                config => ObligationCalculationPatterns.TargetValuePattern().IsMatch(config.TargetPercentage ?? string.Empty),
                "ObligationCalculation:TargetPercentage must have exactly 2 decimal places.")
            .Validate(
                config => config.DenominatorRule == ObligationParameters.YearsWithData,
                "ObligationCalculation:DenominatorRule must be yearsWithData.")
            .ValidateOnStart();

        services.AddSingleton<IObligationCalculationRepository, ObligationCalculationRepository>();
        services.AddSingleton<IObligationCalculationMapper, ObligationCalculationMapper>();
        services.AddScoped<IValidator<ObligationCalculationBase>, ObligationCalculationBaseValidator>();
        services.AddScoped<IValidator<CalculationData>, CalculationDataValidator>();
        services.AddScoped<ObligationCalculationDependencies>();
        services.AddScoped<IObligationCalculationService, ObligationCalculationService>();
        return services;
    }
}
