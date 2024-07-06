using System.ComponentModel.DataAnnotations;

namespace KernelMemory.FileWatcher.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class DirectoryExistsAttribute : ValidationAttribute
{
    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string path && !Directory.Exists(path))
        {
            return new ValidationResult($"Directory does not exist: {path}");
        }

        return ValidationResult.Success!;
    }
}