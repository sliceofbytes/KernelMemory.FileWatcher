using KernelMemory.FileWatcher.Messages;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace KernelMemory.FileWatcher.Services;

/// <summary>
/// Defines the contract for the file watcher service.
/// </summary>
internal interface IFileWatcherService
{
    Task WatchAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Implements the file watching service, managing multiple directory watchers and processing file events.
/// </summary>
internal class FileWatcherService : IFileWatcherService, IDisposable
{
    private readonly ILogger _logger;
    private readonly IFileWatcherFactory _fileWatcherFactory;
    private readonly FileWatcherOptions _options;
    private readonly IMessageStore _messageStore;
    private readonly ConcurrentDictionary<string, IFileSystemWatcher> _watchers = new();
    private readonly BlockingCollection<string> _filesToProcess = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _initialScanTasks = [];
    private Task? _processingTask;
    private Task? _watchTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the FileWatcherService.
    /// </summary>
    public FileWatcherService(
        IFileWatcherFactory fileWatcherFactory,
        IMessageStore messageStore,
        IOptions<FileWatcherOptions> options)
    {
        _logger = Log.ForContext<FileWatcherService>();
        _fileWatcherFactory = fileWatcherFactory ?? throw new ArgumentNullException(nameof(fileWatcherFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _messageStore = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
    }

    /// <summary>
    /// Starts the file watching process asynchronously.
    /// </summary>
    public Task WatchAsync(CancellationToken cancellationToken)
    {
        _watchTask = Task.Run(() => WatchInternalAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal method to handle the file watching process.
    /// </summary>
    private async Task WatchInternalAsync(CancellationToken cancellationToken)
    {
        _logger.Information("File Watcher Service - {Status}", "Starting");
        foreach (var directory in _options.Directories)
        {
            WatchDirectory(directory);
        }

        _processingTask = ProcessFilesAsync(cancellationToken);

        if (_options.WaitForInitialScans)
        {
            _logger.Information("Waiting for initial scans to complete");
            await Task.WhenAll(_initialScanTasks);
            _logger.Information("Initial scans completed");
        }

        _logger.Information("File watching setup completed, now running indefinitely");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException oce)
        {
            _logger.Information(oce, "File Watcher Service - {Status}", "Cancelled");
        }
    }

    /// <summary>
    /// Sets up a watcher for a specific directory.
    /// </summary>
    private void WatchDirectory(FileWatcherDirectoryOptions directory)
    {
        var watcher = _fileWatcherFactory.Create(directory.Path, directory.Filter, directory.IncludeSubdirectories);

        watcher.Changed += async (s, e) => await WrapEventHandlerAsync(nameof(watcher.Changed), e, () => EnqueueEventAsync(e, directory));
        watcher.Created += async (s, e) => await WrapEventHandlerAsync(nameof(watcher.Created), e, () => EnqueueEventAsync(e, directory));
        watcher.Deleted += async (s, e) => await WrapEventHandlerAsync(nameof(watcher.Deleted), e, () => EnqueueEventAsync(e, directory));
        watcher.Renamed += async (s, e) => await WrapEventHandlerAsync(nameof(watcher.Renamed), e, () => EnqueueEventAsync(e, directory));
        watcher.Error += (s, e) => OnError(e);
        watcher.EnableRaisingEvents = true;

        _watchers.TryAdd(directory.Path, watcher);

        if (directory.InitialScan)
        {
            var initialScanTask = Task.Run(async () =>
            {
                try
                {
                    await InitialScanAsync(directory);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during initial scan of {DirectoryPath}", directory.Path);
                }
            });

            _initialScanTasks.Add(initialScanTask);
        }

        _logger.Information("File Watcher Service - {Status} {Path}", "Watching", directory.Path);
    }

    /// <summary>
    /// Processes files asynchronously from the queue.
    /// </summary>
    private async Task ProcessFilesAsync(CancellationToken cancellationToken)
    {
        _logger.Information("File Watcher Service - {Status}", "Process Loop Started");
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_filesToProcess.TryTake(out string? file, Timeout.Infinite, _cts.Token))
                {
                    _logger.Information("Processing file: {File}", file);
                    var directory = _options.Directories.First(d => file.StartsWith(d.Path));
                    await EnqueueFileEventAsync(FileEventType.Upsert, file, directory);
                }
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.Information(oce, "File processing loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in ProcessFilesAsync");
        }
        finally
        {
            _logger.Information("File processing loop ended");
        }
    }

    /// <summary>
    /// Wraps event handlers to provide consistent error handling.
    /// </summary>
    private async Task WrapEventHandlerAsync(string eventType, FileSystemEventArgs e, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (IOException ioEx)
        {
            _logger.Error(ioEx, "IO error processing {EventType} event for {FileName}. The file may be in use.", eventType, e.FullPath);
        }
        catch (UnauthorizedAccessException uaEx)
        {
            _logger.Error(uaEx, "Access denied when processing {EventType} event for {FileName}. Check file permissions.", eventType, e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error processing {EventType} event for {FileName}", eventType, e.FullPath);
        }
    }

    /// <summary>
    /// Performs an initial scan of a directory.
    /// </summary>
    private async Task InitialScanAsync(FileWatcherDirectoryOptions directory)
    {
        _logger.Information("File Watcher Service - {Status}", "Initial Scan Starting");
        var searchOption = directory.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        await Task.Run(() =>
        {
            foreach (string file in Directory.EnumerateFiles(directory.Path, directory.Filter, searchOption))
            {
                _filesToProcess.Add(file);
            }

            _logger.Information("File Watcher Service - {Status}", "Initial Scan Complete");
        });
    }

    /// <summary>
    /// Enqueues a file event for processing.
    /// </summary>
    private async Task EnqueueEventAsync(FileSystemEventArgs e, FileWatcherDirectoryOptions directory)
    {
        var eventType = ConvertEventTypes(e.ChangeType);
        if (e.ChangeType == WatcherChangeTypes.Renamed && e is RenamedEventArgs renamedArgs)
        {
            await EnqueueFileEventAsync(FileEventType.Delete, renamedArgs.OldFullPath, directory);
            await EnqueueFileEventAsync(FileEventType.Upsert, renamedArgs.FullPath, directory);
        }
        else
        {
            await EnqueueFileEventAsync(eventType, e.FullPath, directory);
        }
    }

    /// <summary>
    /// Enqueues a file event with retry logic.
    /// </summary>
    private async Task EnqueueFileEventAsync(FileEventType eventType, string fullPath, FileWatcherDirectoryOptions directory)
    {
        int retryCount = 0;
        const int maxRetries = 3;
        const int retryDelay = 1000; // 1 second

        while (retryCount < maxRetries)
        {
            try
            {
                string fileName = Path.GetFileName(fullPath);
                string fileDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;
                string fileRelativePath = Path.GetRelativePath(directory.Path, fullPath);
                var fileEvent = new FileEvent(
                    eventType: eventType,
                    fileName: fileName,
                    directory: fileDirectory,
                    relativePath: fileRelativePath);

                await _messageStore.AddAsync(fileEvent);
                return;
            }
            catch (IOException ex)
            {
                _logger.Warning(ex, "IO error when enqueueing file event for {FullPath}. Retry {RetryCount} of {MaxRetries}", fullPath, retryCount + 1, maxRetries);
                await Task.Delay(retryDelay);
                retryCount++;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error when enqueueing file event for {FullPath}", fullPath);
                return;
            }
        }

        _logger.Error("Failed to enqueue file event for {FullPath} after {MaxRetries} retries", fullPath, maxRetries);
    }

    /// <summary>
    /// Handles errors from the file system watcher.
    /// </summary>
    private void OnError(ErrorEventArgs e)
    {
        _logger.Error(e.GetException(), "An error occurred in the file watcher");
    }

    /// <summary>
    /// Converts FileSystemWatcher change types to FileEventType.
    /// </summary>
    private static FileEventType ConvertEventTypes(WatcherChangeTypes wct) =>
        wct switch
        {
            WatcherChangeTypes.Deleted => FileEventType.Delete,
            WatcherChangeTypes.Changed => FileEventType.Upsert,
            WatcherChangeTypes.Created => FileEventType.Upsert,
            _ => FileEventType.Ignore
        };

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Send cancel signal to start the dispose process.
                _cts?.Cancel();
                _watchTask?.Wait();

                // Wait for the initial scan to complete or respond to cancel.
                Task.WhenAll(_initialScanTasks).Wait();

                // Wait for the processing task to complete or respond to cancel.
                _processingTask?.Wait();

                foreach (var watcher in _watchers.Values)
                {
                    try
                    {
                        watcher?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error disposing file watcher service references.");
                    }
                }

                // Clear the watchers collection.
                _watchers.Clear();

                // Dispose of the blocking collection 2nd to last before the cancellation token.
                _filesToProcess?.Dispose();

                // Dispose of cancellation token last.
                _cts?.Dispose();
            }

            _disposed = true;
        }
    }
}