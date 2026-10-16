using System.Diagnostics.CodeAnalysis;

namespace WasteBatteriesObligationsBe.Utils.Health;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }
}
