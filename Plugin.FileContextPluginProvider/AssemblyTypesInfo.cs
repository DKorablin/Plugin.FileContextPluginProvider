using System;
using System.Diagnostics;

namespace Plugin.FileContextPluginProvider
{
	[Serializable]
	[DebuggerDisplay("AssemblyPath = {" + nameof(AssemblyPath) + "}, Types = {" + nameof(Types) + "?.Length}, Error = {" + nameof(Error) + "}")]
	internal class AssemblyTypesInfo
	{
		/// <summary>The path to analyzed assembly.</summary>
		public String AssemblyPath { get; }

		/// <summary>The path to referenced assembly that was loaded before current assembly.</summary>
		public String ReferencedAssemblyPath { get; set; }

		/// <summary>Error message if assembly is failed to load.</summary>
		public String Error { get; }

		/// <summary>The list of found plugin types that are supported by plugin system.</summary>
		public String[] Types { get; }

		private AssemblyTypesInfo(String assemblyPath)
			=> this.AssemblyPath = assemblyPath;

		/// <summary>Create instance of assembly information with resolved types supported by plugin system.</summary>
		/// <param name="assemblyPath">The path to found assembly.</param>
		/// <param name="types">The list of found types.</param>
		public AssemblyTypesInfo(String assemblyPath, String[] types)
			: this(assemblyPath)
			=> this.Types = types;

		/// <summary>Create instance of assembly information with assembly path and error message with the reason why assembly can't be loaded.</summary>
		/// <param name="assemblyPath">The path to found assembly.</param>
		/// <param name="error">The error message with the reason why assembly can't be loaded.</param>
		public AssemblyTypesInfo(String assemblyPath, String error)
			: this(assemblyPath)//We can't use Trace from different app domain directly
			=> this.Error = error;

		/// <summary>Create instance of assembly information with assembly path, error message and previously found assembly that was analyzed before.</summary>
		/// <param name="assemblyPath">The path to found assembly.</param>
		/// <param name="error">The error message with description that assembly was loaded from the different location.</param>
		/// <param name="referencedAssemblyPath">The path to assembly that was loaded before.</param>
		public AssemblyTypesInfo(String assemblyPath, String error, String referencedAssemblyPath)
			: this(assemblyPath, error)
			=> this.ReferencedAssemblyPath = referencedAssemblyPath;
	}
}