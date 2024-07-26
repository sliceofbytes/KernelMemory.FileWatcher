using Microsoft.Extensions.Hosting;

namespace KernelMemory.FileWatcher.Services;

/// <summary>
/// A hosted service that manages the lifecycle of the FileWatcherService.
/// This service runs in the background and is responsible for starting and stopping the file watching process.
/// </summary>
internal class WatcherHostedService : BackgroundService
{
    private readonly IFileWatcherService _fileWatcher;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the WatcherHostedService.
    /// </summary>
    /// <param name="fileWatcher">The file watcher service to be managed.</param>
    /// <param name="logger">The logger for this service.</param>
    public WatcherHostedService(IFileWatcherService fileWatcher, ILogger logger)
    {
        _fileWatcher = fileWatcher;
        _logger = logger.ForContext<WatcherHostedService>();
    }

    /// <summary>
    /// This method is called when the IHostedService starts. It's responsible for initiating the file watching process.
    /// </summary>
    /// <param name="stoppingToken">A CancellationToken that signals when the host is stopping.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Watcher Hosted Service - {Status}", "Starting");
        try
        {
            // Start the file watching process
            var task = _fileWatcher.WatchAsync(stoppingToken);
            _logger.Information("Watcher Hosted Service - {Status}", "Started");
            return task;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Watcher Hosted Service - {Status}", "Startup Failed");
            throw;
        }
    }

    /// <summary>
    /// This method is called when the IHostedService is stopping. It's responsible for gracefully stopping the file watching process.
    /// </summary>
    /// <param name="cancellationToken">A CancellationToken that signals when the stop operation should no longer be graceful.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Watcher Hosted Service - {Status}", "Stopping");
        var stopTask = base.StopAsync(cancellationToken);
        _logger.Information("Watcher Hosted Service - {Status}", "Stopped");
        return stopTask;
    }
}