using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Plugin.FilePluginProvider;
using SAL.Flatbed;

namespace Plugin.FileContextPluginProvider.Context;

internal class IsolatedLoadContext : AssemblyLoadContext, IDisposable
{
	private readonly Object _lock = new Object();
	protected DirectoryInfo Directory { get; }

	public IsolatedLoadContext(String assemblyPath)
		: base($"Plugin.FileContextPluginProvider.ProbingContext.{Guid.NewGuid():N}", true)
	{
		this.Directory = new DirectoryInfo(assemblyPath);
		this.Resolving += this.OnResolve;
	}

	public AssemblyTypesInfo[] CheckAssemblies()
	{
		IEnumerable<String> assemblyFiles = System.IO.Directory.EnumerateFiles(this.Directory.FullName, "*.*", SearchOption.AllDirectories)
			.Where(FilePluginArgs.CheckFileExtension);

		ConcurrentBag<AssemblyTypesInfo> results = new ConcurrentBag<AssemblyTypesInfo>();

		ParallelOptions options = new ParallelOptions
		{
			MaxDegreeOfParallelism = Environment.ProcessorCount // Limit to CPU cores to avoid context switching overhead
		};

		Parallel.ForEach(assemblyFiles, options, filePath =>
		{
			AssemblyTypesInfo info = this.GetPluginTypes(filePath);
			if(info != null)
				results.Add(info);
		});

		return results.ToArray();
	}

	public AssemblyTypesInfo CheckAssembly(String assemblyFilePath)
	{
		AssemblyTypesInfo result = null;
		if(FilePluginArgs.CheckFileExtension(assemblyFilePath))
			result = this.GetPluginTypes(assemblyFilePath);

		return result;
	}

	protected AssemblyTypesInfo GetPluginTypes(String filePath)
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
			default:
				// Handle scenario when assembly with same identity is already loaded into this context.
				// We search for existing loaded assembly with matching full name and provide its location as referencedAssemblyPath.
				AssemblyName targetName = AssemblyName.GetAssemblyName(filePath);
				lock(_lock)
				{
					Assembly existing = this.Assemblies.FirstOrDefault(a => a.GetName().FullName == targetName.FullName);
					return existing != null && !String.Equals(existing.Location, filePath, StringComparison.OrdinalIgnoreCase)
						? new AssemblyTypesInfo(filePath, $"Assembly \"{existing.FullName}\" will be skipped because it’s already loaded from \"{existing.Location}\".", existing.Location)
						: new AssemblyTypesInfo(filePath, (exc.InnerException ?? exc).Message, existing?.Location);
				}
			}
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

		return types.Count > 0
			? new AssemblyTypesInfo(filePath, types.ToArray())
			: null;
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