namespace KernelMemory.FileWatcher.Configuration;

internal static class DefaultOptions
{
    public static string ConfigFileName => "appsettings.json";
    public static string ConfigFolder => "/config";
    public static string LogFolder => "logs";
    public static string LogFileName => "km-file-watcher.log";
    public static string HttpClientName => "km-client";
    public static string AuthHeaderName => "Authorization";
    public static string KernelMemoryEndpoint => "http://localhost:9001/";
    public static int KernelMemoryRetries => 2;
}
