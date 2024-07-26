using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Retry;

namespace KernelMemory.FileWatcher.Extensions;

internal static class HttpClientStartup
{
    internal static IServiceCollection ConfigureHttpClient(this IServiceCollection services, IConfiguration configuration)
    {
        var kernelMemoryOptions = configuration.GetSection(nameof(KernelMemoryOptions)).Get<KernelMemoryOptions>();

        services.AddHttpClient(DefaultOptions.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(kernelMemoryOptions?.Endpoint ?? DefaultOptions.KernelMemoryEndpoint);
            if (!string.IsNullOrWhiteSpace(kernelMemoryOptions?.ApiKey))
            {
                client.DefaultRequestHeaders.Add(DefaultOptions.AuthHeaderName, kernelMemoryOptions.ApiKey);
            }
        })
        .AddPolicyHandler(GetRetryPolicy(kernelMemoryOptions))
        .AddPolicyHandler(GetCircuitBreakerPolicy(kernelMemoryOptions));

        return services;
    }

    internal static AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy(KernelMemoryOptions? kernelMemoryOptions)
    {
        var delay = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: TimeSpan.FromSeconds(kernelMemoryOptions?.FirstRetryDelay ?? DefaultOptions.KernelMemoryFirstRetryDelay),
            retryCount: kernelMemoryOptions?.Retries ?? DefaultOptions.KernelMemoryRetries,
            fastFirst: true);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(delay);
    }

    internal static AsyncCircuitBreakerPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(KernelMemoryOptions? kernelMemoryOptions)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: kernelMemoryOptions?.CircuitEventsBeforeBreak ?? DefaultOptions.CircuitEventsBeforeBreak,
                durationOfBreak: TimeSpan.FromSeconds(kernelMemoryOptions?.CircuitBreakDuration ?? DefaultOptions.CircuitBreakDuration),
                onBreak: (result, timespan, _) =>
                {
                    Log.Warning("Circuit breaker opened for {DurationOfBreak} due to failures", timespan);
                },
                onReset: (_) =>
                {
                    Log.Information("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    Log.Information("Circuit breaker is half-open");
                });
    }
}
