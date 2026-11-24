using MongoDB.Bson;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Mapping;

public interface IObligationCalculationMapper
{
    // calculatedAtUtc comes from the caller.
    // version replaces the initial version when a new version allocates the next one.
    // supersedesCalculationId is the calculation this one replaces. A first calculation leaves it null.
    ObligationCalculationModel ToNewCalculation(
        ObligationCalculationBase calculation,
        DateTime calculatedAtUtc,
        int? version = null,
        string? supersedesCalculationId = null);
}

public sealed class ObligationCalculationMapper : IObligationCalculationMapper
{
    public ObligationCalculationModel ToNewCalculation(
        ObligationCalculationBase calculation,
        DateTime calculatedAtUtc,
        int? version = null,
        string? supersedesCalculationId = null) =>
        new(
            calculation,
            ObjectId.GenerateNewId().ToString(),
            calculatedAtUtc,
            version ?? ObligationCalculationModel.InitialVersion,
            supersedesCalculationId);
}
