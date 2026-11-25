using System.Diagnostics.CodeAnalysis;
using WasteBatteriesObligationsBe.Example.Endpoints;
using WasteBatteriesObligationsBe.ExampleData.Endpoints;
using WasteBatteriesObligationsBe.ObligationCalculation.Endpoints;

namespace WasteBatteriesObligationsBe.Endpoints;

[ExcludeFromCodeCoverage]
public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapObligationCalculationEndpoints().RequireAuthorization();

        // TEMPORARY: Remove before deploying
        app.MapExampleEndpoints().RequireAuthorization();
        app.MapExampleDataEndpoints().RequireAuthorization();

        return app;
    }
}
