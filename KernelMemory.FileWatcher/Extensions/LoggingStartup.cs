using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace KernelMemory.FileWatcher.Extensions;

internal static class LoggingStartup
{
    internal static IServiceCollection ConfigureLogging(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        services.AddSerilog((_, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(GetLogFilePath(), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
                .MinimumLevel.Warning();
        });

        return services;
    }

    private static string GetLogFilePath()
    {
        return Path.Exists(Path.Combine(DefaultOptions.ConfigFolder, DefaultOptions.LogFolder))
            ? Path.Combine(DefaultOptions.ConfigFolder, DefaultOptions.LogFolder, DefaultOptions.LogFileName)
            : Path.Combine(DefaultOptions.LogFolder, DefaultOptions.LogFileName);
    }
}
