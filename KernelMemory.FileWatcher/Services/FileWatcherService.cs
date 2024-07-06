using KernelMemory.FileWatcher.Messages;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace KernelMemory.FileWatcher.Services;

internal interface IFileWatcherService
{
    Task WatchAsync();
}

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
    private readonly Task? _processingTask;

    private bool _disposed;

    public FileWatcherService(
        ILogger logger,
        IFileWatcherFactory fileWatcherFactory,
        IMessageStore messageStore,
        IOptions<FileWatcherOptions> options)
    {
        _logger = logger.ForContext<FileWatcherService>();
        _fileWatcherFactory = fileWatcherFactory;
        _options = options.Value;
        _messageStore = messageStore;
        _processingTask = Task.Run(ProcessFilesAsync);
    }

    public async Task WatchAsync()
    {
        foreach (var directory in _options.Directories)
        {
            WatchDirectory(directory);
        }

        if (_options.WaitForInitialScans)
        {
            await Task.WhenAll(_initialScanTasks);
        }
    }

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

        _logger.Information("Watching {DirectoryPath}", directory.Path);
    }

    private async Task ProcessFilesAsync()
    {
        try
        {
            foreach (string file in _filesToProcess.GetConsumingEnumerable(_cts.Token))
            {
                var directory = _options.Directories.First(d => file.StartsWith(d.Path));
                await EnqueueFileEventAsync(FileEventType.Upsert, file, directory);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

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

    private async Task InitialScanAsync(FileWatcherDirectoryOptions directory)
    {
        var searchOption = directory.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        await Task.Run(() =>
        {
            foreach (string file in Directory.EnumerateFiles(directory.Path, directory.Filter, searchOption))
            {
                _filesToProcess.Add(file);
            }
        });

        _logger.Information("Initial scan completed for {DirectoryPath}", directory.Path);
    }


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

    private async Task EnqueueFileEventAsync(FileEventType eventType, string fullPath, FileWatcherDirectoryOptions directory)
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
    }

    private void OnError(ErrorEventArgs e)
    {
        _logger.Error(e.GetException(), "An error occurred in the file watcher");
    }

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
                foreach (var watcher in _watchers.Values)
                {
                    try
                    {
                        _cts?.Cancel();
                        _cts?.Dispose();
                        _filesToProcess?.Dispose();
                        _processingTask?.Wait();
                        Task.WhenAll(_initialScanTasks).Wait();
                        watcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error disposing file watcher service references.");
                    }
                }

                _watchers.Clear();
            }

            _disposed = true;
        }
    }
}