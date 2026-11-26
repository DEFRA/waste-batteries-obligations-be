using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using WasteBatteriesObligationsBe.Config;
using WasteBatteriesObligationsBe.Utils.Http;
using WasteBatteriesObligationsBe.Utils.Time;

namespace WasteBatteriesObligationsBe.SubmitApi;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSubmitApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IValidateOptions<SubmitApiConfig>>(new SubmitApiConfigValidator(environment));
        services
            .AddOptions<SubmitApiConfig>()
            .Bind(configuration.GetRequiredSection(SubmitApiConfig.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IWebIdentityTokenSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SubmitApiConfig>>();
            var time = sp.GetRequiredService<ITimeProvider>();
            IWebIdentityTokenIssuer issuer = options.Value.IsDevelopment
                ? new DevelopmentWebIdentityTokenSource(options, time)
                : new StsWebIdentityTokenSource(options);
            return new CachingWebIdentityTokenSource(issuer, time);
        });

        // Direct to the submissions service. Tracing is forwarded and the call is
        // retried by the resilience handler. The CDP proxy is not: the other service
        // is inside the VPC, and this client sets its own service token.
        services
            .AddHttpClientWithTracing<IPomSubmissionClient, PomSubmissionClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var baseUrl = sp.GetRequiredService<IOptions<SubmitApiConfig>>().Value.BaseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseProxy = false
            })
            .AddStandardResilienceHandler();

        return services;
    }

    private static string? CdpEnvironmentName() => Environment.GetEnvironmentVariable("ENVIRONMENT");

    [ExcludeFromCodeCoverage]
    private sealed class SubmitApiConfigValidator(IHostEnvironment environment) : IValidateOptions<SubmitApiConfig>
    {
        public ValidateOptionsResult Validate(string? name, SubmitApiConfig options)
        {
            var error = options.ValidationError(environment.EnvironmentName, CdpEnvironmentName());
            return error is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(error);
        }
    }
}
