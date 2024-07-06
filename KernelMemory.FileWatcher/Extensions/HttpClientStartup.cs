using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
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
        }).AddPolicyHandler(GetRetryPolicy(kernelMemoryOptions?.Retries ?? DefaultOptions.KernelMemoryRetries));

        return services;
    }

    internal static AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy(int retries)
    {
        var delay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: TimeSpan.FromSeconds(1), retryCount: retries, fastFirst: true);

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(delay);
    }
}
