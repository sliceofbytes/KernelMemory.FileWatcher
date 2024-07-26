using KernelMemory.FileWatcher.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using System.Net.Http.Headers;

namespace KernelMemory.FileWatcher.Services;

/// <summary>
/// Processes messages from the message store and sends them to the Kernel Memory service.
/// </summary>
internal class HttpWorker : IHostedService, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMessageStore _store;
    private readonly KernelMemoryOptions _options;
    private readonly TimeSpan _processingInterval;
    private readonly CancellationTokenSource _stoppingCts = new();
    private Task? _executingTask;
    private Timer? _timer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the HttpWorker class.
    /// </summary>
    public HttpWorker(
        ILogger logger,
        IOptions<KernelMemoryOptions> options,
        IHttpClientFactory httpClientFactory,
        IMessageStore messageStore)
    {
        _logger = logger?.ForContext<HttpWorker>() ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _store = messageStore ?? throw new ArgumentNullException(nameof(messageStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _processingInterval = _options.Schedule;

        _logger.Information("HttpWorker constructed");
    }

    /// <summary>
    /// Starts the HttpWorker service.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("HttpWorker starting");
        _timer = new Timer(ExecuteTask, null, TimeSpan.Zero, _processingInterval);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes the message processing task.
    /// </summary>
    private void ExecuteTask(object? state)
    {
        _logger.Debug("HttpWorker timer tick");
        _executingTask = ProcessMessagesAsync(_stoppingCts.Token);
    }

    /// <summary>
    /// Stops the HttpWorker service.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("HttpWorker is stopping");

        if (_timer != null)
            await _timer.DisposeAsync();

        try
        {
            // Signal cancellation to the executing method
            await _stoppingCts.CancelAsync();
        }
        finally
        {
            // Wait until the task completes or the stop token triggers
            if (_executingTask != null)
            {
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }

        _logger.Information("HttpWorker stopped");
    }

    /// <summary>
    /// Processes all messages in the store.
    /// </summary>
    private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.Debug("Checking for messages to process");
            var messages = _store.TakeAll();
            _logger.Information("Found {Count} messages to process", messages.Count);

            if (messages.Count == 0)
            {
                _logger.Debug("No messages to process");
                return;
            }

            var tasks = messages.Select(message => ProcessMessageAsync(message, stoppingToken));
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException oce)
        {
            _logger.Information(oce, "Message processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred while processing messages");
        }
    }

    /// <summary>
    /// Processes a single message.
    /// </summary>
    private async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        if (message.Event?.EventType == FileEventType.Ignore)
        {
            return;
        }

        _logger.Information("Processing message {DocumentId} for file {FileName} of type {EventType}", message.DocumentId, message.Event?.FileName, message.Event?.EventType);

        try
        {
            var client = _httpClientFactory.CreateClient(DefaultOptions.HttpClientName);
            HttpResponseMessage? response = null;

            switch (message.Event?.EventType)
            {
                case FileEventType.Upsert:
                    response = await UpsertDocumentAsync(client, message, stoppingToken);
                    break;
                case FileEventType.Delete:
                    response = await DeleteDocumentAsync(client, message, stoppingToken);
                    break;
                default:
                    _logger.Warning("Unexpected event type {EventType} for message {DocumentId}", message.Event?.EventType, message.DocumentId);
                    return;
            }

            if (response?.IsSuccessStatusCode == true)
            {
                _logger.Information("Successfully processed message {DocumentId}", message.DocumentId);
            }
            else
            {
                _logger.Error("Failed to process message {DocumentId}. Status code: {StatusCode}", message.DocumentId, response?.StatusCode);
            }
        }
        catch (HttpRequestException ex) when (ex.InnerException is BrokenCircuitException)
        {
            _logger.Error(ex, "Circuit breaker is open. KernelMemory service is unavailable");
        }
        catch (ArgumentException ex)
        {
            _logger.Error(ex, "Invalid message data for {DocumentId}", message.DocumentId);
        }
        catch (FileNotFoundException ex)
        {
            _logger.Error(ex, "File not found for message {DocumentId}", message.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing message {DocumentId}", message.DocumentId);
        }
    }

    /// <summary>
    /// Sends an upsert request to the Kernel Memory service.
    /// </summary>
    private static async Task<HttpResponseMessage> UpsertDocumentAsync(HttpClient client, Message message, CancellationToken stoppingToken)
    {
        if (message.Event == null)
        {
            throw new ArgumentException("Message Event cannot be null for Upsert operation.", nameof(message));
        }

        string fullPath = Path.Combine(message.Event.Directory, message.Event.FileName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fullPath}", fullPath);
        }

        using var content = new MultipartFormDataContent();
        await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(message.Event.FileName));
        content.Add(fileContent, "file", message.Event.FileName);
        content.Add(new StringContent(message.Index), "index");
        content.Add(new StringContent(message.DocumentId), "documentId");

        return await client.PostAsync("/upload", content, stoppingToken);
    }

    /// <summary>
    /// Sends a delete request to the Kernel Memory service.
    /// </summary>
    private static async Task<HttpResponseMessage> DeleteDocumentAsync(HttpClient client, Message message, CancellationToken stoppingToken)
    {
        return await client.DeleteAsync($"/documents?index={message.Index}&documentId={message.DocumentId}", stoppingToken);
    }

    /// <summary>
    /// Determines the content type based on the file extension.
    /// </summary>
    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
    }

    /// <summary>
    /// Disposes of the HttpWorker resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_timer != null)
                await _timer.DisposeAsync();

            await _stoppingCts.CancelAsync();
            _stoppingCts?.Dispose();

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}