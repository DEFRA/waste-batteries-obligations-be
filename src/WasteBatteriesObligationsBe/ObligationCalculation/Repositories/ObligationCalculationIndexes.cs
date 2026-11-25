using MongoDB.Driver;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;

namespace WasteBatteriesObligationsBe.ObligationCalculation.Repositories;

internal static class ObligationCalculationIndexes
{
    public static List<CreateIndexModel<ObligationCalculationModel>> Create(
        IndexKeysDefinitionBuilder<ObligationCalculationModel> builder)
    {
        // Each version of a scheme and compliance year is stored once, including rows that are no longer current.
        var versionPerSchemeYear = new CreateIndexModel<ObligationCalculationModel>(
            builder
                .Ascending(calculation => calculation.SchemeOrgId)
                .Ascending(calculation => calculation.CompliancePeriodYear)
                .Ascending(calculation => calculation.Version),
            new CreateIndexOptions<ObligationCalculationModel> { Unique = true });

        // One current version per scheme and compliance year.
        var currentPerSchemeYear = new CreateIndexModel<ObligationCalculationModel>(
            builder
                .Ascending(calculation => calculation.SchemeOrgId)
                .Ascending(calculation => calculation.CompliancePeriodYear),
            new CreateIndexOptions<ObligationCalculationModel>
            {
                Unique = true,
                PartialFilterExpression = Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.IsCurrent, true)
            });

        // The current figures for every scheme in a compliance year.
        var currentByYear = new CreateIndexModel<ObligationCalculationModel>(
            builder.Ascending(calculation => calculation.CompliancePeriodYear),
            new CreateIndexOptions<ObligationCalculationModel>
            {
                PartialFilterExpression = Builders<ObligationCalculationModel>.Filter.Eq(calculation => calculation.IsCurrent, true)
            });

        var supersedes = new CreateIndexModel<ObligationCalculationModel>(
            builder.Ascending(calculation => calculation.SupersedesCalculationId),
            new CreateIndexOptions<ObligationCalculationModel>
            {
                Sparse = true
            });

        return [versionPerSchemeYear, currentPerSchemeYear, currentByYear, supersedes];
    }
}
