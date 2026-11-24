using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Endpoints;

public static class ObligationCalculationEndpoints
{
    public const string RoutePrefix = "/obligation-calculations";

    // Used when the caller does not name a year. A fixed year keeps the sample repeatable.
    private const int SampleCompliancePeriodYear = 2026;

    public static RouteGroupBuilder MapObligationCalculationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(RoutePrefix)
            .WithTags("Obligation calculations");

        group.MapPost(string.Empty, Create)
            .WithName("CreateObligationCalculation")
            .WithSummary("Store a sample obligation calculation")
            .ProducesValidationProblem();

        group.MapPatch(string.Empty, AddVersion)
            .WithName("AddObligationCalculationVersion")
            .WithSummary("Store a newer sample version of an obligation calculation")
            .ProducesValidationProblem();

        return group;
    }

    // TEMPORARY: Remove when a calculation is created from POM submission events.
    private static async Task<Created<ObligationCalculationModel>> Create(
        [AsParameters] ObligationCalculationKey key,
        [FromServices] IObligationCalculationService obligationCalculationService,
        CancellationToken cancellationToken)
    {
        var calculation = await obligationCalculationService.CreateAsync(Sample(key, salt: 1), cancellationToken);
        return TypedResults.Created($"{RoutePrefix}/{calculation.Id}", calculation);
    }

    // TEMPORARY: Remove when a new version is created from POM submission events.
    private static async Task<Ok<ObligationCalculationModel>> AddVersion(
        [AsParameters] ObligationCalculationKey key,
        [FromServices] IObligationCalculationService obligationCalculationService,
        CancellationToken cancellationToken)
    {
        var calculation = await obligationCalculationService.AddVersionAsync(Sample(key, salt: 2), cancellationToken);
        return TypedResults.Ok(calculation);
    }

    // TEMPORARY: remove whole region when Create & AddVersion endpoints are removed
    #region Sample calculation
    // A made-up calculation, so the collection can be tried before event processing exists.
    // The same scheme, year and salt always produce the same figures. A new version uses another salt.
    // The obligation total and the current-year scale are not the real calculation.
    private static ObligationCalculationBase Sample(ObligationCalculationKey key, int salt)
    {
        var schemeOrgId = string.IsNullOrWhiteSpace(key.SchemeOrgId)
            ? ObjectId.GenerateNewId().ToString()
            : key.SchemeOrgId;
        var year = key.CompliancePeriodYear ?? SampleCompliancePeriodYear;
        var random = RandomFor(schemeOrgId, year, salt);
        var currentTonnes = Tonnes(random);
        return new ObligationCalculationBase
        {
            SchemeOrgId = schemeOrgId,
            SchemeName = "Sample scheme",
            CompliancePeriodYear = year,
            ObligationTonnes = Tonnes(random),
            PlacedOnMarketByYear =
            [
                Year(year - 2, Tonnes(random), annualised: false, random),
                Year(year - 1, Tonnes(random), annualised: false, random),
                Year(year, currentTonnes, annualised: true, random)
            ],
            Parameters = new ObligationParameters
            {
                Target = new ObligationTarget { Type = ObligationTarget.Percentage, Value = "45.00" },
                WindowYears = 3,
                DenominatorRule = ObligationParameters.YearsWithData
            },
            Trigger = random.Next(2) == 0 ? EventTrigger(random) : ManualTrigger()
        };
    }

    // HashCode changes between processes. The seed has to stay put so a stored snapshot can be compared.
    private static Random RandomFor(string schemeOrgId, int year, int salt)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{schemeOrgId}|{year}|{salt}"));
        var seed = hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24);
        return new Random(seed);
    }

    private static PlacedOnMarketYear Year(int year, string tonnes, bool annualised, Random random)
    {
        var annualisedTonnes = annualised ? DifferentTonnes(random, tonnes) : tonnes;
        return new PlacedOnMarketYear
        {
            Year = year,
            Tonnes = tonnes,
            QuartersOfData = annualised ? 2 : 4,
            AnnualisedTonnes = annualisedTonnes,
            IsAnnualisedEstimate = annualised,
            SourceSubmissionIds = [SubmissionId(random)]
        };
    }

    private static string SubmissionId(Random random)
    {
        var bytes = new byte[12];
        random.NextBytes(bytes);
        return new ObjectId(bytes).ToString();
    }

    private static string Tonnes(Random random) =>
        string.Create(CultureInfo.InvariantCulture, $"{random.Next(0, 10000)}.{random.Next(0, 1000):000}");

    private static string DifferentTonnes(Random random, string tonnes)
    {
        var other = Tonnes(random);
        if (other != tonnes)
        {
            return other;
        }

        var point = tonnes.IndexOf('.');
        var whole = int.Parse(tonnes.AsSpan(0, point), CultureInfo.InvariantCulture);
        var fraction = int.Parse(tonnes.AsSpan(point + 1), CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{whole}.{(fraction + 1) % 1000:000}");
    }

    private static ObligationTrigger EventTrigger(Random random) => new()
    {
        Type = ObligationTrigger.Event,
        EventId = $"evt-{random.Next(1000, 10000)}",
        CorrelationId = $"corr-{random.Next(1000, 10000)}"
    };

    private static ObligationTrigger ManualTrigger() => new()
    {
        Type = ObligationTrigger.Manual,
        Actor = "regulator.user@example.com",
        Reason = "Sample recalculation"
    };
    #endregion
}

// TEMPORARY: remove when Create & AddVersion endpoints are removed
// Optional scheme and compliance year for a sample calculation.
public sealed record ObligationCalculationKey
{
    [FromQuery(Name = "schemeOrgId")]
    public string? SchemeOrgId { get; init; }

    [FromQuery(Name = "compliancePeriodYear")]
    public int? CompliancePeriodYear { get; init; }
}
