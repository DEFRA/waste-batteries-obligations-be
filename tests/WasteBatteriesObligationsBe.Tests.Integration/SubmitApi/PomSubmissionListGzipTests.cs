using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.IO.Compression;
using System.Net.Http.Headers;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.SubmitApi;

namespace WasteBatteriesObligationsBe.Tests.Integration.SubmitApi;

public class PomSubmissionListGzipTests
{
    [Fact]
    public async Task ListAsync_WhenTheSubmitServiceReturnsGzip_ReadsThePage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var json = """
            {"items":[{"id":"674a1b2c3d4e5f6789012345","schemeOrgId":"674a1b2c3d4e5f6789012346","memberOrgId":"674a1b2c3d4e5f6789012347","compliancePeriodYear":2026,"compliancePeriodQuarter":"Quarter 1","tonnageEntries":[{"tonnes":"1.000"}]}],"pageNumber":1,"pageSize":10,"totalCount":1}
            """;
        await using var submit = await SubmitStub.Start(Gzip(json), cancellationToken);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SubmitApi:BaseUrl"] = submit.Url
                });
            });
        });
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<HeaderPropagationValues>().Headers =
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        var client = services.GetRequiredService<IPomSubmissionClient>();

        var page = await client.ListAsync(new PomSubmissionQuery(), cancellationToken);

        var token = services.GetRequiredService<IOptions<SubmitApiConfig>>().Value.DevToken;
        Assert.Equal(new AuthenticationHeaderValue("Bearer", token).ToString(), submit.Authorization);
        Assert.Contains("gzip", submit.AcceptEncoding, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, page.TotalCount);
        var submission = Assert.Single(page.Items);
        Assert.Equal("674a1b2c3d4e5f6789012345", submission.Id);
        Assert.Equal("1.000", Assert.Single(submission.TonnageEntries).Tonnes);
    }

    private static byte[] Gzip(string json)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            gzip.Write(bytes);
        }

        return compressed.ToArray();
    }

    private sealed class SubmitStub : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private SubmitStub(WebApplication app) => _app = app;

        public string Url { get; private set; } = "";

        public string? AcceptEncoding { get; private set; }

        public string? Authorization { get; private set; }

        public static async Task<SubmitStub> Start(byte[] gzipBody, CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = "submit-stub",
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Production
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var stub = new SubmitStub(app);
            app.MapGet("/pom-submissions", async (HttpContext context) =>
            {
                stub.AcceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
                stub.Authorization = context.Request.Headers.Authorization.ToString();
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                context.Response.Headers.ContentEncoding = "gzip";
                await context.Response.Body.WriteAsync(gzipBody, context.RequestAborted);
            });
            await app.StartAsync(cancellationToken);
            stub.Url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
                .Addresses.Single();
            return stub;
        }

        public async ValueTask DisposeAsync() => await _app.DisposeAsync();
    }
}
