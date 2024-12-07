using System;

namespace Plugin.FileContextPluginProvider
{
	[Serializable]
	internal class AssemblyTypesInfo
	{
		public String AssemblyPath { get; }

		public String Error { get; }

		public String[] Types { get; }

		private AssemblyTypesInfo(String assemblyPath)
			=> this.AssemblyPath = assemblyPath;

		public AssemblyTypesInfo(String assemblyPath, String[] types)
			: this(assemblyPath)
			=> this.Types = types;

		public AssemblyTypesInfo(String assemblyPath, String error)
			: this(assemblyPath)//We can't use Trace from different app domain directly
			=> this.Error = error;
	}
}