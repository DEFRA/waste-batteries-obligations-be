using System.Diagnostics.CodeAnalysis;

namespace WasteBatteriesObligationsBe.Utils.Time;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimeProvider(this IServiceCollection services)
    {
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        return services;
    }
}
