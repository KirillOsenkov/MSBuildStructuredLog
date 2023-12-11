using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class ForwardCompatibilityReadingHandler
    {
        public enum Mode
        {
            Disallow,
            FailOnError,
            LogErrorsSummary,
            LogErrorsDetailed,
            IgnoreErrors,
            Invalid
        }

        private bool _isInitialized;
        private Mode _mode;

        private void CheckInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("ForwardCompatibilityReadingHandler is not initialized");
            }
        }

        public void SetMode(Mode mode)
        {
            _isInitialized = true;
            _mode = mode;
        }

        public bool ForwardCompatibilityExplicitlyConfigured { get; private set; }

        public bool ProcessCommandLine(ref string[] args)
        {
            _isInitialized = true;

            var compatArgs = args
                .Where(arg =>
                    arg.StartsWith("--forwardCompatibility", StringComparison.CurrentCultureIgnoreCase) ||
                    arg.StartsWith("-fwd", StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            if (compatArgs.Count == 0)
            {
                return true;
            }

            ForwardCompatibilityExplicitlyConfigured = true;

            if (compatArgs.Count > 1)
            {
                Console.Error.WriteLine("Only one --forwardCompatibility/-fwd argument is allowed");
                return false;
            }

            var compatArg = compatArgs[0];
            args = args.Where(arg => arg != compatArg).ToArray();

            int colonIndex = compatArg.IndexOf(':');

            if (colonIndex == -1)
            {
                _mode = Mode.LogErrorsSummary;
                return true;
            }

            _mode = compatArg.Substring(colonIndex + 1).ToLowerInvariant() switch
            {
                "d" or "disallow" => Mode.Disallow,
                "f" or "failonerror" => Mode.FailOnError,
                "l" or "logerrorssummary" => Mode.LogErrorsSummary,
                "lv" or "logerrorsdetailed" => Mode.LogErrorsDetailed,
                "i" or "ignoreerrors" => Mode.IgnoreErrors,
                _ => Mode.Invalid
            };

            if (_mode == Mode.Invalid)
            {
                Console.Error.WriteLine("Invalid forward compatibility mode");
                return false;
            }

            return true;
        }

        public Build ReadBuild(string binLogFilePath)
        {
            if (string.IsNullOrEmpty(binLogFilePath) || !File.Exists(binLogFilePath))
            {
                return null;
            }

            binLogFilePath = Path.GetFullPath(binLogFilePath);

            var build = BinaryLog.ReadBuild(binLogFilePath, this.AllowForwardCompatibilityDelegate);
            if (_compatibilityException != null)
            {
                throw _compatibilityException;
            }
            return build;
        }

        private Exception _compatibilityException = null;
        public IForwardCompatibilityReadSettings AllowForwardCompatibilityDelegate
        {
            get
            {
                CheckInitialized();

                IForwardCompatibilityReadSettings allowCompatSettings =
                    ((AllowForwardCompatibilityDelegate)(_ => true)).WithDefaultHandler();

                return _mode switch
                {
                    Mode.Disallow => null,
                    Mode.FailOnError => allowCompatSettings.WithCustomErrorHandler(err =>
                        throw (_compatibilityException = new Exception(err.GetFormattedMessage()))),
                    _ => allowCompatSettings
                };
            }
        }

        public void HandleBuildResults(Build build, TextWriter? errorWriter = null)
        {
            CheckInitialized();

            errorWriter ??= Console.Error;

            if (_mode != Mode.LogErrorsSummary && _mode != Mode.LogErrorsDetailed)
            {
                return;
            }

            var errors = build.RecoverableReadingErrors;
            if (errors == null || errors.Count == 0)
            {
                return;
            }

            IEnumerable<string> summaryLines;

            if (_mode == Mode.LogErrorsSummary)
            {
                summaryLines = errors
                    .GroupBy(e => e.errorType)
                    .Select(g =>
                        $"{SpacedReaderErrorType(g.Key)}: {g.Sum(e => e.count)} Total errors (in {g.GroupBy(f => f.recordKind).Count()} distinct types of records)");
            }
            else
            {
                summaryLines = errors
                    .Select(e => $"| {SpacedReaderErrorType(e.errorType)} | {e.recordKind} | {e.count} |")
                    .Prepend($"| :{new string('-', _maxErrorTypeLength-1)} | :---------- | -----------: |")
                    .Prepend($"| {SpacedReaderErrorType("Error Type")} | Record Kind | Errors Count |");
            }

            var summary = string.Join(Environment.NewLine, summaryLines);

            errorWriter.WriteLine();
            errorWriter.WriteLine($"Forward compatibility recoverable errors summary:");
            errorWriter.WriteLine();
            errorWriter.WriteLine(summary);
        }

        private static readonly int _maxErrorTypeLength = Enum.GetNames(typeof(ReaderErrorType)).Max(s => s.Length);
        private string SpacedReaderErrorType(ReaderErrorType errorType)
            => SpacedReaderErrorType(errorType.ToString());

        private string SpacedReaderErrorType(string text)
            => string.Format("{0,-" + _maxErrorTypeLength + "}", text);
    }
}
