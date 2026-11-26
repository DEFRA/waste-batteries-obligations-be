using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using WasteBatteriesObligationsBe.Utils.Logging;

namespace WasteBatteriesObligationsBe.SubmitApi;

public sealed record PomSubmissionQuery
{
    public string? SchemeOrgId { get; init; }

    public string? MemberOrgId { get; init; }

    public IReadOnlyList<int>? CompliancePeriodYears { get; init; }

    public IReadOnlyList<string>? CompliancePeriodQuarters { get; init; }

    public bool? ReturnCurrentOnly { get; init; }

    public int? PageNumber { get; init; }

    public int? PageSize { get; init; }
}

// The fields a later calculation needs. The submissions API returns more, and those are ignored.
public sealed record PomSubmissionDocument
{
    public string Id { get; init; } = string.Empty;

    public string SchemeOrgId { get; init; } = string.Empty;

    public string MemberOrgId { get; init; } = string.Empty;

    public int CompliancePeriodYear { get; init; }

    public string CompliancePeriodQuarter { get; init; } = string.Empty;

    public List<PomSubmissionTonnage> TonnageEntries { get; init; } = [];
}

public sealed record PomSubmissionTonnage
{
    public string Tonnes { get; init; } = string.Empty;
}

public sealed record PomSubmissionPage
{
    public List<PomSubmissionDocument> Items { get; init; } = [];

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class SubmitApiException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IPomSubmissionClient
{
    Task<PomSubmissionPage> ListAsync(PomSubmissionQuery query, CancellationToken cancellationToken);
}

internal sealed class PomSubmissionClient(
    HttpClient httpClient,
    IWebIdentityTokenSource tokens,
    ILogger<PomSubmissionClient> logger) : IPomSubmissionClient
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PomSubmissionPage> ListAsync(PomSubmissionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = ListPath(query);
        using var response = await SendAsync(path, cancellationToken);
        await EnsureSuccess(response, "pom-submissions");
        return await ReadRequired<PomSubmissionPage>(response, "pom-submissions", cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Accepting gzip selects the larger submissions page and compresses that body.
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task EnsureSuccess(HttpResponseMessage response, string path)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        logger.SubmissionsApiCallFailed((int)response.StatusCode, path);
        throw new SubmitApiException(
            $"Submissions API returned {(int)response.StatusCode} for {path}.",
            (int)response.StatusCode);
    }

    private static async Task<T> ReadRequired<T>(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (IsRawGzip(response, bytes))
        {
            bytes = InflateGzip(bytes);
        }

        if (bytes.Length == 0)
        {
            throw new SubmitApiException($"Submissions API returned an empty body for {path}.", (int)response.StatusCode);
        }

        var body = JsonSerializer.Deserialize<T>(bytes, s_jsonOptions);
        return body is null
            ? throw new SubmitApiException($"Submissions API returned an empty body for {path}.", (int)response.StatusCode)
            : body;
    }

    // The handler does not inflate a response when this request set Accept-Encoding.
    // A body that was already inflated does not start with the gzip id.
    private static bool IsRawGzip(HttpResponseMessage response, byte[] bytes) =>
        response.Content.Headers.ContentEncoding.Any(encoding =>
            encoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
        && bytes.Length >= 2
        && bytes[0] == 0x1F
        && bytes[1] == 0x8B;

    private static byte[] InflateGzip(byte[] bytes)
    {
        using var source = new MemoryStream(bytes);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        gzip.CopyTo(inflated);
        return inflated.ToArray();
    }

    internal static string ListPath(PomSubmissionQuery query)
    {
        var parameters = new List<string>();
        Add(parameters, "schemeOrgId", query.SchemeOrgId);
        Add(parameters, "memberOrgId", query.MemberOrgId);
        if (query.CompliancePeriodYears is not null)
        {
            foreach (var year in query.CompliancePeriodYears)
            {
                Add(parameters, "compliancePeriodYears", year.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (query.CompliancePeriodQuarters is not null)
        {
            foreach (var quarter in query.CompliancePeriodQuarters)
            {
                Add(parameters, "compliancePeriodQuarters", quarter);
            }
        }

        if (query.ReturnCurrentOnly is not null)
        {
            Add(parameters, "returnCurrentOnly", query.ReturnCurrentOnly.Value ? "true" : "false");
        }

        if (query.PageNumber is not null)
        {
            Add(parameters, "pageNumber", query.PageNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (query.PageSize is not null)
        {
            Add(parameters, "pageSize", query.PageSize.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return parameters.Count == 0 ? "pom-submissions" : $"pom-submissions?{string.Join('&', parameters)}";
    }

    private static void Add(List<string> parameters, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        parameters.Add($"{name}={Uri.EscapeDataString(value)}");
    }
}
