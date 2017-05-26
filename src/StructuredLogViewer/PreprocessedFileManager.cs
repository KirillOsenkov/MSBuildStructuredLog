using System;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    public class PreprocessedFileManager
    {
        private Build build;
        private SourceFileResolver sourceFileResolver;
        private BuildControl buildControl;

        public PreprocessedFileManager(BuildControl buildControl, SourceFileResolver sourceFileResolver)
        {
            this.build = buildControl.Build;
            this.buildControl = buildControl;
            this.sourceFileResolver = sourceFileResolver;
        }

        public Action GetPreprocessAction(string sourceFilePath, SourceText text)
        {
            if (!CanPreprocess(sourceFilePath))
            {
                return null;
            }

            return () => ShowPreprocessed(sourceFilePath);
        }

        private void ShowPreprocessed(string sourceFilePath)
        {
            var filePath = SettingsService.WriteContentToTempFileAndGetPath("Preprocessed " + sourceFilePath, ".txt");
            buildControl.DisplayFile(filePath);
        }

        private bool CanPreprocess(string sourceFilePath)
        {
            return false;
        }
    }
}
