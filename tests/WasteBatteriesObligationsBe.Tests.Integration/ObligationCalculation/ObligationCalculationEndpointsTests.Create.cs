using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WasteBatteriesObligationsBe.ObligationCalculation.Endpoints;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.Tests.Integration.Utils;

namespace WasteBatteriesObligationsBe.Tests.Integration.ObligationCalculation;

public partial class ObligationCalculationEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Create_WhenCalculationIsValid_SavesItAndLogsIt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = PathFor(CreateSchemeOrgId);
        using var client = fixture.CreateClient();

        // Act
        var response = await client.PostAsync(path, null, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var created = JsonSerializer.Deserialize<ObligationCalculationModel>(json, JsonOptions);
        var stored = await Stored(CreateSchemeOrgId, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal($"{ObligationCalculationEndpoints.RoutePrefix}/{created.Id}", response.Headers.Location?.OriginalString);

        var document = Assert.Single(stored);
        var log = Assert.Single(fixture.Logger.Entries, entry => entry.Message.Contains(created.Id, StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal(4, log.EventId.Id);
        Assert.Equal(
            $"Stored obligation calculation {created.Id} version 1 for scheme {CreateSchemeOrgId} compliance year {CompliancePeriodYear}",
            log.Message);

        Snapshot.Match(ResponseJson(json, PinnedCalculationId), "response");
        Snapshot.Match(StoredJson(document, PinnedCalculationId));
    }

    [Fact]
    public async Task Create_WhenSchemeIsOmitted_StoresACalculation()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();

        // Act
        var response = await client.PostAsync(ObligationCalculationEndpoints.RoutePrefix, null, cancellationToken);
        var created = await response.Content.ReadFromJsonAsync<ObligationCalculationModel>(JsonOptions, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(1, created.Version);
        Assert.Equal(CompliancePeriodYear, created.CompliancePeriodYear);
        Assert.True(ObjectId.TryParse(created.SchemeOrgId, out _));
        Assert.Single(await Stored(created.SchemeOrgId, cancellationToken));
    }

    [Fact]
    public async Task Create_WhenCurrentVersionExists_LeavesTheOriginal()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = PathFor(ConflictSchemeOrgId);
        using var client = fixture.CreateClient();
        await client.PostAsync(path, null, cancellationToken);

        // Act
        var response = await client.PostAsync(path, null, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var after = await Stored(ConflictSchemeOrgId, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var document = Assert.Single(after);
        Snapshot.Match(ProblemJson(json), "problem");
        Snapshot.Match(StoredJson(document, PinnedCalculationId), "stored");
    }

    [Fact]
    public async Task Create_WhenCredentialsAreMissing_ReturnsUnauthorized()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateAnonymousClient();

        // Act
        var response = await client.PostAsync(ObligationCalculationEndpoints.RoutePrefix, null, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string PathFor(string schemeOrgId) =>
        $"{ObligationCalculationEndpoints.RoutePrefix}?schemeOrgId={schemeOrgId}&compliancePeriodYear={CompliancePeriodYear}";
}
