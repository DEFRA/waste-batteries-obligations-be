using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using WasteBatteriesObligationsBe.ExampleData.Models;
using WasteBatteriesObligationsBe.ExampleData.Repositories;
using WasteBatteriesObligationsBe.ExampleData.Services;
using WasteBatteriesObligationsBe.ExampleData.Validators;

namespace WasteBatteriesObligationsBe.ExampleData;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExampleDataServices(this IServiceCollection services)
    {
        services.AddSingleton<IExampleDataRepository, ExampleDataRepository>();
        services.AddScoped<IExampleDataService, ExampleDataService>();
        services.AddScoped<IValidator<CreateExampleDataRequest>, CreateExampleDataRequestValidator>();
        services.AddScoped<IValidator<ExampleDataUser>, ExampleDataUserValidator>();
        return services;
    }
}
