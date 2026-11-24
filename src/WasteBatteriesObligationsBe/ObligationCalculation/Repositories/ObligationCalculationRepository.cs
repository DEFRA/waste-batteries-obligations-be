using MongoDB.Driver;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Repositories;

public interface IObligationCalculationRepository
{
    // Inserts one version. The caller sets the id, version, isCurrent and calculatedAtUtc.
    Task CreateAsync(
        IClientSessionHandle session,
        ObligationCalculationModel calculation,
        CancellationToken cancellationToken = default);

    // Sets isCurrent to false and sets updatedAtUtc when that version is still current.
    // False means it was already updated, and nothing was written.
    Task<bool> SupersedeCurrentAsync(
        IClientSessionHandle session,
        string id,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);

    // The current version for the scheme and compliance year, or null when there is none.
    Task<ObligationCalculationModel?> FindCurrentAsync(
        string schemeOrgId,
        int compliancePeriodYear,
        CancellationToken cancellationToken = default);

    // Every version for the scheme and compliance year, latest version first.
    Task<IReadOnlyList<ObligationCalculationModel>> FindBySchemeYearAsync(
        string schemeOrgId,
        int compliancePeriodYear,
        CancellationToken cancellationToken = default);
}

// Appends obligation calculation versions. A new version is a new document.
// The only change to an older document is clearing isCurrent and setting updatedAtUtc.
// The figure, the inputs, the parameters and the trigger are never rewritten.
// Retention has no expiry, so this collection has no TTL index.
public class ObligationCalculationRepository(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<ObligationCalculationModel>(connectionFactory, CollectionName, loggerFactory), IObligationCalculationRepository
{
    public const string CollectionName = "obligation-calculations";

    // MongoDB duplicate key. The driver usually reports this as ServerErrorCategory.DuplicateKey.
    private const int DuplicateKeyCode = 11000;

    public async Task CreateAsync(
        IClientSessionHandle session,
        ObligationCalculationModel calculation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(calculation);

        try
        {
            await Collection.InsertOneAsync(session, calculation, options: null, cancellationToken);
        }
        catch (Exception exception)
        {
            if (ToDuplicateKey(exception) is { } duplicate)
            {
                throw duplicate;
            }

            throw;
        }
    }

    public async Task<bool> SupersedeCurrentAsync(
        IClientSessionHandle session,
        string id,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(id);

        var result = await Collection.UpdateOneAsync(
            session,
            Builders<ObligationCalculationModel>.Filter.And(
                Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.Id, id),
                Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.IsCurrent, true)),
            Builders<ObligationCalculationModel>.Update
                .Set(calculation => calculation.IsCurrent, false)
                .Set(calculation => calculation.UpdatedAtUtc, updatedAtUtc),
            options: null,
            cancellationToken);

        return result.MatchedCount == 1;
    }

    public async Task<ObligationCalculationModel?> FindCurrentAsync(
        string schemeOrgId,
        int compliancePeriodYear,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schemeOrgId);

        return await Collection
            .Find(Builders<ObligationCalculationModel>.Filter.And(
                Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.SchemeOrgId, schemeOrgId),
                Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.CompliancePeriodYear, compliancePeriodYear),
                Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.IsCurrent, true)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ObligationCalculationModel>> FindBySchemeYearAsync(
        string schemeOrgId,
        int compliancePeriodYear,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schemeOrgId);

        return await Collection
            .Find(Builders<ObligationCalculationModel>.Filter.And(
                Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.SchemeOrgId, schemeOrgId),
                Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.CompliancePeriodYear, compliancePeriodYear)))
            .Sort(Builders<ObligationCalculationModel>.Sort
                .Descending(calculation => calculation.Version)
                .Descending(calculation => calculation.CalculatedAtUtc)
                .Ascending(calculation => calculation.Id))
            .ToListAsync(cancellationToken);
    }

    // A duplicate on the current-version index or the version index.
    private static DuplicateKeyOnSaveException? ToDuplicateKey(Exception exception) =>
        exception switch
        {
            MongoWriteException write when write.WriteError?.Category == ServerErrorCategory.DuplicateKey
                => new DuplicateKeyOnSaveException(write),
            MongoCommandException command when command.Code == DuplicateKeyCode
                => new DuplicateKeyOnSaveException(command),
            _ => null
        };

    protected override List<CreateIndexModel<ObligationCalculationModel>> DefineIndexes(
        IndexKeysDefinitionBuilder<ObligationCalculationModel> builder) =>
        ObligationCalculationIndexes.Create(builder);
}
