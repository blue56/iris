using Iris.Core;
using Iris.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace Iris.Plugins.Targets;

/// <summary>
/// Example plugin that writes messages to the console with formatting.
/// Useful for debugging and monitoring.
/// </summary>
[Plugin("ConsoleTarget", "1.0.0", PluginType.Target,
    Author = "Iris Plugins",
    Description = "Writes messages to the console with optional color formatting")]
public sealed class ConsoleTarget : ITarget
{
    private readonly ILogger<ConsoleTarget> _logger;
    private readonly bool _useColors;
    private readonly string _name;

    public string Name => _name;

    /// <summary>
    /// Creates a console target.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="name">Target name (default: "console")</param>
    /// <param name="useColors">Whether to use colored output (default: true)</param>
    public ConsoleTarget(
        ILogger<ConsoleTarget> logger,
        string? name = null,
        bool useColors = true)
    {
        _logger = logger;
        _name = name ?? "console";
        _useColors = useColors;
    }

    public Task SendAsync(DataMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var separator = new string('=', 60);

            if (_useColors)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n{separator}");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{timestamp}] Message ID: {message.Id}");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"\n{separator}");
                Console.WriteLine($"[{timestamp}] Message ID: {message.Id}");
            }

            // Print metadata if any
            if (message.Metadata.Count > 0)
            {
                if (_useColors) Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Metadata:");
                foreach (var kvp in message.Metadata)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
                if (_useColors) Console.ResetColor();
            }

            // Print body
            if (_useColors) Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Body:");
            Console.WriteLine(message.Body);
            if (_useColors)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(separator);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(separator);
            }

            _logger.LogDebug("Wrote message {Id} to console", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing message {Id} to console", message.Id);
        }

        return Task.CompletedTask;
    }
}
