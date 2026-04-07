namespace Iris.Plugins.Configuration;

public sealed class FileReaderOptions
{
    public bool Enabled { get; set; } = true;
    public string ReadPath { get; set; } = string.Empty;
    public string Filter { get; set; } = "*.*";
    public bool DeleteAfterProcessing { get; set; } = true;
    public List<string> Targets { get; set; } = [];
}
