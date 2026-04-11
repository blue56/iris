namespace Iris.Plugins.Configuration;

public sealed class FilesystemWatcherOptions
{
    public string Name { get; set; } = "filesystemWatcher";
    public bool Enabled { get; set; } = true;
    public string ReadPath { get; set; } = string.Empty;
    public string Filter { get; set; } = "*.*";
    public bool DeleteAfterProcessing { get; set; } = true;
}
