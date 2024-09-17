using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public readonly struct CompilationWrites
    {
        public string Assembly { get; }
        public string RefAssembly { get; }
        public string Pdb { get; }
        public string XmlDocumentation { get; }
        public string SourceLink { get; }

        public string AssemblyOrRefAssembly => Assembly ?? RefAssembly;

        public CompilationWrites(
            string assembly,
            string refAssembly,
            string pdb,
            string xmlDocumentation,
            string sourceLink)
        {
            Assembly = assembly;
            RefAssembly = refAssembly;
            Pdb = pdb;
            XmlDocumentation = xmlDocumentation;
            SourceLink = sourceLink;
        }

        internal static CompilationWrites? TryParse(Task task)
        {
            var parameters = task.FindChild<Folder>(static c => c.Name == Strings.Parameters);
            if (parameters == null)
            {
                // Probably localized MSBuild that we don't yet support
                return null;
            }

            string assembly = null;
            string refAssembly = null;
            string xmlDocumentation = null;
            string sourceLink = null;
            var hasPdb = false;

            foreach (var property in parameters.Children.OfType<Property>())
            {
                switch (property.Name)
                {
                    case "OutputAssembly":
                        assembly = property.Value;
                        break;
                    case "OutputRefAssembly":
                        refAssembly = property.Value;
                        break;
                    case "DocumentationFile":
                        xmlDocumentation = property.Value;
                        break;
                    case "SourceLink":
                        sourceLink = property.Value;
                        break;
                    case "DebugType":
                        switch (property.Value.ToLower())
                        {
                            case "full":
                            case "portable":
                            case "pdbonly":
                                hasPdb = true;
                                break;
                        }
                        break;
                }
            }

            if (string.IsNullOrEmpty(assembly) && string.IsNullOrEmpty(refAssembly))
            {
                return null;
            }

            var pdb = hasPdb && !string.IsNullOrEmpty(assembly)
                ? Path.ChangeExtension(assembly, ".pdb")
                : null;
            return new CompilationWrites(
                assembly,
                refAssembly,
                pdb,
                xmlDocumentation,
                sourceLink);
        }

        public override string ToString() => $"{Path.GetFileName(AssemblyOrRefAssembly)}";
    }
}
