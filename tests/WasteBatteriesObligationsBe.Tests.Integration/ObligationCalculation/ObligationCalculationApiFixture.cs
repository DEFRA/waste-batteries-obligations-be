using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.ObligationCalculation.Services;
using WasteBatteriesObligationsBe.Tests.Common;
using WasteBatteriesObligationsBe.Tests.Integration.Utils;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Integration.ObligationCalculation;

public sealed class ObligationCalculationApiFixture : MongoApiFixture
{
    public static readonly DateTime StartedAt = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);

    public ObligationCalculationApiFixture()
        : base("obligation_it")
    {
    }

    public SteppingTime Clock { get; } = new(StartedAt);

    public CapturingLogger<ObligationCalculationService> Logger { get; } = new();

    public IServiceProvider Services => Factory.Services;

    public IMongoCollection<BsonDocument> Calculations => Collection(ObligationCalculationRepository.CollectionName);

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<ITimeProvider>();
        services.AddSingleton<ITimeProvider>(Clock);
        services.RemoveAll<ILogger<ObligationCalculationService>>();
        services.AddSingleton<ILogger<ObligationCalculationService>>(Logger);
    }

    protected override void OnStarted()
    {
        _ = Factory.Services.GetRequiredService<IObligationCalculationRepository>();
    }
}
