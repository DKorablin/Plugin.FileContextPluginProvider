using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Plugin.FilePluginProvider;
using SAL.Flatbed;

namespace Plugin.FileContextPluginProvider.Context
{
	internal class AssemblyAnalyzerCore : AssemblyLoadContext, IDisposable
	{
		protected DirectoryInfo Directory { get; }

		public AssemblyAnalyzerCore(String assemblyPath)
			: base("Plugin.FileContextPluginProvider.ProbingContext", true)
		{
			this.Directory = new DirectoryInfo(assemblyPath);
			this.Resolving += this.OnResolve;
		}

		public AssemblyTypesInfo GetPluginTypes(String filePath)
		{
			List<String> types = new List<String>();
			try
			{
				Assembly assembly = this.LoadFromAssemblyPath(filePath);

				foreach(Type assemblyType in assembly.GetTypes())
					if(PluginUtils.IsPluginType(assemblyType))
						types.Add(assemblyType.FullName);
			} catch(BadImageFormatException)
			{
				return null;
			} catch(FileLoadException exc)
			{
				Int32 hResult = Marshal.GetHRForException(exc);
				switch((UInt32)hResult)
				{
				case 0x80131515://loadFromRemoteSources
					Exception exc1 = exc.InnerException == null ? exc : exc.InnerException;
					return new AssemblyTypesInfo(filePath, exc1.Message);
				}
				return null;
			} catch(ReflectionTypeLoadException exc)
			{
				String errors = exc.LoaderExceptions != null && exc.LoaderExceptions.Length > 0
					? String.Join(Environment.NewLine, Array.ConvertAll(exc.LoaderExceptions, e => e.Message))
					: exc.Message;
				return new AssemblyTypesInfo(filePath, errors);
			} catch(Exception exc)
			{
				Exception exc1 = exc.InnerException ?? exc;
				return new AssemblyTypesInfo(filePath, exc1.Message);
			}

			if(types.Count > 0)
				return new AssemblyTypesInfo(filePath, types.ToArray());
			return null;
		}

		private Assembly OnResolve(AssemblyLoadContext arg1, AssemblyName arg2)
		{
			foreach(String pluginExtension in FilePluginArgs.LibraryExtensions)
			{
				String assemblyFilePath = Path.Combine(this.Directory.FullName, arg2.Name + pluginExtension);
				if(File.Exists(assemblyFilePath))
				{
					using(FileStream stream = new FileStream(assemblyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
						return arg1.LoadFromStream(stream);
				}
			}

			return null;
		}

		protected override Assembly Load(AssemblyName assemblyName)
		{
			Assembly loadedAssembly = Array.Find(AppDomain.CurrentDomain.GetAssemblies(), asm => String.Equals(asm.FullName, assemblyName.FullName, StringComparison.Ordinal));

			if(loadedAssembly != null)
				return loadedAssembly;

			foreach(String pluginExtension in FilePluginArgs.LibraryExtensions)
			{
				String assemblyFilePath = Path.Combine(this.Directory.FullName, assemblyName.Name + pluginExtension);

				if(File.Exists(assemblyFilePath))
				{
					using(FileStream stream = new FileStream(assemblyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
						return this.LoadFromStream(stream);
				}
			}
			return null;
		}

		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(Boolean disposing)
		{
			if(disposing)
			{
				this.Resolving -= this.OnResolve;
				this.Unload();
			}
		}
	}
}