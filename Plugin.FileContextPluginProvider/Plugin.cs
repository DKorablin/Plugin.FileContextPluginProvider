using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Plugin.FileContextPluginProvider.Context;
using Plugin.FilePluginProvider;
using SAL.Flatbed;

namespace Plugin.FileContextPluginProvider
{
	/// <summary>Plugins loader from file system but it's using separate sandbox to find appropriate assemblies to load</summary>
	public class Plugin : IPluginProvider
	{
		private TraceSource _trace;

		private TraceSource Trace { get => this._trace ??= Plugin.CreateTraceSource<Plugin>(); }

		private IHost Host { get; }

		/// <summary>Arguments passed from primary application</summary>
		private FilePluginArgs Args { get; } = new FilePluginArgs();

		/// <summary>Monitor searching for new plugins in folders</summary>
		private List<FileSystemWatcher> Monitors { get; } = new List<FileSystemWatcher>();

		/// <summary>Parent plugin provider</summary>
		IPluginProvider IPluginProvider.ParentProvider { get; set; }

		public Plugin(IHost host)
			=> this.Host = host ?? throw new ArgumentNullException(nameof(host));

		#region IPlugin
		Boolean IPlugin.OnConnection(ConnectMode mode)
		{
			return true;
		}

		Boolean IPlugin.OnDisconnection(DisconnectMode mode)
		{
			if(mode == DisconnectMode.UserClosed)
				throw new NotSupportedException("Plugin Provider can't be unloaded");
			else
			{
				if(this.Monitors.Count > 0)
				{
					foreach(FileSystemWatcher monitor in this.Monitors)
						monitor.Dispose();
					this.Monitors.Clear();
				}
				return true;
			}
		}
		#endregion IPlugin

		#region IPluginProvider
		void IPluginProvider.LoadPlugins()
		{
			//System.Diagnostics.Debugger.Launch();
			foreach(String pluginPath in this.Args.PluginPath)
				if(Directory.Exists(pluginPath))
				{
					AssemblyTypesInfo[] infos;
					//var loadContext1 = AssemblyLoadContext.GetLoadContext(AppDomain.CurrentDomain.GetAssemblies()[0]);*/
					Int32 asmCount1 = AppDomain.CurrentDomain.GetAssemblies().Length;
					using(AssemblyAnalyzer2 analyzer2 = new AssemblyAnalyzer2(pluginPath))
						infos = analyzer2.CheckAssemblies();
					Int32 asmCount2 = AppDomain.CurrentDomain.GetAssemblies().Length;

					foreach(AssemblyTypesInfo info in infos)
						this.LoadAssembly(info, ConnectMode.Startup);

					foreach(String extension in FilePluginArgs.LibraryExtensions)
					{
						FileSystemWatcher watcher = new FileSystemWatcher(pluginPath, "*"+ extension);
						watcher.Changed += new FileSystemEventHandler(this.Monitor_Changed);
						watcher.EnableRaisingEvents = true;
						this.Monitors.Add(watcher);
					}
				}
		}

		Assembly IPluginProvider.ResolveAssembly(String assemblyName)
		{
			if(String.IsNullOrEmpty(assemblyName))
				throw new ArgumentNullException(nameof(assemblyName), "Assembly name is required to resolve it");

			AssemblyName targetName = new AssemblyName(assemblyName);
			foreach(String pluginPath in this.Args.PluginPath)
				if(Directory.Exists(pluginPath))
					foreach(String file in Directory.GetFiles(pluginPath, "*.*", SearchOption.AllDirectories))
						if(FilePluginArgs.CheckFileExtension(file))//Поиск только файлов с расширением .dll (UPD: Unify file extensions, it should work...)
							try
							{
								AssemblyName name = AssemblyName.GetAssemblyName(file);
								if(name.FullName == targetName.FullName)
									return Assembly.LoadFile(file);
								//return assembly;//TODO: Reference DLL из оперативной памяти не цепляются!
							} catch(BadImageFormatException)
							{
								continue;
							} catch(FileLoadException)
							{
								continue;
							} catch(Exception exc)
							{
								exc.Data.Add("Library", file);
								this.Trace.TraceData(TraceEventType.Error, 1, exc);
							}

			this.Trace.TraceEvent(TraceEventType.Warning, 5, "The provider {0} is unable to locate the assembly {1} in the path {2}", this.GetType(), assemblyName, String.Join(",", this.Args.PluginPath));
			IPluginProvider parentProvider = ((IPluginProvider)this).ParentProvider;
			return parentProvider?.ResolveAssembly(assemblyName);
		}
		#endregion IPluginProvider

		/// <summary>New file for check is available</summary>
		/// <param name="sender">Message sender</param>
		/// <param name="e">Event arguments</param>
		private void Monitor_Changed(Object sender, FileSystemEventArgs e)
		{
			if(e.ChangeType == WatcherChangeTypes.Changed)
			{
				AssemblyTypesInfo info;
				using(AssemblyAnalyzer2 analyzer = new AssemblyAnalyzer2(e.FullPath))
					info = analyzer.CheckAssembly();
				if(info != null)
					this.LoadAssembly(info, ConnectMode.AfterStartup);
			}
		}

		private void LoadAssembly(AssemblyTypesInfo info, ConnectMode mode)
		{
			if(info.Error != null)
			{
				this.Trace.TraceEvent(TraceEventType.Error, 1, "Path: {0} Error: {1}", info.AssemblyPath, info.Error);
				return;
			}
			try
			{
				if(info.Types.Length == 0)
					throw new InvalidOperationException("Types is empty");

				// Проверяем что плагин с таким источником ещё не загружен, если его уже загрузил родительский провайдер.
				// Загрузка из ФС так что источник должен быть по любому уникальный.
				foreach(IPluginDescription plugin in this.Host.Plugins)
					if(info.AssemblyPath.Equals(plugin.Source, StringComparison.InvariantCultureIgnoreCase))
						return;

				Assembly assembly = Assembly.LoadFile(info.AssemblyPath);
				foreach(String type in info.Types)
					this.Host.Plugins.LoadPlugin(assembly, type, info.AssemblyPath, mode);

			} catch(BadImageFormatException exc)//Ошибка загрузки плагина. Можно почитать заголовок загружаемого файла, но мне влом
			{
				exc.Data.Add("Library", info.AssemblyPath);
				this.Trace.TraceData(TraceEventType.Error, 1, exc);
				return;
			} catch(Exception exc)
			{
				exc.Data.Add("Library", info.AssemblyPath);
				this.Trace.TraceData(TraceEventType.Error, 1, exc);
			}
		}

		internal static TraceSource CreateTraceSource<T>(String name = null) where T : IPlugin
		{
			TraceSource result = new TraceSource(typeof(T).Assembly.GetName().Name + name);
			result.Switch.Level = SourceLevels.All;
			result.Listeners.Remove("Default");
			result.Listeners.AddRange(System.Diagnostics.Trace.Listeners);
			return result;
		}
	}
}