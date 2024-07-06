using KernelMemory.FileWatcher.Messages;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;

namespace KernelMemory.FileWatcher.Services;

internal interface IMessageStore
{
    Task AddAsync(FileEvent fileEvent);
    Message? TakeNext();
    IReadOnlyList<Message> TakeAll();
    bool HasNext();
}

internal class MessageStore(ILogger logger, IOptions<FileWatcherOptions> options) : IMessageStore, IDisposable
{
    private readonly ConcurrentDictionary<string, Message> _store = new();
    private readonly ILogger _logger = logger?.ForContext<MessageStore>() ?? throw new ArgumentNullException(nameof(logger));
    private readonly FileWatcherOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ObjectPool<StringBuilder> _pool = new DefaultObjectPoolProvider().CreateStringBuilderPool();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public async Task AddAsync(FileEvent fileEvent)
    {
        ArgumentNullException.ThrowIfNull(fileEvent);

        if (fileEvent.EventType == FileEventType.Ignore)
        {
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            var option = _options.Directories.Find(d =>
                fileEvent.Directory.StartsWith(d.Path, StringComparison.OrdinalIgnoreCase));

            if (option != null)
            {
                string documentId = BuildDocumentId(option.Index, fileEvent.FileName);
                var item = new Message(fileEvent, option.Index, documentId);

                _store[item.DocumentId] = item;

                _logger.Information(
                    "Added event {DocumentId} for file {FileName} of type {EventType} to the store",
                    documentId,
                    item.Event.FileName,
                    item.Event.EventType);
            }
            else
            {
                _logger.Warning("No matching directory found for file {FileName}", fileEvent.FileName);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Message? TakeNext()
    {
        string? key = _store.Keys.FirstOrDefault();
        return key != null && _store.TryRemove(key, out var message) ? message : null;
    }

    public IReadOnlyList<Message> TakeAll()
    {
        var messages = _store.Values.ToList();
        _store.Clear();
        return messages;
    }

    public bool HasNext() => !_store.IsEmpty;

    private string BuildDocumentId(string index, string fileName)
    {
        const char separator = '_';
        var sb = _pool.Get();
        try
        {
            sb.Append(index)
              .Append(separator)
              .Append(fileName.Replace(Path.DirectorySeparatorChar, separator).Replace(' ', separator));
            return sb.ToString();
        }
        finally
        {
            _pool.Return(sb);
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
                _store.Clear();
                _semaphore.Dispose();
            }

            _disposed = true;
        }
    }
}