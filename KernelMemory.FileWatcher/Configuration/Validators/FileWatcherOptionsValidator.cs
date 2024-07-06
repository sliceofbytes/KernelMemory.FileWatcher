using Microsoft.Extensions.Options;

namespace KernelMemory.FileWatcher.Configuration;

internal class FileWatcherOptionsValidator : IValidateOptions<FileWatcherOptions>
{
    public ValidateOptionsResult Validate(string? name, FileWatcherOptions options)
    {
        if (options.Directories == null || options.Directories.Count == 0)
        {
            return ValidateOptionsResult.Fail("At least one directory must be specified.");
        }

        var errors = new List<string>();

        foreach (var dir in options.Directories)
        {
            if (string.IsNullOrWhiteSpace(dir.Path))
            {
                errors.Add("Directory path cannot be null or empty.");
            }
            else if (!Directory.Exists(dir.Path))
            {
                errors.Add($"Directory not found: {dir.Path}");
            }

            if (string.IsNullOrWhiteSpace(dir.Filter))
            {
                errors.Add("Directory filter cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(dir.Index))
            {
                errors.Add("Directory index cannot be null or empty.");
            }
        }

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}
