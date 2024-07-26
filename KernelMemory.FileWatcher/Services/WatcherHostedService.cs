using Microsoft.Extensions.Hosting;

namespace KernelMemory.FileWatcher.Services;
internal class WatcherHostedService : BackgroundService
{
    private readonly IFileWatcherService _fileWatcher;
    private readonly ILogger _logger;

    public WatcherHostedService(IFileWatcherService fileWatcher, ILogger logger)
    {
        _fileWatcher = fileWatcher;
        _logger = logger.ForContext<WatcherHostedService>();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Watcher Hosted Service - {Status}", "Starting");
        try
        {
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

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Watcher Hosted Service - {Status}", "Stopping");
        var stopTask = base.StopAsync(cancellationToken);
        _logger.Information("Watcher Hosted Service - {Status}", "Stopped");
        return stopTask;
    }
}
