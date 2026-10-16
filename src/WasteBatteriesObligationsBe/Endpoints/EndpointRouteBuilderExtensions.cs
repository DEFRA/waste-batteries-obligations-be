using System.Diagnostics.CodeAnalysis;
using WasteBatteriesObligationsBe.Example.Endpoints;
using WasteBatteriesObligationsBe.ExampleData.Endpoints;

namespace WasteBatteriesObligationsBe.Endpoints;

[ExcludeFromCodeCoverage]
public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // TODO: Remove before deploying
        app.MapExampleEndpoints().RequireAuthorization();
        app.MapExampleDataEndpoints().RequireAuthorization();

        return app;
    }
}
