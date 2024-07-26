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
        _logger.Information("WatcherHostedService starting");
        try
        {
            return _fileWatcher.WatchAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "WatcherHostedService failed to start");
            throw;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("WatcherHostedService is stopping");
        return base.StopAsync(cancellationToken);
    }
}
