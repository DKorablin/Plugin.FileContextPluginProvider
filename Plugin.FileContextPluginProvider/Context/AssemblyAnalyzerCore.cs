using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Plugin.FilePluginProvider;

namespace Plugin.FileContextPluginProvider.Context
{
	internal class AssemblyAnalyzerCore : AssemblyLoadContext, IDisposable
	{
		protected DirectoryInfo Directory { get; }

		public AssemblyAnalyzerCore(String assemblyPath)
			: base(true)
		{
			Directory = new DirectoryInfo(assemblyPath);
			Resolving += OnResolve;
		}

		private Assembly OnResolve(AssemblyLoadContext arg1, AssemblyName arg2)
		{
			foreach(String pluginExtension in FilePluginArgs.LibraryExtensions)
			{
				String assemblyFilePath = Path.Combine(Directory.FullName, arg2.Name + pluginExtension);
				if(File.Exists(assemblyFilePath))
					return arg1.LoadFromStream(new FileStream(assemblyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
			}

			return null;
		}

		protected override Assembly Load(AssemblyName assemblyName)
		{
			Assembly loadedAssembly = Array.Find(AppDomain.CurrentDomain.GetAssemblies(), delegate (Assembly asm) { return String.Equals(asm.FullName, assemblyName.FullName, StringComparison.Ordinal); });

			if(loadedAssembly != null)
				return loadedAssembly;

			foreach(String pluginExtension in FilePluginArgs.LibraryExtensions)
			{
				String assemblyFilePath = Path.Combine(Directory.FullName, assemblyName.Name + pluginExtension);

				if(File.Exists(assemblyFilePath))
					return LoadFromStream(new FileStream(assemblyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
			}
			return null;
		}

		public void Dispose()
		{
			Resolving -= OnResolve;
			Unload();
			GC.Collect(2);
			GC.WaitForPendingFinalizers();
			GC.Collect(2);
		}
	}
}