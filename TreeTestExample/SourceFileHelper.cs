using StructuredLogViewerWASM.Pages;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;
using System.ComponentModel;

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
            string path;
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
            }
            else if (bn is Microsoft.Build.Logging.StructuredLogger.Task)
            {
                path = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).SourceFilePath;
                sourceFileName = ((Microsoft.Build.Logging.StructuredLogger.Task)bn).Name;
                sourceFileText = fileResolver.GetSourceFileText(path).Text;
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
            object[] sourceFileResults = new object[3];
            sourceFileResults[0] = sourceFileName;
            sourceFileResults[1] = sourceFileText;
            sourceFileResults[2] = sourceFileLineNumber;
            return sourceFileResults;
        }
    }
}
