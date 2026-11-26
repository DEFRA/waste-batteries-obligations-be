using System.Diagnostics.CodeAnalysis;

namespace WasteBatteriesObligationsBe.Utils.Http;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHeaderPropagationFromConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var traceHeader = configuration.GetValue<string>("TraceHeader");

        services.AddHeaderPropagation(options =>
        {
            if (!string.IsNullOrWhiteSpace(traceHeader))
            {
                options.Headers.Add(traceHeader);
            }
        });

        return services;
    }

    public static IServiceCollection AddProxyHttpClients(this IServiceCollection services)
    {
        services.AddTransient<ProxyHttpMessageHandler>();

        // The submissions client is registered in AddSubmitApi. It calls another CDP
        // service directly and sets its own bearer token, so it does not use this proxy.
        // For a later client that calls a host outside CDP, add
        // Microsoft.Extensions.Http.Resilience on that client, as waste-obligations does
        // for its downstream clients. Register it with AddHttpClientWithTracing or
        // AddHttpClientWithTracingAndProxy. Do not add resilience before a client exists.

        return services;
    }
}
