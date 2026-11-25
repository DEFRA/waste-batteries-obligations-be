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
    private const string CalculateRoute = $"{ObligationCalculationEndpoints.RoutePrefix}/calculate";

    [Fact]
    public async Task Calculate_WhenSubmissionsAreInTheWindow_StoresTheObligationAndABlankSchemeName()
    {
        // Arrange
        // Four quarters of 1,000 tonnes, and the two preceding years missing. 4,000 × 45% = 1,800.000
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();
        var request = Calculation(CalculateSchemeOrgId, s_firstSubmissionIds, tonnes: "1000.000");

        // Act
        var response = await client.PostAsJsonAsync(CalculateRoute, request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var stored = await Stored(CalculateSchemeOrgId, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(json);
        var id = body.RootElement.GetProperty("id").GetString();
        Assert.Equal($"{ObligationCalculationEndpoints.RoutePrefix}/{id}", response.Headers.Location?.OriginalString);
        var document = Assert.Single(stored);
        Snapshot.Match(ResponseJson(json, PinnedCalculationId), "response");
        Snapshot.Match(StoredJson(document, PinnedCalculationId), "stored");
    }

    [Fact]
    public async Task Calculate_WhenCalledAgain_StoresTheNextVersion()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();
        await client.PostAsJsonAsync(
            CalculateRoute,
            Calculation(CalculateAgainSchemeOrgId, s_firstSubmissionIds, tonnes: "1000.000"),
            cancellationToken);

        // Act
        var response = await client.PostAsJsonAsync(
            CalculateRoute,
            Calculation(CalculateAgainSchemeOrgId, s_secondSubmissionIds, tonnes: "2000.000"),
            cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var stored = await Stored(CalculateAgainSchemeOrgId, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(json);
        var id = body.RootElement.GetProperty("id").GetString();
        Assert.Equal($"{ObligationCalculationEndpoints.RoutePrefix}/{id}", response.Headers.Location?.OriginalString);
        Snapshot.Match(ResponseJson(json, PinnedReplacementId, PinnedCalculationId), "response");
        Snapshot.Match(StoredPair(stored[0], stored[1]), "stored");
    }

    [Fact]
    public async Task Calculate_WhenThereAreNoSubmissions_ReturnsValidationProblem()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();
        var request = new CalculationData
        {
            CompliancePeriodYear = CompliancePeriodYear,
            Trigger = ManualTrigger()
        };

        // Act
        var response = await client.PostAsJsonAsync(CalculateRoute, request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Snapshot.Match(ProblemJson(json));
    }

    [Fact]
    public async Task Calculate_WhenCredentialsAreMissing_ReturnsUnauthorized()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = fixture.CreateAnonymousClient();

        // Act
        var response = await client.PostAsJsonAsync(CalculateRoute, new { }, cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static readonly string[] s_firstSubmissionIds =
    [
        "674a1b2c3d4e5f6789012401",
        "674a1b2c3d4e5f6789012402",
        "674a1b2c3d4e5f6789012403",
        "674a1b2c3d4e5f6789012404"
    ];

    private static readonly string[] s_secondSubmissionIds =
    [
        "674a1b2c3d4e5f6789012411",
        "674a1b2c3d4e5f6789012412",
        "674a1b2c3d4e5f6789012413",
        "674a1b2c3d4e5f6789012414"
    ];

    private static CalculationData Calculation(
        string schemeOrgId,
        string[]? submissionIds = null,
        string tonnes = "1000.000")
    {
        submissionIds ??= Enumerable.Range(0, 4).Select(_ => ObjectId.GenerateNewId().ToString()).ToArray();
        var submissions = new List<PomSubmissionForCalculation>(submissionIds.Length);
        for (var index = 0; index < submissionIds.Length; index++)
        {
            submissions.Add(new PomSubmissionForCalculation
            {
                Id = submissionIds[index],
                SchemeOrgId = schemeOrgId,
                CompliancePeriodYear = CompliancePeriodYear,
                CompliancePeriodQuarter = PomSubmissionForCalculation.Quarters[index],
                Tonnes = [tonnes]
            });
        }

        return new CalculationData
        {
            CompliancePeriodYear = CompliancePeriodYear,
            Submissions = submissions,
            Trigger = ManualTrigger()
        };
    }

    private static ObligationTrigger ManualTrigger() => new()
    {
        Type = ObligationTrigger.Manual,
        Actor = "regulator.user@example.com",
        Reason = "Check the calculation"
    };
}
