using System.Collections.Concurrent;

namespace KernelMemory.FileWatcher.Services;

/// <summary>
/// Defines the contract for creating file system watchers.
/// </summary>
internal interface IFileWatcherFactory
{
    IFileSystemWatcher Create(string directory, string filter, bool recursive);
}

/// <summary>
/// Factory for creating and managing FileSystemWatcher instances.
/// </summary>
internal class FileWatcherFactory : IFileWatcherFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, IFileSystemWatcher> _watchers = new();
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the FileWatcherFactory.
    /// </summary>
    /// <param name="logger">The logger to use for this factory.</param>
    public FileWatcherFactory(ILogger logger)
    {
        _logger = logger.ForContext<FileWatcherFactory>();
    }

    /// <summary>
    /// Creates a new FileSystemWatcher instance wrapped in an IFileSystemWatcher.
    /// </summary>
    /// <param name="directory">The directory to watch.</param>
    /// <param name="filter">The file filter to apply.</param>
    /// <param name="recursive">Whether to watch subdirectories.</param>
    /// <returns>An IFileSystemWatcher instance.</returns>
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

        _logger.Information("Created file watcher for directory: {Directory}", directory);

        return wrapper;
    }

    /// <summary>
    /// Disposes the FileWatcherFactory and all created watchers.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
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
                        _logger.Information("Disposed watcher for directory: {Directory}", watcher.Path);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error disposing watcher for directory: {Directory}", watcher.Path);
                    }
                }

                _watchers.Clear();
            }

            _disposed = true;
        }
    }
}