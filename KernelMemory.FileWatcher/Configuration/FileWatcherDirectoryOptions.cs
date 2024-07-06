using KernelMemory.FileWatcher.Attributes;
using System.ComponentModel.DataAnnotations;

namespace KernelMemory.FileWatcher.Configuration;

internal class FileWatcherDirectoryOptions
{
    [Required]
    [DirectoryExists]
    public string Path { get; set; } = string.Empty;

    [Required]
    public string Filter { get; set; } = "*.*";

    public bool IncludeSubdirectories { get; set; } = true;

    [Required]
    public string Index { get; set; } = "default";

    public bool InitialScan { get; set; } = true;
}
