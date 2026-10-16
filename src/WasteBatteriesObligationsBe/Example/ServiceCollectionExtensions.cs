using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using WasteBatteriesObligationsBe.Example.Models;
using WasteBatteriesObligationsBe.Example.Repositories;
using WasteBatteriesObligationsBe.Example.Services;
using WasteBatteriesObligationsBe.Example.Validators;

namespace WasteBatteriesObligationsBe.Example;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddExampleServices(this IServiceCollection services)
    {
        services.AddSingleton<IExampleRepository, ExampleRepository>();
        services.AddScoped<IExampleService, ExampleService>();
        services.AddScoped<IValidator<CreateExampleRequest>, CreateExampleRequestValidator>();
        services.AddScoped<IValidator<UpdateExampleRequest>, UpdateExampleRequestValidator>();
        return services;
    }
}
