using System.ComponentModel.DataAnnotations;

namespace KernelMemory.FileWatcher.Configuration;

internal class KernelMemoryOptions
{
    [Required]
    [Url]
    public string Endpoint { get; set; } = "http://localhost:9001";

    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Schedule { get; set; } = TimeSpan.FromMinutes(1);

    [Range(0, 10)]
    public int Retries { get; set; } = 2;

    [Range(1, 100)]
    public int ParallelUploads { get; set; } = 4;
}
