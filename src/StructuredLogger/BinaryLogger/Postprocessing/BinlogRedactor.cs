using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System.IO;

namespace StructuredLogger.BinaryLogger.Postprocessing
{
    public interface ISensitiveDataProcessor
    {
        /// <summary>
        /// Processes the given text and if needed, replaces sensitive data with a placeholder.
        /// </summary>
        string ReplaceSensitiveData(string text);
    }

    internal sealed class SimpleSensitiveDataProcessor : ISensitiveDataProcessor
    {
        public const string DefaultReplacementPattern = "*******";
        private readonly (string token, string replacement)[] _secretsToRedact;

        public SimpleSensitiveDataProcessor(string[] secretsToRedact, bool identifyReplacements)
        {
            _secretsToRedact = secretsToRedact.Select((secret, cnt) =>
                    (token: secret, identifyReplacements ? $"REDACTED__TKN{(cnt + 1):00}" : DefaultReplacementPattern))
                .ToArray();
        }

        public string ReplaceSensitiveData(string text)
        {
            foreach ((string token, string replacement) secret in _secretsToRedact)
            {
                text = text.Replace(secret.token, secret.replacement);
            }

            return text;
        }
    }

    public class BinlogRedactor
    {
        private readonly ISensitiveDataProcessor _sensitiveDataProcessor;

        public static void RedactSecrets(string binlogPath, string[] secrets)
        {
            var sensitivityProcessor = new SimpleSensitiveDataProcessor(secrets, true);
            string outputFile = Path.Combine(PathUtils.TempPath, Path.GetFileName(Path.GetTempFileName()) + ".binlog");
            new BinlogRedactor(sensitivityProcessor).ProcessBinlog(binlogPath, outputFile, skipEmbeddedFiles: false);
            File.Delete(binlogPath);
            File.Move(outputFile, binlogPath);
        }

        public BinlogRedactor(ISensitiveDataProcessor sensitiveDataProcessor)
        {
            _sensitiveDataProcessor = sensitiveDataProcessor;
        }

        public void ProcessBinlog(
            string inputFileName,
            string outputFileName,
            bool skipEmbeddedFiles)
        {
            BinaryLogReplayEventSource originalEventsSource = new BinaryLogReplayEventSource();
            
            Microsoft.Build.Logging.StructuredLogger.BinaryLogger outputBinlog = new Microsoft.Build.Logging.StructuredLogger.BinaryLogger()
            {
                Parameters = $"LogFile={outputFileName};OmitInitialInfo",
            };

            ((IBuildEventStringsReader) originalEventsSource).StringReadDone += HandleStringRead;
            if (!skipEmbeddedFiles)
            {
                ((IBuildFileReader)originalEventsSource).ArchiveFileEncountered +=
                    ((Action<StringReadEventArgs>)HandleStringRead).ToArchiveFileHandler();
            }

            outputBinlog.Initialize(originalEventsSource);
            originalEventsSource.Replay(inputFileName);
            outputBinlog.Shutdown();

            // TODO: error handling

            void HandleStringRead(StringReadEventArgs args)
            {
                args.StringToBeUsed = _sensitiveDataProcessor.ReplaceSensitiveData(args.OriginalString);
            }
        }
    }
}
