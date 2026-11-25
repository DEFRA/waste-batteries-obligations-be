using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using WasteBatteriesObligationsBe.ObligationCalculation.Endpoints;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Endpoints;

public class ObligationCalculationEndpointsTests
{
    [Fact]
    public void MapObligationCalculationEndpoints_WhenCdpProduction_LeavesTemporaryRoutesOffTheTable()
    {
        // Arrange
        using var app = WebApplication.CreateBuilder().Build();

        // Act
        app.MapObligationCalculationEndpoints(isCdpProduction: true);
        var routes = Routes(app);

        // Assert
        Assert.Empty(routes);
    }

    [Fact]
    public void MapObligationCalculationEndpoints_WhenNotCdpProduction_MapsTemporaryRoutes()
    {
        // Arrange
        using var app = WebApplication.CreateBuilder().Build();

        // Act
        app.MapObligationCalculationEndpoints(isCdpProduction: false);
        var routes = Routes(app);

        // Assert
        Assert.Equal(
            [
                "PATCH /obligation-calculations/",
                "POST /obligation-calculations/",
                "POST /obligation-calculations/calculate"
            ],
            routes);
    }

    private static List<string> Routes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint =>
            {
                var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single();
                return $"{method} {endpoint.RoutePattern.RawText}";
            })
            .Order(StringComparer.Ordinal)
            .ToList();
}
