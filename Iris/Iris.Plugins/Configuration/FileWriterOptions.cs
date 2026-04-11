namespace Iris.Plugins.Configuration;

public sealed class FileWriterOptions
{
    public string Name { get; set; } = "fileWriter";
    public string OutputPath { get; set; } = string.Empty;
    public string FileExtension { get; set; } = ".txt";
}
