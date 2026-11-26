namespace WasteBatteriesObligationsBe.Config;

// How this API calls the submissions API. Aws asks STS for a web identity token.
// Development sends the shared local token, because Floci cannot issue one.
public sealed class SubmitApiConfig
{
    public const string SectionName = "SubmitApi";

    public const string AwsMode = "Aws";

    public const string DevelopmentMode = "Development";

    // CDP rejects GetWebIdentityToken above 15 minutes.
    public const int MinimumTokenLifetimeSeconds = 60;

    public const int MaximumTokenLifetimeSeconds = 900;

    public string BaseUrl { get; set; } = string.Empty;

    // aud claim on the token. The submissions API expects its own service name.
    public string Audience { get; set; } = string.Empty;

    public string Mode { get; set; } = AwsMode;

    public int TokenLifetimeSeconds { get; set; } = 300;

    // Shared with the submissions API. Used only when Mode is Development.
    public string DevToken { get; set; } = string.Empty;

    public bool IsDevelopment => string.Equals(Mode, DevelopmentMode, StringComparison.Ordinal);

    public string? ValidationError(string aspNetEnvironment, string? cdpEnvironment)
    {
        if (!string.Equals(Mode, AwsMode, StringComparison.Ordinal)
            && !string.Equals(Mode, DevelopmentMode, StringComparison.Ordinal))
        {
            return "SubmitApi:Mode must be Aws or Development.";
        }

        if (!IsHttp(BaseUrl))
        {
            return "SubmitApi:BaseUrl must be an absolute http or https URL. On CDP set SubmitApi__BaseUrl to https://waste-batteries-submit-backend.<environment>.cdp-int.defra.cloud.";
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            return "SubmitApi:Audience is required.";
        }

        if (TokenLifetimeSeconds is < MinimumTokenLifetimeSeconds or > MaximumTokenLifetimeSeconds)
        {
            return "SubmitApi:TokenLifetimeSeconds must be from 60 to 900. CDP rejects a longer STS token.";
        }

        if (!IsDevelopment)
        {
            return null;
        }

        if (!string.Equals(aspNetEnvironment, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            return "SubmitApi:Mode cannot be Development unless the host environment is Development.";
        }

        if (string.Equals(cdpEnvironment?.Trim(), "prod", StringComparison.OrdinalIgnoreCase))
        {
            return "SubmitApi:Mode cannot be Development when ENVIRONMENT is prod.";
        }

        if (string.IsNullOrWhiteSpace(DevToken))
        {
            return "SubmitApi:DevToken is required when Mode is Development.";
        }

        return null;
    }

    private static bool IsHttp(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
