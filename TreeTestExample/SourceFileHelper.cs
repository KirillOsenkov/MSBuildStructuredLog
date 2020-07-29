using StructuredLogViewerWASM.Pages;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;

namespace StructuredLogViewerWASM
{
    public class SourceFileHelper
    {
        /// <summary>
        /// Determines the SourceFile (text, name, line number) from the tree node
        /// </summary>
        /// <param name="ContainerSplit">The parent container, holding the shared variables </param>
        /// <param name="fileResolver"> Either the Source or Archive File Resolver to read file from </param>
        /// <param name="bn">BaseNode to be reading file from</param>
        public static SplitPane SourceFileText(SplitPane ContainerSplit, ISourceFileResolver fileResolver, BaseNode bn)
        {
            string path;
            ContainerSplit.sourceFileText = null;
            ContainerSplit.sourceFileName = "";

            if (bn is AbstractDiagnostic)
            {
                AbstractDiagnostic ad = (AbstractDiagnostic)bn;
                path = ad.ProjectFile;
                if (ad.IsTextShortened)
                {
                    ContainerSplit.sourceFileText = ad.Text;
                    ContainerSplit.sourceFileName = ad.ShortenedText;
                }
                else
                {
                    ContainerSplit.sourceFileText = fileResolver.GetSourceFileText(path).Text;
                    ContainerSplit.sourceFileName = ad.Name;
                }
                ContainerSplit.sourceFileLineNumber = ad.LineNumber;
            }
            else if (bn is Project)
            {
                path = ((Project)bn).SourceFilePath;
                ContainerSplit.sourceFileName = ((Project)bn).Name;
                ContainerSplit.sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is Target)
            {
                path = ((Target)bn).SourceFilePath;
                ContainerSplit.sourceFileName = ((Target)bn).Name;
                ContainerSplit.sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is Microsoft.Build.Logging.StructuredLogger.Task)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).SourceFilePath;
                ContainerSplit.sourceFileName = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).Name;
                ContainerSplit.sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is IHasSourceFile && ((IHasSourceFile)bn).SourceFilePath != null)
            {
                path = ((IHasSourceFile)bn).SourceFilePath;
                ContainerSplit.sourceFileName = ((IHasSourceFile)bn).SourceFilePath;
                ContainerSplit.sourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is SourceFileLine && ((SourceFileLine)bn).Parent is Microsoft.Build.Logging.StructuredLogger.SourceFile
            && ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).SourceFilePath != null)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).SourceFilePath;
                ContainerSplit.sourceFileName = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).Name;
                ContainerSplit.sourceFileText = fileResolver.GetSourceFileText(path).Text;
                ContainerSplit.sourceFileLineNumber = ((SourceFileLine)bn).LineNumber;
            }
            else if (bn is NameValueNode && ((NameValueNode)bn).IsValueShortened)
            {
                ContainerSplit.sourceFileText = ((NameValueNode)bn).Value;
                ContainerSplit.sourceFileName = ((NameValueNode)bn).Name;
            }
            else if (bn is TextNode && ((TextNode)bn).IsTextShortened)
            {
                ContainerSplit.sourceFileText = ((TextNode)bn).Text;
                ContainerSplit.sourceFileName = ((TextNode)bn).Name;
            }

            if (ContainerSplit.sourceFileText == null)
            {
                ContainerSplit.sourceFileText = "No file to display";
            }

            return ContainerSplit;
        }
    }
}
