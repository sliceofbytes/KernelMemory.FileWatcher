using KernelMemory.FileWatcher.Messages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace KernelMemory.FileWatcher.Services;

internal class HttpWorker(
    ILogger logger,
    IOptions<KernelMemoryOptions> options,
    IHttpClientFactory httpClientFactory,
    IMessageStore messageStore) : BackgroundService
{
    private readonly ILogger _logger = logger.ForContext<HttpWorker>();
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMessageStore _store = messageStore;
    private readonly KernelMemoryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Starting HttpWorker");

        using var timer = new PeriodicTimer(_options.Schedule);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessMessagesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException oce)
        {
            _logger.Information(oce, "Stopping HttpWorker");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "An error occurred in HttpWorker");
        }
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken stoppingToken)
    {
        if (message.Event?.EventType == FileEventType.Ignore)
        {
            return;
        }

        _logger.Information(
            "Processing message {DocumentId} for file {FileName} of type {EventType}",
            message.DocumentId,
            message.Event?.FileName,
            message.Event?.EventType);

        try
        {
            var client = _httpClientFactory.CreateClient("km-client");
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
                _logger.Error(
                    "Failed to process message {DocumentId}. Status code: {StatusCode}",
                    message.DocumentId,
                    response?.StatusCode);
            }
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

    private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
    {
        var messages = _store.TakeAll();
        if (messages.Count == 0)
        {
            _logger.Information("No messages to process");
            return;
        }

        var tasks = messages.Select(message => ProcessMessageAsync(message, stoppingToken));
        await Task.WhenAll(tasks);
    }

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

        var content = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(fullPath);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(message.Event.FileName));
        content.Add(fileContent, "file", message.Event.FileName);
        content.Add(new StringContent(message.Index), "index");
        content.Add(new StringContent(message.DocumentId), "documentId");

        return await client.PostAsync("/upload", content, stoppingToken);
    }

    private static async Task<HttpResponseMessage> DeleteDocumentAsync(HttpClient client, Message message, CancellationToken stoppingToken)
    {
        return await client.DeleteAsync($"/documents?index={message.Index}&documentId={message.DocumentId}", stoppingToken);
    }

    private static string GetContentType(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
    }
}