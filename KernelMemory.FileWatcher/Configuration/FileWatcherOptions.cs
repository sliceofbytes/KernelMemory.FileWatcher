using System.ComponentModel.DataAnnotations;

namespace KernelMemory.FileWatcher.Configuration;

internal class FileWatcherOptions
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one directory must be specified.")]
    public List<FileWatcherDirectoryOptions> Directories { get; set; } = [];

    public bool WaitForInitialScans { get; set; } = false;
}