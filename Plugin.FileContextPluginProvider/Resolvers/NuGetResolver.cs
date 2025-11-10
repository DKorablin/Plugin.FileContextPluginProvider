using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace Plugin.FileContextPluginProvider.Resolvers;

internal class NuGetResolver
{
	private static String _nugetPackagesPath;
	private readonly String _assemblyPath;
	private readonly String _dependencyPath;
	private DependencyContext _dependencyContext;

	private static String NuGetPackagesPath
	{
		get
		{
			if(_nugetPackagesPath == null)
			{
				_nugetPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
				if(String.IsNullOrEmpty(_nugetPackagesPath))
				{
					String userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
					_nugetPackagesPath = System.IO.Path.Combine(userProfile, ".nuget", "packages");
				}
			}
			return _nugetPackagesPath;
		}
	}

	public Boolean IsDependencyFileExists => System.IO.File.Exists(this._dependencyPath);

	public DependencyContext Context
	{
		get
		{
			if(this._dependencyContext == null && this.IsDependencyFileExists)
			{
				using(System.IO.FileStream fs = new System.IO.FileStream(this._dependencyPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
					this._dependencyContext = new DependencyContextJsonReader().Read(fs);
			}
			return this._dependencyContext;
		}
	}

	public NuGetResolver(String assemblyPath)
	{
		this._assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
		this._dependencyPath = System.IO.Path.ChangeExtension(assemblyPath, ".deps.json");
	}

	public String ResolveAssemblyToPath(AssemblyName assemblyName)
	{
		DependencyContext ctx = this.Context;
		if(ctx == null)
			return null;

		var runtimeLib = ctx.RuntimeLibraries.FirstOrDefault(l => String.Equals(l.Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
		if(runtimeLib != null)
		{
			var assembly = runtimeLib.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths).FirstOrDefault();
			if(assembly != null)
			{
				String assemblyFullPath = System.IO.Path.Combine(NuGetPackagesPath, runtimeLib.Path, assembly);
				if(System.IO.File.Exists(assemblyFullPath))
					return assemblyFullPath;
			}
		}
		return null;
	}

	public IEnumerable<String> GetNuGetAssemblyPaths()
	{
		DependencyContext ctx = this.Context;
		if(ctx == null)
			yield break;

		var runtimeLib = ctx.RuntimeLibraries.FirstOrDefault(l => String.Equals(l.Name, System.IO.Path.GetFileNameWithoutExtension(this._assemblyPath), StringComparison.OrdinalIgnoreCase));
		if(runtimeLib != null)
		{
			foreach(var assembly in runtimeLib.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths))
			{
				String assemblyFullPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(this._assemblyPath), runtimeLib.Path, assembly);
				if(System.IO.File.Exists(assemblyFullPath))
					yield return assemblyFullPath;
			}
		}
	}
}