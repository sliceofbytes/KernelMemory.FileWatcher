namespace KernelMemory.FileWatcher.Messages;

internal class Message
{
    public FileEvent Event { get; }
    public string Index { get; }
    public string DocumentId { get; }

    public Message(FileEvent @event, string index, string documentId)
    {
        Event = @event ?? throw new ArgumentNullException(nameof(@event));
        Index = index ?? throw new ArgumentNullException(nameof(index));
        DocumentId = documentId ?? throw new ArgumentNullException(nameof(documentId));

        if (string.IsNullOrWhiteSpace(index))
            throw new ArgumentException("Index cannot be empty or whitespace.", nameof(index));
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("DocumentId cannot be empty or whitespace.", nameof(documentId));
    }

    public string FullPath => Path.Combine(Event.Directory, Event.FileName);
}