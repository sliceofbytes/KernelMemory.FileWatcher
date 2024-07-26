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
                using var host = CreateHostBuilder(args).Build();
                Log.Information("Host built...");
                var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

                using var cts = new CancellationTokenSource();
                lifetime.ApplicationStopping.Register(() => cts.Cancel());

                Log.Information("Starting Application");
                try
                {
                    await host.StartAsync(cts.Token);
                    Log.Information("Host.StartAsync completed successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception occurred during host.StartAsync");
                    throw; // Re-throw to be caught by the outer try-catch
                }

                // Log all hosted services
                var hostedServices = host.Services.GetServices<IHostedService>();
                foreach (var service in hostedServices)
                {
                    Log.Information("Hosted service registered: {ServiceType}", service.GetType().Name);
                }

                // Wait for the application to stop
                await host.WaitForShutdownAsync(cts.Token);

                if (cts.IsCancellationRequested)
                {
                    Log.Information("Application stop requested. Restarting...");
                    continue; // Restart the application
                }

                break; // Normal exit

            }
            catch (OptionsValidationException ove)
            {
                Console.WriteLine($"Configuration is invalid: {ove.Message}");
                Log.Error(ove, "Configuration is invalid. Retrying in 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
                break; // Exit the loop on unexpected errors
            }
        }

        await Log.CloseAndFlushAsync();
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
            .AddJsonFile(DefaultOptions.ConfigFileName, optional: false, reloadOnChange: true)
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

        services
            .AddOptions<KernelMemoryOptions>()
            .Bind(configuration.GetSection(nameof(KernelMemoryOptions)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<FileWatcherOptions>, FileWatcherOptionsValidator>();
        services.AddSingleton<IValidateOptions<KernelMemoryOptions>, KernelMemoryOptionsValidator>();

        // Configure Kernel Memory Http Client
        services.ConfigureHttpClient(configuration);

        // Register policies for dependency injection for configuration changes.
        services.AddSingleton<IRetryPolicy<HttpResponseMessage>>(sp => HttpClientStartup.GetRetryPolicy(sp.GetRequiredService<IOptions<KernelMemoryOptions>>().Value));
        services.AddSingleton<ICircuitBreakerPolicy<HttpResponseMessage>>(sp => HttpClientStartup.GetCircuitBreakerPolicy(sp.GetRequiredService<IOptions<KernelMemoryOptions>>().Value));

        services
             .AddHostedService<HttpWorker>()
            .AddHostedService<ConfigurationMonitor>()
            .AddHostedService<WatcherHostedService>()
            .AddSingleton<IMessageStore, MessageStore>()
            .AddSingleton<IFileWatcherFactory, FileWatcherFactory>()
            .AddSingleton<IFileWatcherService, FileWatcherService>();
            //.AddSingleton<IMessageStore, MessageStore>()
            //.AddSingleton<IFileWatcherFactory, FileWatcherFactory>()
            //.AddSingleton<IFileWatcherService, FileWatcherService>()
            //.AddHostedService<ConfigurationMonitor>()
            //.AddHostedService<WatcherHostedService>()
            //.AddHostedService<HttpWorker>();

        services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(DefaultOptions.HostShutdownTimeout));
    }
}