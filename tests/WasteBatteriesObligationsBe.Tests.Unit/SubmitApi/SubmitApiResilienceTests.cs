using Microsoft.AspNetCore.HeaderPropagation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Primitives;
using Polly;
using System.Net;
using WasteBatteriesObligationsBe.SubmitApi;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.Tests.Unit.SubmitApi;

public class SubmitApiResilienceTests
{
    [Fact]
    public async Task ListAsync_WhenTheApiIsTemporarilyUnavailable_RetriesAndReadsThePage()
    {
        using var provider = Services(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        InitialiseHeaderPropagation(provider);
        var handler = provider.GetRequiredService<CountingHandler>();
        var client = provider.GetRequiredService<IPomSubmissionClient>();

        var page = await client.ListAsync(new PomSubmissionQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Calls);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task ListAsync_WhenTheApiStaysUnavailable_StopsAfterTheRetryBudget()
    {
        using var provider = Services(HttpStatusCode.ServiceUnavailable);
        InitialiseHeaderPropagation(provider);
        var handler = provider.GetRequiredService<CountingHandler>();
        var client = provider.GetRequiredService<IPomSubmissionClient>();

        var exception = await Assert.ThrowsAsync<SubmitApiException>(() =>
            client.ListAsync(new PomSubmissionQuery(), TestContext.Current.CancellationToken));

        // One attempt plus the two retries configured below.
        Assert.Equal(3, handler.Calls);
        Assert.Equal(503, exception.StatusCode);
    }

    [Fact]
    public async Task ListAsync_WhenTheApiRejectsTheCall_DoesNotRetry()
    {
        using var provider = Services(HttpStatusCode.Unauthorized);
        InitialiseHeaderPropagation(provider);
        var handler = provider.GetRequiredService<CountingHandler>();
        var client = provider.GetRequiredService<IPomSubmissionClient>();

        var exception = await Assert.ThrowsAsync<SubmitApiException>(() =>
            client.ListAsync(new PomSubmissionQuery(), TestContext.Current.CancellationToken));

        Assert.Equal(1, handler.Calls);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public void AddSubmitApi_LeavesTheClientTimeoutToTheResilienceHandler()
    {
        using var provider = Services(HttpStatusCode.OK);
        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("IPomSubmissionClient");

        Assert.Equal(Timeout.InfiniteTimeSpan, http.Timeout);
    }

    private static ServiceProvider Services(params HttpStatusCode[] codes)
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
        services.AddSingleton(new CountingHandler(codes));
        services.AddSubmitApi(configuration, new DevelopmentHost());
        services.AddHttpClient<IPomSubmissionClient, PomSubmissionClient>()
            .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<CountingHandler>());
        services.Configure<HttpStandardResilienceOptions>("IPomSubmissionClient-standard", options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(1);
            options.Retry.BackoffType = DelayBackoffType.Constant;
            options.Retry.UseJitter = false;
        });

        return services.BuildServiceProvider();
    }

    // AddHttpClientWithTracing propagates the trace header. That handler refuses to send
    // until the request middleware has initialised the header collection.
    private static void InitialiseHeaderPropagation(IServiceProvider provider)
    {
        provider.GetRequiredService<HeaderPropagationValues>().Headers =
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CountingHandler(HttpStatusCode[] codes) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            var status = codes[Math.Min(call, codes.Length) - 1];
            var response = new HttpResponseMessage(status);
            if (status == HttpStatusCode.OK)
            {
                response.Content = new StringContent(
                    """{"items":[],"pageNumber":1,"pageSize":10,"totalCount":0}""",
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            return Task.FromResult(response);
        }
    }

    private sealed class DevelopmentHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
