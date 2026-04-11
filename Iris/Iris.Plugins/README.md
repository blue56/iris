# Iris.Plugins

This project contains example plugins that demonstrate how to create custom sources and targets for the Iris data relay system **as a separate plugin assembly**.

## Purpose

The `Iris.Plugins` project serves as:

- **Reference Implementation**: Examples of well-structured standalone plugins
- **Template Project**: Starting point for your own plugin assemblies
- **Demonstration**: Shows how plugins work independently from the core
- **Future Phase 2**: Ready for dynamic plugin loading

## Important: Architecture Note

This project is **intentionally separate** from the main Iris application to demonstrate the plugin architecture:

- ? **Iris.Plugins** references **Iris** (to access interfaces)
- ? **Iris** does NOT reference **Iris.Plugins** (no circular dependency)
- ? Plugins are discovered and loaded by the plugin system

In **Phase 1** (current), these example plugins are for reference only. To use them, you would:
1. Copy the plugin code into the main Iris project, OR
2. Wait for Phase 2 dynamic loading feature

In **Phase 2** (future), the `DynamicPluginLoader` will load this assembly at runtime without any code changes to Iris core.

## Included Plugins

### Sources

#### TimerSource
**Type**: Source  
**Author**: Iris Plugins  
**Version**: 1.0.0

Generates test messages on a configurable timer interval. Useful for testing and demonstration.

**Features**:
- Configurable interval
- Automatic message counter
- Timestamp metadata
- Target routing support

**Use Cases**:
- Testing pipeline without external data sources
- Generating heartbeat messages
- Load testing

---

#### HttpPollerSource
**Type**: Source  
**Author**: Iris Plugins  
**Version**: 1.0.0

Polls an HTTP endpoint at regular intervals and forwards the response as messages.

**Features**:
- Configurable polling interval
- Automatic retry on failure
- HTTP metadata (status code, content type)
- Response body forwarding

**Use Cases**:
- Monitoring REST APIs
- Periodic data collection from web services
- Integration with HTTP-based systems

---

### Targets

#### ConsoleTarget
**Type**: Target  
**Author**: Iris Plugins  
**Version**: 1.0.0

Writes messages to the console with optional color formatting.

**Features**:
- Color-coded output
- Metadata display
- Message ID tracking
- Timestamped entries

**Use Cases**:
- Debugging message flow
- Development monitoring
- Quick message inspection

---

#### HttpWebhookTarget
**Type**: Target  
**Author**: Iris Plugins  
**Version**: 1.0.0

Posts messages to an HTTP webhook endpoint as JSON.

**Features**:
- Automatic retry with exponential backoff
- JSON payload formatting
- Configurable webhook URL
- Error handling and logging

**Use Cases**:
- Webhook integrations
- REST API notifications
- External system integration

---

## Using These Plugins (Current - Phase 1)

Since dynamic loading isn't implemented yet, to use these plugins:

### Option 1: Copy Plugin Code

1. Copy the desired plugin file (e.g., `TimerSource.cs`) to the appropriate folder in the main Iris project:
   - Sources ? `Iris/Sources/`
   - Targets ? `Iris/Targets/`

2. Update the namespace:
   ```csharp
   // Change from:
   namespace Iris.Plugins.Sources;

   // To:
   namespace Iris.Sources;
   ```

3. Register in `BuiltInPluginFactory.cs`:
   ```csharp
   ["Timer"] = typeof(TimerSource),
   ```

4. Add initialization logic in `PluginBootstrapService.cs` if needed

5. Configure in `appsettings.yaml`

### Option 2: Wait for Phase 2

In Phase 2, the `DynamicPluginLoader` will automatically discover and load plugins from this assembly without any code changes.

## Using These Plugins

Dynamic loading is implemented. To use the built plugin payload:

1. Build the `Iris.Plugins` project
2. Place the output in its own subfolder under the plugins directory (for example `plugins/Iris.Plugins/`)
3. Keep `Iris.Plugins.dll`, `Iris.Plugins.deps.json`, and the plugin-private dependencies together in that subfolder
4. Configure in `appsettings.yaml`:

