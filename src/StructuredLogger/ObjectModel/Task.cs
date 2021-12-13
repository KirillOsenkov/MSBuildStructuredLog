using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Task : TimedNode, IHasSourceFile, IHasLineNumber
    {
        public string FromAssembly { get; set; }
        public string CommandLineArguments { get; set; }
        public string SourceFilePath { get; set; }

        private string title;
        public string Title
        {
            get
            {
                if (title == null)
                {
                    title = Name;
                }

                return title;
            }

            set
            {
                title = value;
            }
        }

        public override string TypeName => nameof(Task);

        public virtual bool IsDerivedTask => this.GetType() != typeof(Task);

        public int? LineNumber { get; set; }

        public override string ToString() => Title;

        public string GetTargetFrameworkIdentifier()
        {
            try
            {
                var taskDllPath = FromAssembly;
                if (!File.Exists(taskDllPath))
                {
                    // `FromAssembly` might be an assembly name instead of a file path, e.g. "Microsoft.Build.Tasks.Core, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
                    // Assuming that this assembly is an MSBuild assembly which is in the same directory as the MSBuild executable
                    var msbuildDirectory = Path.GetDirectoryName(GetNearestParent<Build>()?.MSBuildExecutablePath);
                    if (msbuildDirectory is not null)
                    {
                        var assemblyName = new AssemblyName(FromAssembly);
                        taskDllPath = Path.Combine(msbuildDirectory, $"{assemblyName.Name}.dll");
                    }
                }

                var resolver = new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"));
                using var loadContext = new MetadataLoadContext(resolver);
                var taskAssembly = loadContext.LoadFromAssemblyPath(taskDllPath);
                var targetFrameworkAttribute = taskAssembly.GetCustomAttributesData().FirstOrDefault(e => e.AttributeType.Name == nameof(TargetFrameworkAttribute));
                if (targetFrameworkAttribute?.ConstructorArguments.FirstOrDefault().Value is string frameworkNameValue)
                {
                    var frameworkName = new FrameworkName(frameworkNameValue);
                    return frameworkName.Identifier;
                }
                throw new InvalidOperationException($"TargetFrameworkAttribute was not found in \"{taskDllPath}\"");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to identify the target framework identifier for the task \"{Name}\" from \"{FromAssembly}\"", ex);
            }
        }
    }
}
