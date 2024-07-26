using Microsoft.Extensions.Hosting;

namespace KernelMemory.FileWatcher.Services;
internal class WatcherHostedService : IHostedService
{
    private readonly IFileWatcherService _fileWatcher;
    private readonly ILogger _logger;

    public WatcherHostedService(IFileWatcherService fileWatcher, ILogger logger)
    {
        _fileWatcher = fileWatcher;
        _logger = logger.ForContext<WatcherHostedService>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("WatcherHostedService starting");
        try
        {
            await _fileWatcher.WatchAsync(cancellationToken);
            _logger.Information("WatcherHostedService started successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "WatcherHostedService failed to start");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("WatcherHostedService is stopping");
        return Task.CompletedTask;
    }

}
