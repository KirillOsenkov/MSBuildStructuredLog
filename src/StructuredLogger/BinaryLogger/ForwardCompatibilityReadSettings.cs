using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public delegate bool AllowForwardCompatibilityDelegate(string message);

    public interface IForwardCompatibilityReadSettings
    {
        /// <summary>
        /// Unknown build events or unknown parts of known build events will be ignored if this returns true.
        /// </summary>
        /// <param name="versionErrorMessage">Version error message in case log is of higher then known version.</param>
        bool AllowForwardCompatibility(string versionErrorMessage);

        /// <summary>
        /// Optional handler for recoverable read errors.
        /// </summary>
        Action<BinaryLogReaderErrorEventArgs> ErrorHandler { get; }
    }

    public static class CompatibilitySettingsExtensions
    {
        public static IForwardCompatibilityReadSettings WithDefaultHandler(
                       this AllowForwardCompatibilityDelegate allowForwardCompatibilityCallback)
            => new ConditionalForwardCompatibilityWithDefaultHandler(allowForwardCompatibilityCallback);

        public static IForwardCompatibilityReadSettings WithCustomErrorHandler(
            this IForwardCompatibilityReadSettings settings,
            Action<BinaryLogReaderErrorEventArgs> errorHandler)
        {
            if (errorHandler == null)
            {
                return settings;
            }

            if (settings is ConditionalForwardCompatibilityWithDefaultHandler defualtHandler)
            {
                return defualtHandler.WithCustomErrorHandler(errorHandler);
            }

            return new ForwardCompatibilityReadSettings(
                settings.AllowForwardCompatibility,
                settings.ErrorHandler + errorHandler);
        }
    }

    public class ConditionalForwardCompatibilityWithDefaultHandler : IForwardCompatibilityReadSettings
    {
        private readonly AllowForwardCompatibilityDelegate _allowForwardCompatibilityCallback;
        public ConditionalForwardCompatibilityWithDefaultHandler(
            AllowForwardCompatibilityDelegate allowForwardCompatibilityCallback)
            => _allowForwardCompatibilityCallback = allowForwardCompatibilityCallback;

        public bool AllowForwardCompatibility(string versionErrorMessage)
            => _allowForwardCompatibilityCallback?.Invoke(versionErrorMessage) ?? false;

        public Action<BinaryLogReaderErrorEventArgs> ErrorHandler => null;

        internal IForwardCompatibilityReadSettings WithCustomErrorHandler(
            Action<BinaryLogReaderErrorEventArgs> errorHandler)
        {
            return new ForwardCompatibilityReadSettings(this._allowForwardCompatibilityCallback, errorHandler);
        }
    }

    public class ForwardCompatibilityReadSettings : IForwardCompatibilityReadSettings
    {
        private readonly AllowForwardCompatibilityDelegate _allowForwardCompatibilityCallback;

        public ForwardCompatibilityReadSettings(
            AllowForwardCompatibilityDelegate allowForwardCompatibilityCallback,
            Action<BinaryLogReaderErrorEventArgs> errorHandler)
        {
            _allowForwardCompatibilityCallback = allowForwardCompatibilityCallback;
            ErrorHandler = errorHandler;
        }

        public bool AllowForwardCompatibility(string versionErrorMessage) =>
            _allowForwardCompatibilityCallback?.Invoke(versionErrorMessage) ?? false;
        public Action<BinaryLogReaderErrorEventArgs> ErrorHandler { get; private init; }
    }
}
