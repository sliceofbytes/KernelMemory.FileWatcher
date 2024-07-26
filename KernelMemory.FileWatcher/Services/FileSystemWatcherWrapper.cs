namespace KernelMemory.FileWatcher.Services;

/// <summary>
/// Wraps FileSystemWatcher to manage event handlers and improve testability.
/// </summary>
internal class FileSystemWatcherWrapper : IFileSystemWatcher
{
    private readonly Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> _changedHandlers = [];
    private readonly Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> _createdHandlers = [];
    private readonly Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> _deletedHandlers = [];
    private readonly Dictionary<EventHandler<RenamedEventArgs>, RenamedEventHandler> _renamedHandlers = [];
    private readonly Dictionary<EventHandler<ErrorEventArgs>, ErrorEventHandler> _errorHandlers = [];
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    public FileSystemWatcherWrapper(FileSystemWatcher watcher)
    {
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
    }

    public event EventHandler<FileSystemEventArgs>? Changed
    {
        add => AddFileSystemEventHandler(_changedHandlers, value, handler => _watcher.Changed += handler);
        remove => RemoveFileSystemEventHandler(_changedHandlers, value, handler => _watcher.Changed -= handler);
    }

    public event EventHandler<FileSystemEventArgs>? Created
    {
        add => AddFileSystemEventHandler(_createdHandlers, value, handler => _watcher.Created += handler);
        remove => RemoveFileSystemEventHandler(_createdHandlers, value, handler => _watcher.Created -= handler);
    }

    public event EventHandler<FileSystemEventArgs>? Deleted
    {
        add => AddFileSystemEventHandler(_deletedHandlers, value, handler => _watcher.Deleted += handler);
        remove => RemoveFileSystemEventHandler(_deletedHandlers, value, handler => _watcher.Deleted -= handler);
    }

    public event EventHandler<RenamedEventArgs>? Renamed
    {
        add => AddRenamedEventHandler(_renamedHandlers, value, handler => _watcher.Renamed += handler);
        remove => RemoveRenamedEventHandler(_renamedHandlers, value, handler => _watcher.Renamed -= handler);
    }

    public event EventHandler<ErrorEventArgs>? Error
    {
        add => AddErrorEventHandler(_errorHandlers, value, handler => _watcher.Error += handler);
        remove => RemoveErrorEventHandler(_errorHandlers, value, handler => _watcher.Error -= handler);
    }

    /// <summary>
    /// Gets the path of the directory to watch.
    /// </summary>
    public string Path
    {
        get => _watcher.Path;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the component is enabled.
    /// </summary>
    public bool EnableRaisingEvents
    {
        get => _watcher.EnableRaisingEvents;
        set => _watcher.EnableRaisingEvents = value;
    }

    private void AddFileSystemEventHandler(
        Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> handlers,
        EventHandler<FileSystemEventArgs>? value,
        Action<FileSystemEventHandler> addToWatcher)
    {
        if (value is null) return;
        var handler = new FileSystemEventHandler((sender, args) => value(sender, args));
        handlers[value] = handler;
        addToWatcher(handler);
    }

    private void RemoveFileSystemEventHandler(
        Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> handlers,
        EventHandler<FileSystemEventArgs>? value,
        Action<FileSystemEventHandler> removeFromWatcher)
    {
        if (value is null) return;
        if (handlers.TryGetValue(value, out var handler))
        {
            removeFromWatcher(handler);
            handlers.Remove(value);
        }
    }

    private void AddRenamedEventHandler(
        Dictionary<EventHandler<RenamedEventArgs>, RenamedEventHandler> handlers,
        EventHandler<RenamedEventArgs>? value,
        Action<RenamedEventHandler> addToWatcher)
    {
        if (value is null) return;
        var handler = new RenamedEventHandler((sender, args) => value(sender, args));
        handlers[value] = handler;
        addToWatcher(handler);
    }

    private void RemoveRenamedEventHandler(
        Dictionary<EventHandler<RenamedEventArgs>, RenamedEventHandler> handlers,
        EventHandler<RenamedEventArgs>? value,
        Action<RenamedEventHandler> removeFromWatcher)
    {
        if (value is null) return;
        if (handlers.TryGetValue(value, out var handler))
        {
            removeFromWatcher(handler);
            handlers.Remove(value);
        }
    }

    private void AddErrorEventHandler(
        Dictionary<EventHandler<ErrorEventArgs>, ErrorEventHandler> handlers,
        EventHandler<ErrorEventArgs>? value,
        Action<ErrorEventHandler> addToWatcher)
    {
        if (value is null) return;
        var handler = new ErrorEventHandler((sender, args) => value(sender, args));
        handlers[value] = handler;
        addToWatcher(handler);
    }

    private void RemoveErrorEventHandler(
        Dictionary<EventHandler<ErrorEventArgs>, ErrorEventHandler> handlers,
        EventHandler<ErrorEventArgs>? value,
        Action<ErrorEventHandler> removeFromWatcher)
    {
        if (value is null) return;
        if (handlers.TryGetValue(value, out var handler))
        {
            removeFromWatcher(handler);
            handlers.Remove(value);
        }
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
                _watcher.Dispose();
                _changedHandlers.Clear();
                _createdHandlers.Clear();
                _deletedHandlers.Clear();
                _renamedHandlers.Clear();
                _errorHandlers.Clear();
            }

            _disposed = true;
        }
    }
}