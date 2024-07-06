using Microsoft.Extensions.Options;

namespace KernelMemory.FileWatcher.Configuration;

internal class KernelMemoryOptionsValidator : IValidateOptions<KernelMemoryOptions>
{
    public ValidateOptionsResult Validate(string? name, KernelMemoryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail("KernelMemory Endpoint cannot be null or empty.");
        }

        if (options.Schedule <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("Schedule must be a positive TimeSpan.");
        }

        if (options.Retries < 0)
        {
            return ValidateOptionsResult.Fail("Retries cannot be negative.");
        }

        if (options.ParallelUploads <= 0)
        {
            return ValidateOptionsResult.Fail("ParallelUploads must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}