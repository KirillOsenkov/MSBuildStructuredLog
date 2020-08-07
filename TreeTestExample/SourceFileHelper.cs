using StructuredLogViewerWASM.Pages;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;
using System.ComponentModel;
using Microsoft.Language.Xml;
using System.Linq;

namespace StructuredLogViewerWASM
{
    public class SourceFileHelper
    {
        /// <summary>
        /// Determines the SourceFile (text, name, line number) from the tree node
        /// </summary>
        /// <param name="fileResolver"> Either the Source or Archive File Resolver to read file from </param>
        /// <param name="bn">BaseNode to be reading file from</param>
        public static object[] SourceFileText(ISourceFileResolver fileResolver, BaseNode bn)
        {
            string path = "";
            string sourceFileText = null;
            string sourceFileName = "";
            int sourceFileLineNumber = -1;

            if (bn is AbstractDiagnostic)
            {
                AbstractDiagnostic ad = (AbstractDiagnostic)bn;
                path = ad.ProjectFile;
                if (ad.IsTextShortened)
                {
                    sourceFileText = ad.Text;
                    sourceFileName = ad.ShortenedText;
                }
                else
                {
                    sourceFileText = fileResolver.GetSourceFileText(path).Text;
                    sourceFileName = ad.Name;
                }
                sourceFileLineNumber = ad.LineNumber;

            }
            else if (bn is Project)
            {
                path = ((Project)bn).SourceFilePath;
                sourceFileName = ((Project)bn).Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is Target)
            {
                path = ((Target)bn).SourceFilePath;
                sourceFileName = ((Target)bn).Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
                sourceFileLineNumber = TargetLineNumber(fileResolver.GetSourceFileText(path), sourceFileName);
            }
            else if (bn is Microsoft.Build.Logging.StructuredLogger.Task)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).SourceFilePath;
                sourceFileName = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
                sourceFileLineNumber = TaskLineNumber(fileResolver.GetSourceFileText(path), ((Microsoft.Build.Logging.StructuredLogger.Task)bn).Parent, sourceFileName);
            }
            else if (bn is IHasSourceFile && ((IHasSourceFile)bn).SourceFilePath != null)
            {
                path = ((IHasSourceFile)bn).SourceFilePath;
                sourceFileName = ((IHasSourceFile)bn).SourceFilePath;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is SourceFileLine && ((SourceFileLine)bn).Parent is Microsoft.Build.Logging.StructuredLogger.SourceFile
            && ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).SourceFilePath != null)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).SourceFilePath;
                sourceFileName = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
                sourceFileLineNumber = ((SourceFileLine)bn).LineNumber;
            }
            else if (bn is NameValueNode && ((NameValueNode)bn).IsValueShortened)
            {
                sourceFileText = ((NameValueNode)bn).Value;
                sourceFileName = ((NameValueNode)bn).Name;
            }
            else if (bn is TextNode && ((TextNode)bn).IsTextShortened)
            {
                sourceFileText = ((TextNode)bn).Text;
                sourceFileName = ((TextNode)bn).Name;
            }

            if (sourceFileText == null)
            {
                sourceFileText = "No file to display";
            }

            string[] fileParts = path.Split(".");
            string fileExtension = fileParts[fileParts.Length - 1];
            if (fileExtension.Equals("csproj") || fileExtension.Equals("metaproj") || fileExtension.Equals("targets"))
            {
                fileExtension = "xml";
            }
            object[] sourceFileResults = new object[4];
            sourceFileResults[0] = sourceFileName;
            sourceFileResults[1] = sourceFileText;
            sourceFileResults[2] = sourceFileLineNumber;
            sourceFileResults[3] = fileExtension;
            return sourceFileResults;
        }

        /// <summary>
        /// Finds the line number for a Task
        /// </summary>
        /// <param name="text"> The file information for the target node </param>
        /// <param name="parent"> Target the task should reside in </param>
        /// <param name="name"> Name of the task to highlight </param>
        /// <returns> Line number to highlight</returns>
        public static int  TaskLineNumber(SourceText text, TreeNode parent, string name)
        {
            Target target = parent as Target;
            if (target == null)
            {
                return -1;
            }
            return TargetLineNumber(text, target.Name, name);
        }

        /// <summary>
        /// Finds the line number for a Target
        /// </summary>
        /// <param name="text"> The file information for the target node </param>
        /// <param name="targetName"> Name of the target to find in the file </param>
        /// <param name="taskName"> Name of the task to find in the file </param>
        /// <returns> Line number to highlight</returns>
        public static int TargetLineNumber(SourceText text, string targetName, string taskName = null)
        {
            var xml = text.XmlRoot;
            IXmlElement root = xml.Root;
            int startPosition = 0;
            int line = 0;

            // work around a bug in Xml Parser where a virtual parent is created around the root element
            // when the root element is preceded by trivia (comment)
            if (root.Name == null && root.Elements.FirstOrDefault() is IXmlElement firstElement && firstElement.Name == "Project")
            {
                root = firstElement;
            }

            foreach (var element in root.Elements)
            {
                if (element.Name == "Target" && element.Attributes != null)
                {
                    var nameAttribute = element.AsSyntaxElement.Attributes.FirstOrDefault(a => a.Name == "Name" && a.Value == targetName);
                    if (nameAttribute != null)
                    {
                        startPosition = nameAttribute.ValueNode.Start;

                        if (taskName != null)
                        {
                            var tasks = element.Elements.Where(e => e.Name == taskName).ToArray();
                            if (tasks.Length == 1)
                            {
                                startPosition = tasks[0].AsSyntaxElement.NameNode.Start;
                            }
                        }

                        break;
                    }
                }
            }

            if (startPosition > 0)
            {
                line = text.GetLineNumberFromPosition(startPosition);
            }

            return  line + 1;
        }
    }
}
