using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Endpoints;

public static class ObligationCalculationEndpoints
{
    public const string RoutePrefix = "/obligation-calculations";

    // Used when the caller does not name a year. A fixed year keeps the sample repeatable.
    private const int SampleCompliancePeriodYear = 2026;

    public static RouteGroupBuilder MapObligationCalculationEndpoints(this IEndpointRouteBuilder app) =>
        app.MapObligationCalculationEndpoints(CdpEnvironment.IsProduction(CdpEnvironment.Read()));

    // TEMPORARY sample writes. CDP sets ASPNETCORE_ENVIRONMENT to Production in every environment,
    // so that name cannot tell production from dev. The platform name is the ENVIRONMENT variable,
    // and production is "prod". These routes are left off the table there.
    internal static RouteGroupBuilder MapObligationCalculationEndpoints(this IEndpointRouteBuilder app, bool isCdpProduction)
    {
        var group = app.MapGroup(RoutePrefix)
            .WithTags("Obligation calculations");

        if (isCdpProduction)
        {
            return group;
        }

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
        var random = NumbersFor(schemeOrgId, year, salt);
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

    // HashCode changes between processes. The same scheme, year and salt have to produce the same
    // figures so a stored snapshot can be compared. These are sample numbers, not a secret.
    private static SampleNumbers NumbersFor(string schemeOrgId, int year, int salt)
    {
        var material = Encoding.UTF8.GetBytes($"{schemeOrgId}|{year}|{salt}");
        return new SampleNumbers(SHA256.HashData(material));
    }

    private sealed class SampleNumbers(byte[] seed)
    {
        private int _offset;

        public int Next(int maxExclusive) => Next(0, maxExclusive);

        public int Next(int minInclusive, int maxExclusive)
        {
            var range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUnsigned() % range);
        }

        public void NextBytes(byte[] buffer)
        {
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = (byte)NextUnsigned();
            }
        }

        private uint NextUnsigned()
        {
            Span<byte> material = stackalloc byte[seed.Length + 4];
            seed.CopyTo(material);
            BinaryPrimitives.WriteInt32LittleEndian(material[seed.Length..], _offset++);
            Span<byte> digest = stackalloc byte[32];
            SHA256.HashData(material, digest);
            return BinaryPrimitives.ReadUInt32LittleEndian(digest);
        }
    }

    private static PlacedOnMarketYear Year(int year, string tonnes, bool annualised, SampleNumbers random)
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

    private static string SubmissionId(SampleNumbers random)
    {
        var bytes = new byte[12];
        random.NextBytes(bytes);
        return new ObjectId(bytes).ToString();
    }

    private static string Tonnes(SampleNumbers random) =>
        string.Create(CultureInfo.InvariantCulture, $"{random.Next(0, 10000)}.{random.Next(0, 1000):000}");

    private static string DifferentTonnes(SampleNumbers random, string tonnes)
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

    private static ObligationTrigger EventTrigger(SampleNumbers random) => new()
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
