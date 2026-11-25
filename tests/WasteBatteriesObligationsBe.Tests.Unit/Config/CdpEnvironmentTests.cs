using WasteBatteriesObligationsBe.Config;

namespace WasteBatteriesObligationsBe.Tests.Unit.Config;

public class CdpEnvironmentTests
{
    [Theory]
    [InlineData("prod")]
    [InlineData("PROD")]
    [InlineData("Prod")]
    [InlineData(" prod ")]
    public void IsProduction_WhenPlatformNameIsProd_ReturnsTrue(string environment)
    {
        Assert.True(CdpEnvironment.IsProduction(environment));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dev")]
    [InlineData("test")]
    [InlineData("perf-test")]
    [InlineData("ext-test")]
    [InlineData("infra-dev")]
    [InlineData("management")]
    [InlineData("local")]
    [InlineData("Production")]
    [InlineData("production")]
    public void IsProduction_WhenPlatformNameIsNotProd_ReturnsFalse(string? environment)
    {
        Assert.False(CdpEnvironment.IsProduction(environment));
    }
}
