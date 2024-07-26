using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace KernelMemory.FileWatcher.Services;

internal class ConfigurationMonitor : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<FileWatcherOptions> _fileWatcherOptions;
    private readonly IOptionsMonitor<KernelMemoryOptions> _kernelMemoryOptions;
    private readonly ILogger _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public ConfigurationMonitor(
        IConfiguration configuration,
        IOptionsMonitor<FileWatcherOptions> fileWatcherOptions,
        IOptionsMonitor<KernelMemoryOptions> kernelMemoryOptions,
        ILogger logger,
        IHostApplicationLifetime applicationLifetime)
    {
        _configuration = configuration;
        _fileWatcherOptions = fileWatcherOptions;
        _kernelMemoryOptions = kernelMemoryOptions;
        _logger = logger.ForContext<ConfigurationMonitor>();
        _applicationLifetime = applicationLifetime;
        _logger.Information("Configuration Monitor Constructed");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Configuration Monitor Started");
        _fileWatcherOptions.OnChange(_ => ValidateConfiguration());
        _kernelMemoryOptions.OnChange(_ => ValidateConfiguration());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Configuration Monitor Stopping");
        return Task.CompletedTask;
    }

    private void ValidateConfiguration()
    {
        try
        {
            var fileWatcherOptions = _configuration.GetSection(nameof(FileWatcherOptions)).Get<FileWatcherOptions>();
            var kernelMemoryOptions = _configuration.GetSection(nameof(KernelMemoryOptions)).Get<KernelMemoryOptions>();

            if (fileWatcherOptions == null || kernelMemoryOptions == null)
            {
                throw new InvalidOperationException("Configuration sections are missing.");
            }

            Validator.ValidateObject(fileWatcherOptions, new ValidationContext(fileWatcherOptions), true);
            Validator.ValidateObject(kernelMemoryOptions, new ValidationContext(kernelMemoryOptions), true);

            _logger.Information("Configuration validated successfully.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Configuration validation failed. Stopping the application.");
            _applicationLifetime.StopApplication();
        }
    }
}
