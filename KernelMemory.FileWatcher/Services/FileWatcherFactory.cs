using System.Collections.Concurrent;

namespace KernelMemory.FileWatcher.Services;
internal interface IFileWatcherFactory
{
    IFileSystemWatcher Create(string directory, string filter, bool recursive);
}

internal class FileWatcherFactory(ILogger logger) : IFileWatcherFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, IFileSystemWatcher> _watchers = new();
    private readonly ILogger _logger = logger.ForContext<FileWatcherFactory>();
    private bool _disposed;

    public IFileSystemWatcher Create(string directory, string filter, bool recursive)
    {
        var watcher = new FileSystemWatcher(directory)
        {
            NotifyFilter = NotifyFilters.Attributes
                           | NotifyFilters.CreationTime
                           | NotifyFilters.DirectoryName
                           | NotifyFilters.FileName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size,
            Filter = filter,
            IncludeSubdirectories = recursive
        };

        var wrapper = new FileSystemWatcherWrapper(watcher);
        _watchers.TryAdd(directory, wrapper);

        return wrapper;
    }

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
                        watcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error disposing watcher");
                    }
                }

                _watchers.Clear();
            }

            _disposed = true;
        }
    }
}
