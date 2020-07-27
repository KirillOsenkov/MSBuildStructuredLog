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
            ContainerSplit.SourceFileText = null;
            ContainerSplit.sfn = "";

            if (bn is AbstractDiagnostic)
            {
                AbstractDiagnostic ad = (AbstractDiagnostic)bn;
                path = ad.ProjectFile;
                if (ad.IsTextShortened)
                {
                    ContainerSplit.SourceFileText = ad.Text;
                    ContainerSplit.sfn = ad.ShortenedText;
                }
                else
                {
                    ContainerSplit.SourceFileText = fileResolver.GetSourceFileText(path).Text;
                    ContainerSplit.sfn = ad.Name;
                }
            }
            else if (bn is Project)
            {
                path = ((Project)bn).SourceFilePath;
                ContainerSplit.sfn = ((Project)bn).Name;
                ContainerSplit.SourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is Target)
            {
                path = ((Target)ContainerSplit.selected).SourceFilePath;
                ContainerSplit.sfn = ((Target)bn).Name;
                ContainerSplit.SourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is Microsoft.Build.Logging.StructuredLogger.Task)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).SourceFilePath;
                ContainerSplit.sfn = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).Name;
                ContainerSplit.SourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is IHasSourceFile && ((IHasSourceFile)bn).SourceFilePath != null)
            {
                path = ((IHasSourceFile)bn).SourceFilePath;
                ContainerSplit.sfn = ((IHasSourceFile)bn).SourceFilePath;
                ContainerSplit.SourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is SourceFileLine && ((SourceFileLine)bn).Parent is Microsoft.Build.Logging.StructuredLogger.SourceFile
            && ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).SourceFilePath != null)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).SourceFilePath;
                ContainerSplit.sfn = ((Microsoft.Build.Logging.StructuredLogger.SourceFile)((SourceFileLine)bn).Parent).Name;
                ContainerSplit.SourceFileText = fileResolver.GetSourceFileText(path).Text;
            }
            else if (bn is NameValueNode && ((NameValueNode)bn).IsValueShortened)
            {
                ContainerSplit.SourceFileText = ((NameValueNode)bn).Value;
                ContainerSplit.sfn = ((NameValueNode)bn).Name;
            }
            else if (bn is TextNode && ((TextNode)bn).IsTextShortened)
            {
                ContainerSplit.SourceFileText = ((TextNode)bn).Text;
                ContainerSplit.sfn = ((TextNode)bn).Name;
            }

            if (ContainerSplit.SourceFileText == null)
            {
                ContainerSplit.SourceFileText = "No file to display";
            }

            return ContainerSplit;
        }
    }
}