```yaml
pluginSystem:
  dynamicLoading:
    enabled: true
    pluginDirectories:
      - "plugins/"

sources:
  timer:
    type: "Iris.Plugins.Sources.TimerSource, Iris.Plugins"
    enabled: true
    configuration:
      interval: "00:00:05"
      targets:
        - "console"
```

## Creating Your Own Plugin Project

You can create your own plugin assembly following this structure:

### Step 1: Create New Class Library

```bash
dotnet new classlib -n MyCompany.IrisPlugins
cd MyCompany.IrisPlugins
dotnet add reference ../Iris/Iris.csproj
```

### Step 2: Create Plugin Class

```csharp
using Iris.Core;
using Iris.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace MyCompany.IrisPlugins;

[Plugin("MyCustomSource", "1.0.0", PluginType.Source,
    Author = "Your Name",
    Description = "Description of what your plugin does")]
public sealed class MyCustomSource : ISource
{
    private readonly ILogger<MyCustomSource> _logger;

    public event Func<DataMessage, Task>? MessageReceived;
    public IReadOnlyList<string> TargetNames { get; }

    public MyCustomSource(ILogger<MyCustomSource> logger)
    {
        _logger = logger;
        TargetNames = new List<string>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MyCustomSource starting...");
        // Your initialization logic
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MyCustomSource stopping...");
        // Your cleanup logic
        return Task.CompletedTask;
    }
}
```

### Step 3: Build and Deploy

**Phase 1 (Current)**: Copy code to main project  
**Phase 2 (Future)**: Build DLL and place in plugins directory

```bash
dotnet build
# DLL will be in bin/Debug/net9.0/MyCompany.IrisPlugins.dll
```

## Dependencies

This project depends on:
- **Iris** (core project): Provides interfaces and infrastructure
- **.NET 9**: Framework
- **Microsoft.Extensions.Logging**: Logging abstractions

## Building

```bash
dotnet build Iris.Plugins
```

## Testing

Create tests in the main `Iris.Tests` project:

```csharp
[Fact]
public async Task TimerSource_GeneratesMessages()
{
    var logger = Substitute.For<ILogger<TimerSource>>();
    var source = new TimerSource(logger, TimeSpan.FromMilliseconds(100));

    var messageReceived = false;
    source.MessageReceived += msg => 
    {
        messageReceived = true;
        return Task.CompletedTask;
    };

    await source.StartAsync(CancellationToken.None);
    await Task.Delay(200);

    Assert.True(messageReceived);
}
```

## Best Practices

1. **Use Dependency Injection**: Constructor inject all dependencies
2. **Add Logging**: Use ILogger for diagnostics
3. **Handle Errors**: Gracefully handle exceptions
4. **Clean Up Resources**: Implement IDisposable if needed
5. **Document Parameters**: XML comments for public APIs
6. **Test Thoroughly**: Unit test your plugins
7. **Follow Conventions**: Match existing plugin patterns

## Contributing

To contribute a plugin:

1. Create your plugin in this project
2. Add tests
3. Update this README
4. Submit a pull request

## Future: Dynamic Loading

In Phase 2, these plugins will be loadable without modifying the factory:

```csharp
// Future: Automatic discovery
pluginLoader.LoadPluginsFromAssembly("Iris.Plugins.dll");

// Future: Configuration-driven loading
plugins:
  sources:
    - assembly: "Iris.Plugins"
      type: "Iris.Plugins.Sources.TimerSource"
      configuration: { ... }
```

## Support

For questions about creating plugins, see:
- [PLUGIN_SYSTEM.md](../PLUGIN_SYSTEM.md) - Complete plugin guide
- [PLUGIN_ARCHITECTURE_DIAGRAM.md](../PLUGIN_ARCHITECTURE_DIAGRAM.md) - Architecture diagrams
- Main Iris [README.md](../README.md) - General documentation
