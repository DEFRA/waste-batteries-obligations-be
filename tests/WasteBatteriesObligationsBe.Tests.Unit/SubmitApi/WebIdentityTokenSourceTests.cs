using Microsoft.Extensions.Options;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.SubmitApi;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Unit.SubmitApi;

public class WebIdentityTokenSourceTests
{
    [Fact]
    public async Task GetTokenAsync_ReusesTheTokenUntilTheRefreshTime()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var issuer = new CountingIssuer(clock.UtcNow.AddMinutes(5));
        var source = new CachingWebIdentityTokenSource(issuer, clock);

        var first = await source.GetTokenAsync(cancellationToken);
        clock.UtcNow = clock.UtcNow.AddMinutes(4);
        var second = await source.GetTokenAsync(cancellationToken);

        Assert.Equal("token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, issuer.Calls);
    }

    [Fact]
    public async Task GetTokenAsync_RefreshesOnceTheTokenIsInsideTheSkew()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var issuer = new CountingIssuer(clock.UtcNow.AddMinutes(5));
        var source = new CachingWebIdentityTokenSource(issuer, clock);

        await source.GetTokenAsync(cancellationToken);
        clock.UtcNow = clock.UtcNow.AddMinutes(4).AddSeconds(31);
        var refreshed = await source.GetTokenAsync(cancellationToken);

        Assert.Equal("token-2", refreshed);
        Assert.Equal(2, issuer.Calls);
    }

    [Fact]
    public async Task IssueAsync_ReturnsTheConfiguredDevToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var source = new DevelopmentWebIdentityTokenSource(
            Options.Create(new SubmitApiConfig { DevToken = "local-obligations-to-submit" }),
            clock);

        var issued = await source.IssueAsync(cancellationToken);

        Assert.Equal("local-obligations-to-submit", issued.Token);
        Assert.True(issued.ExpiresAtUtc > clock.UtcNow);
    }

    private sealed class CountingIssuer(DateTime expiresAtUtc) : IWebIdentityTokenIssuer
    {
        public int Calls { get; private set; }

        public Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new IssuedWebIdentityToken($"token-{Calls}", expiresAtUtc));
        }
    }

    private sealed class MutableTime(DateTime utcNow) : ITimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
