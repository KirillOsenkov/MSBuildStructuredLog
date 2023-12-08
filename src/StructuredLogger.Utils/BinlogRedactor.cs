using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Build.SensitiveDataDetector;

namespace StructuredLogger.Utils
{
    public sealed class BinlogRedactorOptions
    {
        public BinlogRedactorOptions(string inputPath)
        {
            InputPath = inputPath;
        }

        public string[]? TokensToRedact { get; set; }
        public string InputPath { get; set; }
        public string? OutputFileName { get; set; }
        public bool ProcessEmbeddedFiles { get; set; } = true;
        public bool IdentifyReplacemenets { get; set; } = true;
        public bool AutodetectCommonPatterns { get; set; } = true;
        public bool AutodetectUsername { get; set; } = true;
    }

    public class BinlogRedactor
    {
        private readonly ISensitiveDataRedactor _sensitiveDataRedactor;

        public static void RedactSecrets(
            string binlogPath,
            string[] secrets)
            => RedactSecrets(
                new BinlogRedactorOptions(binlogPath) { TokensToRedact = secrets, }, progress: null);

        public static void RedactSecrets(
            BinlogRedactorOptions redactorOptions,
            Progress progress)
        {
            string outputFile;
            bool replaceInPlace = false;

            if (string.IsNullOrEmpty(redactorOptions.OutputFileName) ||
                string.Equals(redactorOptions.InputPath, redactorOptions.OutputFileName, StringComparison.OrdinalIgnoreCase))
            {
                outputFile = Path.Combine(PathUtils.TempPath, Path.GetFileName(Path.GetTempFileName()) + ".binlog");
                replaceInPlace = true;
            }
            else
            {
                outputFile = redactorOptions.OutputFileName;
            }

            SensitiveDataKind sensitiveDataKind = SensitiveDataKind.ExplicitSecrets;
            if (redactorOptions.AutodetectCommonPatterns)
            {
                sensitiveDataKind |= SensitiveDataKind.CommonSecrets;
            }
            if (redactorOptions.AutodetectUsername)
            {
                sensitiveDataKind |= SensitiveDataKind.Username;
            }

            ISensitiveDataRedactor sensitiveDataRedactor = SensitiveDataDetectorFactory.GetSecretsDetector(
                sensitiveDataKind,
                redactorOptions.IdentifyReplacemenets,
                redactorOptions.TokensToRedact);

            new BinlogRedactor(sensitiveDataRedactor) { Progress = progress }
                .ProcessBinlog(redactorOptions.InputPath, outputFile, !redactorOptions.ProcessEmbeddedFiles);

            if (replaceInPlace)
            {
                File.Delete(redactorOptions.InputPath);
                File.Move(outputFile, redactorOptions.InputPath);
            }
        }

        public BinlogRedactor(ISensitiveDataRedactor sensitiveDataRedactor)
        {
            _sensitiveDataRedactor = sensitiveDataRedactor;
        }

        public Progress Progress { private get; set; }

        public void ProcessBinlog(
            string inputFileName,
            string outputFileName,
            bool skipEmbeddedFiles)
        {
            BinaryLogReplayEventSource originalEventsSource = new()
            {
                // Let's allow this always for redacting - as in GUI
                // the file already needed to be opened and in the CLI
                // we run the fwd compat mode always.
                AllowForwardCompatibility = true,
            };
            
            Microsoft.Build.Logging.StructuredLogger.BinaryLogger outputBinlog = new()
            {
                Parameters = $"LogFile={outputFileName};OmitInitialInfo",
            };

            ((IBuildEventArgsReaderNotifications) originalEventsSource).StringReadDone += HandleStringRead;
            if (!skipEmbeddedFiles)
            {
                ((IBuildEventArgsReaderNotifications)originalEventsSource).ArchiveFileEncountered +=
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

            ((IBuildEventArgsReaderNotifications)originalEventsSource).StringReadDone -= HandleStringRead;

            void HandleStringRead(StringReadEventArgs args)
            {
                args.StringToBeUsed = _sensitiveDataRedactor.Redact(args.OriginalString);
            }
        }
    }
}
