using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Plugin.FilePluginProvider;

namespace Plugin.FileContextPluginProvider.Context
{
	internal class AssemblyAnalyzer2 : AssemblyAnalyzerCore
	{
		public AssemblyAnalyzer2(String assemblyPath)
			: base(assemblyPath) { }

		public AssemblyTypesInfo[] CheckAssemblies()
		{
			IEnumerable<String> assemblyFiles = System.IO.Directory.EnumerateFiles(this.Directory.FullName, "*.*", SearchOption.AllDirectories)
				.Where(FilePluginArgs.CheckFileExtension);

			// Create a task for each assembly file to check in parallel
			Task<AssemblyTypesInfo>[] tasks = assemblyFiles
				.Select(filePath => Task.Run(() => this.CheckAssemblyInIsolation(filePath)))
				.ToArray();

			// Wait for all tasks to complete
			Task.WaitAll(tasks);

			// Collect non-null results
			return tasks
				.Select(task => task.Result)
				.Where(info => info != null)
				.ToArray();
		}

		public AssemblyTypesInfo CheckAssembly(String assemblyFilePath)
		{
			AssemblyTypesInfo result = null;
			if(FilePluginArgs.CheckFileExtension(assemblyFilePath))
				result = this.GetPluginTypes(assemblyFilePath);

			return result;
		}

		private AssemblyTypesInfo CheckAssemblyInIsolation(String filePath)
		{
			// Create a separate AssemblyLoadContext for each file to avoid race conditions
			// This ensures thread safety as each task has its own isolated context
			using(AssemblyAnalyzerCore isolatedContext = new AssemblyAnalyzerCore(Directory.FullName))
			{
				return isolatedContext.GetPluginTypes(filePath);
			}
		}
	}
}