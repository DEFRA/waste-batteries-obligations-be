using WasteBatteriesObligationsBe.Config;

namespace WasteBatteriesObligationsBe.Tests.Unit.Config;

public class SubmitApiConfigTests
{
    [Fact]
    public void ValidationError_WhenAwsHasAnAbsoluteUrl_ReturnsNull()
    {
        var error = Aws().ValidationError("Production", "dev");

        Assert.Null(error);
    }

    [Fact]
    public void ValidationError_WhenDevelopmentHasAToken_ReturnsNull()
    {
        var error = Development().ValidationError("Development", null);

        Assert.Null(error);
    }

    [Fact]
    public void ValidationError_WhenTheBaseUrlIsNotAbsolute_NamesTheBaseUrl()
    {
        var config = Aws();
        config.BaseUrl = "set-automatically-when-deployed";

        var error = config.ValidationError("Production", "dev");

        Assert.Contains("BaseUrl", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(901)]
    public void ValidationError_WhenTheTokenLifetimeIsOutsideTheCdpLimit_NamesTheLifetime(int seconds)
    {
        var config = Aws();
        config.TokenLifetimeSeconds = seconds;

        var error = config.ValidationError("Production", "dev");

        Assert.Contains("TokenLifetimeSeconds", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationError_WhenDevelopmentIsUsedInProductionHost_NamesTheHost()
    {
        var error = Development().ValidationError("Production", "dev");

        Assert.Contains("host environment is Development", error, StringComparison.Ordinal);
    }

    private static SubmitApiConfig Aws() => new()
    {
        Mode = SubmitApiConfig.AwsMode,
        BaseUrl = "https://waste-batteries-submit-backend.dev.cdp-int.defra.cloud",
        Audience = "waste-batteries-submit-backend",
        TokenLifetimeSeconds = 300
    };

    private static SubmitApiConfig Development() => new()
    {
        Mode = SubmitApiConfig.DevelopmentMode,
        BaseUrl = "http://localhost:3001",
        Audience = "waste-batteries-submit-backend",
        DevToken = "local-obligations-to-submit",
        TokenLifetimeSeconds = 300
    };
}
