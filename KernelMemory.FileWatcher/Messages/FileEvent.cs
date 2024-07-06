namespace KernelMemory.FileWatcher.Messages;

internal record FileEvent
{
    public FileEventType EventType { get; init; }
    public string FileName { get; init; }
    public string Directory { get; init; }
    public string RelativePath { get; init; }
    public DateTime Time { get; init; }

    public FileEvent(FileEventType eventType, string fileName, string directory, string relativePath)
    {
        EventType = eventType;
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        Time = DateTime.Now;

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName cannot be empty or whitespace.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory cannot be empty or whitespace.", nameof(directory));
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("RelativePath cannot be empty or whitespace.", nameof(relativePath));
    }
}