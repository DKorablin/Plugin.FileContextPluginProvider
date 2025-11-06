# Plugin.FileContextPluginProvider

[![Auto build](https://github.com/DKorablin/Plugin.FileContextPluginProvider/actions/workflows/release.yml/badge.svg)](https://github.com/DKorablin/Plugin.FileContextPluginProvider/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A robust plugin loader for the SAL (Software Abstraction Layer) framework that provides secure plugin discovery and loading using isolated AssemblyLoadContext instances.

## 🚀 Features

- **Isolated Assembly Loading**: Each plugin is analyzed in a separate `AssemblyLoadContext` to prevent conflicts and ensure sandbox security
- **Parallel Processing**: Uses TPL (Task Parallel Library) for efficient concurrent plugin scanning
- **Thread-Safe**: Each analysis task runs in its own isolated context, eliminating race conditions
- **File System Monitoring**: Automatic detection and loading of new plugins added after startup
- **Error Handling**: Comprehensive exception handling with detailed error reporting
- **Memory Efficient**: Uses `Directory.EnumerateFiles` for deferred execution when scanning directories
- **Flexible Configuration**: Supports multiple plugin paths via configuration or command-line arguments

## 📦 Installation

To install the File Context Plugin Provider Plugin, follow these steps:
1. Download the latest release from the [Releases](https://github.com/DKorablin/Plugin.FileContextPluginProvider/releases)
2. Extract the downloaded ZIP file to a desired location.
3. Use the provided [Flatbed.Dialog (Lite)](https://dkorablin.github.io/Flatbed-Dialog-Lite) executable or download one of the supported host applications:
	- [Flatbed.Dialog](https://dkorablin.github.io/Flatbed-Dialog)
	- [Flatbed.MDI](https://dkorablin.github.io/Flatbed-MDI)
	- [Flatbed.MDI (WPF)](https://dkorablin.github.io/Flatbed-MDI-Avalon)
	- [Flatbed.WorkerService](https://dkorablin.github.io/Flatbed-WorkerService)

## 🔧 Requirements

- **.NET 8.0** or higher
- **SAL.Flatbed** host application

## 📖 Usage

### Basic Configuration

Configure plugin paths via application configuration:

```xml
<configuration>
  <appSettings>
	<add key="SAL_Path" value="C:\Plugins|C:\MorePlugins" />
  </appSettings>
</configuration>
```

### Command-Line Arguments

Alternatively, specify plugin paths via command-line:

```bash
YourApp.exe /SAL_Path:C:\Plugins|C:\AdditionalPlugins
```

### Code Example

```csharp
using Plugin.FileContextPluginProvider;
using SAL.Flatbed;

// Create the plugin provider with your host
IHost host = /* your SAL host instance */;
var pluginProvider = new Plugin(host);

// Connect and load plugins
((IPlugin)pluginProvider).OnConnection(ConnectMode.Startup);
((IPluginProvider)pluginProvider).LoadPlugins();
```

## 🏗️ Architecture

### Core Components

#### AssemblyAnalyzerCore
Base class that extends `AssemblyLoadContext` to provide:
- Custom assembly resolution from plugin directories
- Proper resource disposal
- Isolated loading context per analysis session

#### AssemblyAnalyzer2
Implements plugin discovery with:
- **Parallel scanning**: Uses TPL to scan multiple assemblies concurrently
- **Isolated contexts**: Creates separate `AssemblyLoadContext` per file to avoid conflicts
- **File monitoring**: Watches for new `.dll` files in plugin directories

#### AssemblyTypesReader2
Static utility class for:
- Loading assemblies and extracting plugin types
- Handling various exception scenarios (BadImageFormat, FileLoad, ReflectionTypeLoad)
- Identifying types that implement the SAL plugin interface

### Threading Model

The provider uses modern TPL patterns:

```csharp
// Each assembly file gets its own task and isolated context
Task<AssemblyTypesInfo>[] tasks = assemblyFiles
	.Select(filePath => Task.Run(() => CheckAssemblyInIsolation(filePath)))
	.ToArray();

// Each task creates its own AssemblyLoadContext
private AssemblyTypesInfo CheckAssemblyInIsolation(String filePath)
{
	using(AssemblyAnalyzerCore isolatedContext = new AssemblyAnalyzerCore(Directory.FullName))
	{
		return isolatedContext.GetPluginTypes(filePath);
	}
}
```

### Benefits of Isolated Contexts

1. **Security**: Plugins are scanned in isolated contexts before actual loading
2. **Stability**: Prevents type conflicts between different plugin versions
3. **Clean Unloading**: Contexts can be unloaded after analysis, freeing memory
4. **Thread Safety**: No shared state between concurrent analysis operations

## 🔍 How It Works

1. **Discovery Phase**:
	- Scans configured directories for `.dll` files
	- Creates isolated `AssemblyLoadContext` for each file
	- Loads assembly and inspects types in parallel

2. **Validation Phase**:
	- Identifies types implementing SAL plugin interfaces
	- Captures metadata (assembly path, plugin types)
	- Handles errors gracefully (bad images, loader exceptions)

3. **Loading Phase**:
	- Loads validated assemblies into main application domain
	- Calls `OnConnection` for each discovered plugin
	- Checks for duplicates to prevent re-loading

4. **Monitoring Phase**:
	- Sets up `FileSystemWatcher` for each plugin directory
	- Detects new `.dll` files added after startup
	- Automatically analyzes and loads new plugins

## 🛡️ Error Handling

The provider handles various error scenarios:

- **BadImageFormatException**: Invalid or native assemblies are skipped
- **FileLoadException**: Reports security or dependency issues
- **ReflectionTypeLoadException**: Captures and reports loader errors
- **General Exceptions**: Logged with context for troubleshooting

## 🔄 Plugin Lifecycle

```
Startup → Discovery → Analysis → Validation → Loading → Monitoring → Shutdown
              ↓                                             ↓
        (Isolated Context)                         (FileSystemWatcher)
              ↓                                             ↓
          Unloaded                                New Plugin Detection
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 🔗 Related Projects

- [SAL.Flatbed](https://github.com/DKorablin/SAL.Flatbed) - Software Abstraction Layer framework

## 📝 Version History

See [Releases](https://github.com/DKorablin/Plugin.FileContextPluginProvider/releases) for version history and changes.

---

**Note**: This provider is part of the SAL (Software Abstraction Layer) plugin ecosystem and requires the SAL.Flatbed application to function.