namespace KernelMemory.FileWatcher.Services;

internal interface IFileSystemWatcher : IDisposable
{
    event EventHandler<FileSystemEventArgs> Changed;
    event EventHandler<FileSystemEventArgs> Created;
    event EventHandler<FileSystemEventArgs> Deleted;
    event EventHandler<RenamedEventArgs> Renamed;
    event EventHandler<ErrorEventArgs> Error;
    bool EnableRaisingEvents { get; set; }
}
