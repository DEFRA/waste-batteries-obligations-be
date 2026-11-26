using Amazon;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using WasteBatteriesObligationsBe.Config;

namespace WasteBatteriesObligationsBe.SubmitApi;

// Asks AWS STS (Security Token Service) for the short-lived JWT sent to the submissions API.
// Floci cannot issue GetWebIdentityToken, so this stays out of the coverage gate.
[ExcludeFromCodeCoverage]
internal sealed class StsWebIdentityTokenSource : IWebIdentityTokenIssuer, IDisposable
{
    private readonly AmazonSecurityTokenServiceClient _sts;
    private readonly IOptions<SubmitApiConfig> _options;

    public StsWebIdentityTokenSource(IOptions<SubmitApiConfig> options)
    {
        _options = options;
        // GetWebIdentityToken is not served from the global STS endpoint. CDP runs in eu-west-2.
        _sts = new AmazonSecurityTokenServiceClient(new AmazonSecurityTokenServiceConfig
        {
            RegionEndpoint = RegionEndpoint.EUWest2
        });
    }

    public async Task<IssuedWebIdentityToken> IssueAsync(CancellationToken cancellationToken)
    {
        // Audience is the submissions service name. CDP refuses a lifetime over 15 minutes.
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

        // The AWS SDK sometimes returns an unspecified DateTime. The cache compares it as UTC.
        var expires = response.Expiration.Kind == DateTimeKind.Utc
            ? response.Expiration
            : response.Expiration.ToUniversalTime();
        return new IssuedWebIdentityToken(response.WebIdentityToken, expires);
    }

    public void Dispose() => _sts.Dispose();
}
