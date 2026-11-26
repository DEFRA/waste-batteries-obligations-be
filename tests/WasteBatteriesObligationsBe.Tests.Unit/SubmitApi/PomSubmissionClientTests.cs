using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WasteBatteriesObligationsBe.SubmitApi;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Unit.SubmitApi;

public class PomSubmissionClientTests
{
    [Fact]
    public async Task ListAsync_BuildsTheFilterQueryAndReadsThePage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        HttpRequestMessage? sent = null;
        using var harness = Client(
            request =>
            {
                sent = request;
                return Json(HttpStatusCode.OK, new
                {
                    items = new[]
                    {
                        new
                        {
                            id = "674a1b2c3d4e5f6789012345",
                            schemeOrgId = "674a1b2c3d4e5f6789012346",
                            compliancePeriodYear = 2026,
                            compliancePeriodQuarter = "Quarter 1",
                            tonnageEntries = new[] { new { tonnes = "1.000" } }
                        }
                    },
                    pageNumber = 2,
                    pageSize = 25,
                    totalCount = 40
                });
            },
            "service-token");

        var page = await harness.Client.ListAsync(new PomSubmissionQuery
        {
            SchemeOrgId = "674a1b2c3d4e5f6789012346",
            CompliancePeriodYears = [2025, 2026],
            CompliancePeriodQuarters = ["Quarter 1", "Quarter 2"],
            ReturnCurrentOnly = false,
            PageNumber = 2,
            PageSize = 25
        }, cancellationToken);

        Assert.NotNull(sent);
        Assert.Equal("Bearer", sent.Headers.Authorization?.Scheme);
        Assert.Equal("service-token", sent.Headers.Authorization?.Parameter);
        Assert.Equal("gzip", Assert.Single(sent.Headers.AcceptEncoding).Value);
        Assert.Equal(HttpMethod.Get, sent.Method);
        var query = sent.RequestUri?.Query ?? string.Empty;
        Assert.Contains("schemeOrgId=674a1b2c3d4e5f6789012346", query, StringComparison.Ordinal);
        Assert.Contains("compliancePeriodYears=2025", query, StringComparison.Ordinal);
        Assert.Contains("compliancePeriodYears=2026", query, StringComparison.Ordinal);
        Assert.Contains("compliancePeriodQuarters=Quarter%201", query, StringComparison.Ordinal);
        Assert.Contains("returnCurrentOnly=false", query, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", query, StringComparison.Ordinal);
        Assert.Contains("pageSize=25", query, StringComparison.Ordinal);
        Assert.Equal(40, page.TotalCount);
        Assert.Equal("1.000", Assert.Single(page.Items).TonnageEntries[0].Tonnes);
    }

    [Fact]
    public void ListPath_WhenThereAreNoFilters_HasNoQuery()
    {
        Assert.Equal("pom-submissions", PomSubmissionClient.ListPath(new PomSubmissionQuery()));
    }

    [Fact]
    public async Task ListAsync_WhenTheBodyIsGzip_ReadsThePage()
    {
        using var harness = Client(
            _ => Gzip(HttpStatusCode.OK, """{"items":[],"pageNumber":1,"pageSize":10,"totalCount":3}"""),
            "service-token");

        var page = await harness.Client.ListAsync(new PomSubmissionQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task ListAsync_WhenGzipWasAlreadyInflated_ReadsThePage()
    {
        var response = Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), pageNumber = 1, pageSize = 10, totalCount = 4 });
        response.Content.Headers.ContentEncoding.Add("gzip");
        using var harness = Client(_ => response, "service-token");

        var page = await harness.Client.ListAsync(new PomSubmissionQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(4, page.TotalCount);
    }

    [Fact]
    public async Task ListAsync_WhenTheApiRejectsTheCall_Throws()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var harness = Client(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), "service-token");

        var exception = await Assert.ThrowsAsync<SubmitApiException>(() =>
            harness.Client.ListAsync(new PomSubmissionQuery(), cancellationToken));

        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public async Task ListAsync_WhenTheBodyHasNoBytes_Throws()
    {
        using var harness = Client(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            },
            "service-token");

        var exception = await Assert.ThrowsAsync<SubmitApiException>(() =>
            harness.Client.ListAsync(new PomSubmissionQuery(), TestContext.Current.CancellationToken));

        Assert.Contains("empty body", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_WhenTheBodyIsEmpty_Throws()
    {
        using var harness = Client(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            },
            "service-token");

        var exception = await Assert.ThrowsAsync<SubmitApiException>(() =>
            harness.Client.ListAsync(new PomSubmissionQuery(), TestContext.Current.CancellationToken));

        Assert.Contains("empty body", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSubmitApi_ResolvesTheListClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubmitApi:BaseUrl"] = "http://localhost:3001",
                ["SubmitApi:Audience"] = "waste-batteries-submit-backend",
                ["SubmitApi:Mode"] = "Development",
                ["SubmitApi:DevToken"] = "local-obligations-to-submit"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddTimeProvider();
        services.AddSubmitApi(configuration, new DevelopmentHost());

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IPomSubmissionClient>();

        Assert.IsType<PomSubmissionClient>(client);
    }

    private static Harness Client(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        string token)
    {
        var http = new HttpClient(new StubHandler(respond))
        {
            BaseAddress = new Uri("http://submit.test/")
        };
        return new Harness(new PomSubmissionClient(http, new StaticToken(token), NullLogger<PomSubmissionClient>.Instance), http);
    }

    private static HttpResponseMessage Gzip(HttpStatusCode status, string json)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes);
        }

        var response = new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(compressed.ToArray())
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        response.Content.Headers.ContentEncoding.Add("gzip");
        return response;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status)
        {
            Content = JsonContent.Create(body, options: new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };

    private sealed class Harness(PomSubmissionClient client, HttpClient http) : IDisposable
    {
        public PomSubmissionClient Client { get; } = client;

        public void Dispose() => http.Dispose();
    }

    private sealed class StaticToken(string token) : IWebIdentityTokenSource
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken) => Task.FromResult(token);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class DevelopmentHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
