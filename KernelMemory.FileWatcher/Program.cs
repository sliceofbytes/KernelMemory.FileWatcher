using KernelMemory.FileWatcher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KernelMemory.FileWatcher;

internal class Program
{
    protected Program()
    {
    }

    internal static async Task Main(string[] args)
    {
        try
        {
            using var host = CreateHostBuilder(args).Build();
            StartWatcher(host.Services);
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void StartWatcher(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var fileWatcher = provider.GetRequiredService<IFileWatcherService>();
        _ = fileWatcher.WatchAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.Sources.Clear();
                config.AddConfiguration(BuildConfiguration());
            })
            .ConfigureServices((hostContext, services) =>
            {
                ConfigureServices(services, hostContext.Configuration);
            })
            .UseConsoleLifetime();

    private static IConfiguration BuildConfiguration()
    {
        string basePath = File.Exists(Path.Combine(DefaultOptions.ConfigFolder, DefaultOptions.ConfigFileName)) ? DefaultOptions.ConfigFolder : AppContext.BaseDirectory;
        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(DefaultOptions.ConfigFileName, optional: false)
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services
            .ConfigureLogging(configuration);

        services
            .AddOptions<FileWatcherOptions>()
            .Bind(configuration.GetSection(nameof(FileWatcherOptions)))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<FileWatcherOptions>, FileWatcherOptionsValidator>();

        services
            .AddOptions<KernelMemoryOptions>()
            .Bind(configuration.GetSection(nameof(KernelMemoryOptions)))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<KernelMemoryOptions>, KernelMemoryOptionsValidator>();

        services
            .AddSingleton<IMessageStore, MessageStore>()
            .ConfigureHttpClient(configuration)
            .AddSingleton<IFileWatcherFactory, FileWatcherFactory>()
            .AddScoped<IFileWatcherService, FileWatcherService>()
            .AddHostedService<HttpWorker>();
    }
}