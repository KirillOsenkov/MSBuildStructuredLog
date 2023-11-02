using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using System.IO;
using System.Threading;

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

        public static void RedactSecrets(
            string binlogPath,
            string[] secrets)
         => RedactSecrets(binlogPath, secrets, processEmbeddedFiles: true, progress: null);

        public static void RedactSecrets(
            string binlogPath,
            string[] secrets,
            bool processEmbeddedFiles,
            Progress progress)
        {
            string outputFile = Path.Combine(PathUtils.TempPath, Path.GetFileName(Path.GetTempFileName()) + ".binlog");
            RedactSecrets(binlogPath, outputFile, secrets, processEmbeddedFiles, progress);
            File.Delete(binlogPath);
            File.Move(outputFile, binlogPath);
        }

        public static void RedactSecrets(
            string binlogPath,
            string outputFile,
            string[] secrets,
            bool processEmbeddedFiles,
            Progress progress)
        {
            if (string.IsNullOrEmpty(outputFile) ||
                string.Equals(binlogPath, outputFile, StringComparison.OrdinalIgnoreCase))
            {
                // This is in place redaction.
                RedactSecrets(binlogPath, secrets, processEmbeddedFiles, progress);
                return;
            }
            var sensitivityProcessor = new SimpleSensitiveDataProcessor(secrets, true);
            new BinlogRedactor(sensitivityProcessor) { Progress = progress }
                .ProcessBinlog(binlogPath, outputFile, skipEmbeddedFiles: !processEmbeddedFiles);
        }

        public BinlogRedactor(ISensitiveDataProcessor sensitiveDataProcessor)
        {
            _sensitiveDataProcessor = sensitiveDataProcessor;
        }

        public Progress Progress { private get; set; }

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

            var inputStream = new FileStream(inputFileName, FileMode.Open, FileAccess.Read, FileShare.Read);

            CancellationTokenSource cts = null;
            if (Progress != null)
            {
                cts = new CancellationTokenSource();
                long streamLength = inputStream.Length;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        await System.Threading.Tasks.Task.Delay(200, cts.Token);
                        Progress.Report((double)inputStream.Position / streamLength);
                    }
                }, cts.Token);
            }

            originalEventsSource.Replay(inputStream, CancellationToken.None);
            outputBinlog.Shutdown();

            // TODO: error handling

            if (Progress != null)
            {
                cts.Cancel();
                Progress.Report(1.0);
            }

            ((IBuildEventStringsReader)originalEventsSource).StringReadDone -= HandleStringRead;

            void HandleStringRead(StringReadEventArgs args)
            {
                args.StringToBeUsed = _sensitiveDataProcessor.ReplaceSensitiveData(args.OriginalString);
            }
        }
    }
}
