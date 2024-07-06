namespace KernelMemory.FileWatcher.Services;

internal class FileSystemWatcherWrapper(FileSystemWatcher watcher) : IFileSystemWatcher
{
    private readonly Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> _changedHandlers = [];
    private readonly Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> _createdHandlers = [];
    private readonly Dictionary<EventHandler<FileSystemEventArgs>, FileSystemEventHandler> _deletedHandlers = [];
    private readonly Dictionary<EventHandler<RenamedEventArgs>, RenamedEventHandler> _renamedHandlers = [];
    private readonly Dictionary<EventHandler<ErrorEventArgs>, ErrorEventHandler> _errorHandlers = [];
    private readonly FileSystemWatcher _watcher = watcher;
    private bool _disposed;

    public event EventHandler<FileSystemEventArgs>? Changed
    {
        add
        {
            if (value is null) return;
            void handler(object sender, FileSystemEventArgs args) => value(sender, args);
            _changedHandlers[value] = handler;
            _watcher.Changed += handler;
        }
        remove
        {
            if (value is null) return;
            if (_changedHandlers.TryGetValue(value, out var handler))
            {
                _watcher.Changed -= handler;
                _changedHandlers.Remove(value);
            }
        }
    }

    public event EventHandler<FileSystemEventArgs>? Created
    {
        add
        {
            if (value is null) return;
            void handler(object sender, FileSystemEventArgs args) => value(sender, args);
            _createdHandlers[value] = handler;
            _watcher.Created += handler;
        }
        remove
        {
            if (value is null) return;
            if (_createdHandlers.TryGetValue(value, out var handler))
            {
                _watcher.Created -= handler;
                _createdHandlers.Remove(value);
            }
        }
    }

    public event EventHandler<FileSystemEventArgs>? Deleted
    {
        add
        {
            if (value is null) return;
            void handler(object sender, FileSystemEventArgs args) => value(sender, args);
            _deletedHandlers[value] = handler;
            _watcher.Deleted += handler;
        }
        remove
        {
            if (value is null) return;
            if (_deletedHandlers.TryGetValue(value, out var handler))
            {
                _watcher.Deleted -= handler;
                _deletedHandlers.Remove(value);
            }
        }
    }

    public event EventHandler<RenamedEventArgs>? Renamed
    {
        add
        {
            if (value is null) return;
            void handler(object sender, RenamedEventArgs args) => value(sender, args);
            _renamedHandlers[value] = handler;
            _watcher.Renamed += handler;
        }
        remove
        {
            if (value is null) return;
            if (_renamedHandlers.TryGetValue(value, out var handler))
            {
                _watcher.Renamed -= handler;
                _renamedHandlers.Remove(value);
            }
        }
    }

    public event EventHandler<ErrorEventArgs>? Error
    {
        add
        {
            if (value is null) return;
            void handler(object sender, ErrorEventArgs args) => value(sender, args);
            _errorHandlers[value] = handler;
            _watcher.Error += handler;
        }
        remove
        {
            if (value is null) return;
            if (_errorHandlers.TryGetValue(value, out var handler))
            {
                _watcher.Error -= handler;
                _errorHandlers.Remove(value);
            }
        }
    }

    public bool EnableRaisingEvents
    {
        get => _watcher.EnableRaisingEvents;
        set => _watcher.EnableRaisingEvents = value;
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