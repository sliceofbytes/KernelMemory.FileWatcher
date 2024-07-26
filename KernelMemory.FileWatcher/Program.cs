using KernelMemory.FileWatcher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Retry;
using Serilog.Events;

namespace KernelMemory.FileWatcher;

internal class Program
{
    protected Program()
    {
    }

    internal static async Task Main(string[] args)
    {
        // Configure and create the initial logger
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateBootstrapLogger();

        while (true)
        {
            try
            {
                // Build and start the host
                Log.Information("Building Host - {Status}", "Started");
                using var host = CreateHostBuilder(args).Build();
                Log.Information("Building Host - {Status}", "Complete");

                Log.Information("Application Startup - {Status}", "Started");
                await host.StartAsync();
                Log.Information("Application Startup - {Status}", "Complete");

                // Wait for the application to stop
                await host.WaitForShutdownAsync();

                // Check if the application was stopped normally
                if (host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested)
                {
                    Log.Information("Application stop requested. Restarting...");

                    // Restart the application
                    continue;
                }

                // Normal exit
                break;
            }
            catch (OptionsValidationException ove)
            {
                // Handle configuration validation errors
                Log.Error(ove, "Configuration is invalid. Retrying in 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                Log.Fatal(ex, "Host terminated unexpectedly");
                break;
            }
        }

        // Ensure all logs are written before exiting
        await Log.CloseAndFlushAsync();
    }

    // Create and configure the host
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

    // Build the configuration
    private static IConfiguration BuildConfiguration()
    {
        // Determine the base path for configuration files
        string basePath = File.Exists(Path.Combine(DefaultOptions.ConfigFolder, DefaultOptions.ConfigFileName))
            ? DefaultOptions.ConfigFolder
            : AppContext.BaseDirectory;

        // Build and return the configuration
        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(DefaultOptions.ConfigFileName, optional: false, reloadOnChange: true)
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();
    }

    // Configure services for dependency injection
    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configure logging
        services.ConfigureLogging(configuration);

        // Configure and validate FileWatcherOptions
        services
            .AddOptions<FileWatcherOptions>()
            .Bind(configuration.GetSection(nameof(FileWatcherOptions)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Configure and validate KernelMemoryOptions
        services
            .AddOptions<KernelMemoryOptions>()
            .Bind(configuration.GetSection(nameof(KernelMemoryOptions)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add options validators
        services.AddSingleton<IValidateOptions<FileWatcherOptions>, FileWatcherOptionsValidator>();
        services.AddSingleton<IValidateOptions<KernelMemoryOptions>, KernelMemoryOptionsValidator>();

        // Configure Kernel Memory Http Client
        services.ConfigureHttpClient(configuration);

        // Register policies for dependency injection
        services.AddSingleton<IRetryPolicy<HttpResponseMessage>>(sp =>
            HttpClientStartup.GetRetryPolicy(sp.GetRequiredService<IOptions<KernelMemoryOptions>>().Value));
        services.AddSingleton<ICircuitBreakerPolicy<HttpResponseMessage>>(sp =>
            HttpClientStartup.GetCircuitBreakerPolicy(sp.GetRequiredService<IOptions<KernelMemoryOptions>>().Value));

        // Register services and hosted services
        services
            .AddSingleton<IMessageStore, MessageStore>()
            .AddSingleton<IFileWatcherFactory, FileWatcherFactory>()
            .AddSingleton<IFileWatcherService, FileWatcherService>()
            .AddHostedService<ConfigurationMonitor>()
            .AddHostedService<WatcherHostedService>()
            .AddHostedService<HttpWorker>();

        // Configure host options
        services.Configure<HostOptions>(opts =>
            opts.ShutdownTimeout = TimeSpan.FromSeconds(DefaultOptions.HostShutdownTimeout));
    }
}