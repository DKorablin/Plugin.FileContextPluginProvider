using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using Plugin.FilePluginProvider;

namespace Plugin.FileContextPluginProvider.Context
{
	internal class AssemblyAnalyzer2 : AssemblyAnalyzerCore
	{
		public AssemblyAnalyzer2(String assemblyPath)
			: base(assemblyPath) { }

		public AssemblyTypesInfo[] CheckAssemblies()
		{
			//System.Diagnostics.Debugger.Launch();
			List<AssemblyTypesInfo> assemblies = new List<AssemblyTypesInfo>();

			List<ManualResetEvent> onDone = new List<ManualResetEvent>();
			List<AssemblyTypesReader2> readers = new List<AssemblyTypesReader2>();
			foreach(String filePath in System.IO.Directory.GetFiles(Directory.FullName, "*.*", SearchOption.AllDirectories))
				if(new FilePluginArgs().CheckFileExtension(filePath))
				{
					ManualResetEvent evt = new ManualResetEvent(false);
					AssemblyTypesReader2 reader = new AssemblyTypesReader2(new String[] { filePath }, evt);
					onDone.Add(evt);
					readers.Add(reader);

					ThreadPool.QueueUserWorkItem<AssemblyLoadContext>(reader.Read, this, true);
				}

			foreach(ManualResetEvent evt in onDone)
				evt.WaitOne();

			foreach(AssemblyTypesReader2 reader in readers)
				foreach(AssemblyTypesInfo info in reader.Info)
				{
					//reader.OnDone.WaitOne();
					if(info != null)
						assemblies.Add(info);
				}

			return assemblies.ToArray();
		}

		public AssemblyTypesInfo CheckAssembly()
		{
			AssemblyTypesInfo result = null;
			if(new FilePluginArgs().CheckFileExtension(Directory.FullName))
				result = AssemblyTypesReader2.GetAssemblyTypes(this, Directory.FullName);

			return result;
		}
	}
}