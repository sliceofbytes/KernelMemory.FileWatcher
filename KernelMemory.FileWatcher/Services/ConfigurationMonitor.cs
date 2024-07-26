using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace KernelMemory.FileWatcher.Services;

/// <summary>
/// Monitors and validates configuration changes for FileWatcher and KernelMemory options.
/// </summary>
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
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _fileWatcherOptions = fileWatcherOptions ?? throw new ArgumentNullException(nameof(fileWatcherOptions));
        _kernelMemoryOptions = kernelMemoryOptions ?? throw new ArgumentNullException(nameof(kernelMemoryOptions));
        _logger = logger?.ForContext<ConfigurationMonitor>() ?? throw new ArgumentNullException(nameof(logger));
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        _logger.Information("Configuration Monitor - {Status}", "Constructed");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Configuration Monitor - {Status}", "Starting");
        _fileWatcherOptions.OnChange(_ => ValidateConfiguration());
        _kernelMemoryOptions.OnChange(_ => ValidateConfiguration());
        _logger.Information("Configuration Monitor - {Status}", "Started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Configuration Monitor - {Status}", "Stopping");

        // No specific stop actions needed
        _logger.Information("Configuration Monitor - {Status}", "Stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the current configuration when changes occur.
    /// </summary>
    private void ValidateConfiguration()
    {
        _logger.Information("Configuration Monitor - {Status}", "Validating Configuration");
        try
        {
            var fileWatcherOptions = _configuration.GetSection(nameof(FileWatcherOptions)).Get<FileWatcherOptions>();
            var kernelMemoryOptions = _configuration.GetSection(nameof(KernelMemoryOptions)).Get<KernelMemoryOptions>();

            if (fileWatcherOptions == null || kernelMemoryOptions == null)
            {
                throw new InvalidOperationException("Configuration sections are missing.");
            }

            Validator.ValidateObject(fileWatcherOptions, new ValidationContext(fileWatcherOptions), validateAllProperties: true);
            Validator.ValidateObject(kernelMemoryOptions, new ValidationContext(kernelMemoryOptions), validateAllProperties: true);

            _logger.Information("Configuration Monitor - {Status}", "Validation Successful");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Configuration Monitor - {Status}", "Validation Failed");
            _applicationLifetime.StopApplication();
        }
    }
}