using System.Runtime.CompilerServices;
using Iris.Core;
using Iris.Core.Plugins;

[assembly: TypeForwardedTo(typeof(DataMessage))]
[assembly: TypeForwardedTo(typeof(ISource))]
[assembly: TypeForwardedTo(typeof(ITarget))]
[assembly: TypeForwardedTo(typeof(IPluginMetadata))]
[assembly: TypeForwardedTo(typeof(PluginAttribute))]
[assembly: TypeForwardedTo(typeof(PluginType))]
