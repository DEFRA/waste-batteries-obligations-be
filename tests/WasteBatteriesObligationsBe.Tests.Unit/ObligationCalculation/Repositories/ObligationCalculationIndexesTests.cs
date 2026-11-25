using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using WasteBatteriesObligationsBe.ObligationCalculation.Models;
using WasteBatteriesObligationsBe.ObligationCalculation.Repositories;
using WasteBatteriesObligationsBe.Utils.Mongo;

namespace WasteBatteriesObligationsBe.Tests.Unit.ObligationCalculation.Repositories;

public class ObligationCalculationIndexesTests
{
    [Fact]
    public void Create_WhenCalled_DefinesCalculationIndexes()
    {
        // Arrange
        MongoConventions.Register();
        var indexKeys = Builders<ObligationCalculationModel>.IndexKeys;

        // Act
        var indexes = ObligationCalculationIndexes.Create(indexKeys);
        var args = RenderArgs();

        // Assert
        Assert.Equal(4, indexes.Count);

        var version = indexes[0];
        Assert.True(version.Options.Unique);
        Assert.Equal(
            "{ \"schemeOrgId\" : 1, \"compliancePeriodYear\" : 1, \"version\" : 1 }",
            version.Keys.Render(args).ToString());
        Assert.Null(IndexOptions(version).PartialFilterExpression);

        var current = indexes[1];
        Assert.True(current.Options.Unique);
        Assert.Equal(
            "{ \"schemeOrgId\" : 1, \"compliancePeriodYear\" : 1 }",
            current.Keys.Render(args).ToString());
        Assert.Equal("{ \"isCurrent\" : true }", PartialFilter(current).Render(args).ToString());

        var year = indexes[2];
        Assert.NotEqual(true, year.Options?.Unique);
        Assert.Equal("{ \"compliancePeriodYear\" : 1 }", year.Keys.Render(args).ToString());
        Assert.Equal("{ \"isCurrent\" : true }", PartialFilter(year).Render(args).ToString());

        var supersedes = indexes[3];
        Assert.True(supersedes.Options.Sparse);
        Assert.Equal("{ \"supersedesCalculationId\" : 1 }", supersedes.Keys.Render(args).ToString());
        Assert.Null(IndexOptions(supersedes).PartialFilterExpression);
    }

    private static FilterDefinition<ObligationCalculationModel> PartialFilter(CreateIndexModel<ObligationCalculationModel> index)
    {
        var options = IndexOptions(index);
        Assert.NotNull(options.PartialFilterExpression);
        return options.PartialFilterExpression;
    }

    private static CreateIndexOptions<ObligationCalculationModel> IndexOptions(CreateIndexModel<ObligationCalculationModel> index) =>
        Assert.IsType<CreateIndexOptions<ObligationCalculationModel>>(index.Options);

    private static RenderArgs<ObligationCalculationModel> RenderArgs()
    {
        var registry = BsonSerializer.SerializerRegistry;
        return new RenderArgs<ObligationCalculationModel>(
            registry.GetSerializer<ObligationCalculationModel>(),
            registry,
            default,
            false,
            false,
            false,
            null);
    }
}
