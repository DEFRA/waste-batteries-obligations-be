using Amazon;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.SubmitApi;

internal sealed record IssuedWebIdentityToken(string Token, DateTime ExpiresAtUtc);

internal interface IWebIdentityTokenIssuer
{
    Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken);
}

internal interface IWebIdentityTokenSource
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}

internal sealed class DevelopmentWebIdentityTokenSource(
    IOptions<SubmitApiConfig> options,
    ITimeProvider time) : IWebIdentityTokenIssuer
{
    public Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken)
    {
        var token = options.Value.DevToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("SubmitApi:DevToken is required when Mode is Development.");
        }

        return Task.FromResult(new IssuedWebIdentityToken(token, time.UtcNow.AddHours(1)));
    }
}

// Reuses a token until shortly before it expires. STS will not issue one for longer than 15 minutes.
internal sealed class CachingWebIdentityTokenSource(
    IWebIdentityTokenIssuer issuer,
    ITimeProvider time) : IWebIdentityTokenSource, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTime _refreshAtUtc;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return _token!;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return _token!;
            }

            var issued = await issuer.IssueAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(issued.Token))
            {
                throw new InvalidOperationException("The submissions service token was empty.");
            }

            if (issued.ExpiresAtUtc <= time.UtcNow)
            {
                throw new InvalidOperationException("The submissions service token is already expired.");
            }

            var lifetimeSeconds = (issued.ExpiresAtUtc - time.UtcNow).TotalSeconds;
            var skewSeconds = lifetimeSeconds > 30 ? 30 : 1;
            _token = issued.Token;
            _refreshAtUtc = issued.ExpiresAtUtc.AddSeconds(-skewSeconds);
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        if (issuer is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private bool IsFresh() => _token is not null && time.UtcNow < _refreshAtUtc;
}

[ExcludeFromCodeCoverage]
internal sealed class StsWebIdentityTokenSource : IWebIdentityTokenIssuer, IDisposable
{
    private readonly IAmazonSecurityTokenService _sts;
    private readonly IOptions<SubmitApiConfig> _options;

    public StsWebIdentityTokenSource(IOptions<SubmitApiConfig> options)
    {
        _options = options;
        // GetWebIdentityToken is not on the global STS endpoint. CDP runs in eu-west-2.
        _sts = new AmazonSecurityTokenServiceClient(new AmazonSecurityTokenServiceConfig
        {
            RegionEndpoint = RegionEndpoint.EUWest2
        });
    }

    public async Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken)
    {
        var response = await _sts.GetWebIdentityTokenAsync(new GetWebIdentityTokenRequest
        {
            Audience = [_options.Value.Audience],
            SigningAlgorithm = "RS256",
            DurationSeconds = _options.Value.TokenLifetimeSeconds
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(response.WebIdentityToken))
        {
            throw new InvalidOperationException("STS returned an empty web identity token.");
        }

        var expires = response.Expiration.Kind == DateTimeKind.Utc
            ? response.Expiration
            : response.Expiration.ToUniversalTime();
        return new IssuedWebIdentityToken(response.WebIdentityToken, expires);
    }

    public void Dispose() => _sts.Dispose();
}
