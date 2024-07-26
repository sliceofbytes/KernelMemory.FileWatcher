using KernelMemory.FileWatcher.Messages;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace KernelMemory.FileWatcher.Services;

/// <summary>
/// Defines the contract for a message store that handles file events.
/// </summary>
internal interface IMessageStore
{
    Task AddAsync(FileEvent fileEvent);
    Message? TakeNext();
    IReadOnlyList<Message> TakeAll();
    bool HasNext();
}

/// <summary>
/// Thread-safe message store for file events. Uses ConcurrentDictionary for storage,
/// SemaphoreSlim for write synchronization, and ObjectPool for StringBuilder efficiency.
/// </summary>
internal class MessageStore : IMessageStore, IDisposable
{
    private readonly ConcurrentDictionary<string, Message> _store = new();
    private readonly ILogger _logger;
    private readonly FileWatcherOptions _options;

    // Reusable StringBuilder pool to reduce memory allocations
    private readonly ObjectPool<StringBuilder> _pool;

     // Ensures single-threaded write access
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private bool _disposed;

    public MessageStore(ILogger logger, IOptions<FileWatcherOptions> options)
    {
        _logger = logger?.ForContext<MessageStore>() ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _pool = new DefaultObjectPoolProvider().CreateStringBuilderPool();
    }

    /// <summary>
    /// Asynchronously adds a file event to the message store.
    /// </summary>
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
                string documentId = BuildDocumentId(option.Index, fileEvent);
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

    /// <summary>
    /// Removes and returns the next message from the store, if available.
    /// </summary>
    public Message? TakeNext()
    {
        string? key = _store.Keys.FirstOrDefault();
        return key != null && _store.TryRemove(key, out var message) ? message : null;
    }

    /// <summary>
    /// Removes and returns all messages from the store.
    /// </summary>
    public IReadOnlyList<Message> TakeAll()
    {
        var messages = _store.Values.ToList();
        _store.Clear();
        return messages;
    }

    /// <summary>
    /// Checks if there are any messages in the store.
    /// </summary>
    public bool HasNext() => !_store.IsEmpty;

    /// <summary>
    /// Builds a document ID from the index and file event.
    /// </summary>
    private string BuildDocumentId(string index, FileEvent fileEvent)
    {
        const char separator = '_';
        var sb = _pool.Get();
        try
        {
            sb.Append(index)
              .Append(separator)
              .Append(Path.ChangeExtension(fileEvent.RelativePath, null)
                          .Replace(Path.DirectorySeparatorChar, separator)
                          .Replace(' ', separator)
                          .ToLowerInvariant());

            string documentPathing = sb.ToString();

            return Regex.Replace(
                documentPathing,
                $@"[^a-z0-9{Regex.Escape(separator.ToString())}]+",
                separator.ToString())
                .Replace($"{separator}{separator}", separator.ToString())
                .Trim(separator);
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