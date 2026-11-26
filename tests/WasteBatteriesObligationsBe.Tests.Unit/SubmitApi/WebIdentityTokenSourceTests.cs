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

    [Fact]
    public async Task IssueAsync_WhenTheDevTokenIsMissing_Throws()
    {
        var clock = new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var source = new DevelopmentWebIdentityTokenSource(
            Options.Create(new SubmitApiConfig { DevToken = " " }),
            clock);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.IssueAsync(TestContext.Current.CancellationToken));

        Assert.Contains("DevToken", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTokenAsync_WhenASecondCallerWaits_ReusesTheTokenTheFirstCallerStored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var issuer = new BlockingIssuer(clock.UtcNow.AddMinutes(5), release.Task);
        var source = new CachingWebIdentityTokenSource(issuer, clock);

        var first = source.GetTokenAsync(cancellationToken);
        await issuer.Entered.WaitAsync(cancellationToken);
        var second = source.GetTokenAsync(cancellationToken);
        release.SetResult();
        var tokens = await Task.WhenAll(first, second);

        Assert.Equal("token-1", tokens[0]);
        Assert.Equal(tokens[0], tokens[1]);
        Assert.Equal(1, issuer.Calls);
    }

    [Fact]
    public async Task GetTokenAsync_WhenTheIssuedTokenIsEmpty_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var source = new CachingWebIdentityTokenSource(new FixedIssuer(" ", clock.UtcNow.AddMinutes(5)), clock);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetTokenAsync(cancellationToken));

        Assert.Contains("empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTokenAsync_WhenTheIssuedTokenIsAlreadyExpired_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var source = new CachingWebIdentityTokenSource(new FixedIssuer("token-1", clock.UtcNow), clock);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetTokenAsync(cancellationToken));

        Assert.Contains("already expired", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_ReleasesTheIssuer()
    {
        var issuer = new DisposableIssuer();
        var source = new CachingWebIdentityTokenSource(
            issuer,
            new MutableTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        source.Dispose();

        Assert.True(issuer.Disposed);
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

    private sealed class FixedIssuer(string token, DateTime expiresAtUtc) : IWebIdentityTokenIssuer
    {
        public Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IssuedWebIdentityToken(token, expiresAtUtc));
    }

    private sealed class BlockingIssuer(DateTime expiresAtUtc, Task release) : IWebIdentityTokenIssuer
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public Task Entered => _entered.Task;

        public int Calls => _calls;

        public async Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            _entered.TrySetResult();
            await release.WaitAsync(cancellationToken);
            return new IssuedWebIdentityToken($"token-{call}", expiresAtUtc);
        }
    }

    private sealed class DisposableIssuer : IWebIdentityTokenIssuer, IDisposable
    {
        public bool Disposed { get; private set; }

        public Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Dispose() => Disposed = true;
    }
}
