// ---------------------------------------------------------------------------
// Load YAML configuration
// ---------------------------------------------------------------------------
var yamlPath = Path.Combine(AppContext.BaseDirectory, "appsettings.yaml");

if (!File.Exists(yamlPath))
    throw new FileNotFoundException(
        $"Required configuration file not found. Ensure 'appsettings.yaml' is set to copy to the output directory.",
        yamlPath);

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Loading configuration from: {yamlPath}");

var yamlContent = await File.ReadAllTextAsync(yamlPath);

