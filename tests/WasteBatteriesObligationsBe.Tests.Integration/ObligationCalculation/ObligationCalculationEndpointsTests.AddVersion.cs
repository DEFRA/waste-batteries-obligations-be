using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Net;
using System.Net.Http.Json;
using WasteBatteriesObligationsBe.ObligationCalculation.Endpoints;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.ObligationCalculation.Validators;
using WasteBatteriesObligationsBe.Tests.Integration.Utils;

namespace WasteBatteriesObligationsBe.Tests.Integration.ObligationCalculation;

public partial class ObligationCalculationEndpointsTests
{
    [Fact]
    public async Task AddVersion_WhenCurrentVersionExists_KeepsTheFirstCalculationAndReadsTheLatest()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = PathFor(VersionSchemeOrgId);
        using var client = fixture.CreateClient();
        var created = await client.PostAsJsonAsync(path, new { }, cancellationToken);
        var first = await created.Content.ReadFromJsonAsync<ObligationCalculationModel>(JsonOptions, cancellationToken);

        // Act
        var response = await client.PatchAsync(path, null, cancellationToken);
        var added = await response.Content.ReadFromJsonAsync<ObligationCalculationModel>(JsonOptions, cancellationToken);
        var repository = fixture.Services.GetRequiredService<IObligationCalculationRepository>();
        var history = await repository.FindBySchemeYearAsync(VersionSchemeOrgId, CompliancePeriodYear, cancellationToken);
        var current = await repository.FindCurrentAsync(VersionSchemeOrgId, CompliancePeriodYear, cancellationToken);
        var documents = await Stored(VersionSchemeOrgId, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(added);
        Assert.Equal([2, 1], history.Select(calculation => calculation.Version).ToArray());
        Assert.Equal(first.Id, history[1].Id);
        Assert.Equal(added.Id, current!.Id);

        var log = Assert.Single(fixture.Logger.Entries, entry => entry.Message.Contains(added.Id, StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal(5, log.EventId.Id);
        Assert.Equal(
            $"Stored obligation calculation {added.Id} version 2 superseding {first.Id} for scheme {VersionSchemeOrgId} compliance year {CompliancePeriodYear}",
            log.Message);

        Assert.Equal(2, documents.Count);
        Snapshot.Match(StoredPair(documents[0], documents[1]));
    }

    [Fact]
    public async Task AddVersion_WhenNoCurrentVersionExists_ReturnsBadRequest()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var schemeOrgId = ObjectId.GenerateNewId().ToString();
        using var client = fixture.CreateClient();

        // Act
        var response = await client.PatchAsync(
            PathFor(schemeOrgId),
            null,
            cancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
        var stored = await Stored(schemeOrgId, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(
            problem.Errors["ObligationCalculation"],
            message => message == ObligationCalculationValidationMessages.CurrentCalculationMissing);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task AddVersion_WhenCredentialsAreMissing_ReturnsUnauthorized()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateAnonymousClient();

        // Act
        var response = await client.PatchAsync(ObligationCalculationEndpoints.RoutePrefix, null, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
