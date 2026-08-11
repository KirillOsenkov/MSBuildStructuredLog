using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Metadata available before a length-framed binary log event is fully deserialized.
    /// </summary>
    public readonly struct BinaryLogEventMetadata
    {
        public BinaryLogEventMetadata(
            BinaryLogRecordKind recordKind,
            BuildEventContext buildEventContext,
            BuildEventContext originalBuildEventContext = null)
        {
            RecordKind = recordKind;
            BuildEventContext = buildEventContext;
            OriginalBuildEventContext = originalBuildEventContext;
        }

        /// <summary>
        /// The serialized event type.
        /// </summary>
        public BinaryLogRecordKind RecordKind { get; }

        /// <summary>
        /// The event's build context, or <see langword="null"/> when the event has no context.
        /// </summary>
        public BuildEventContext BuildEventContext { get; }

        /// <summary>
        /// The original context carried by a target-skipped event, or <see langword="null"/>.
        /// </summary>
        public BuildEventContext OriginalBuildEventContext { get; }
    }

    /// <summary>
    /// Decides whether a binary log event should be deserialized and dispatched.
    /// </summary>
    /// <remarks>
    /// Returning <see langword="false"/> skips the event. For length-framed binlogs, the
    /// type-specific payload can be skipped without deserializing it. Auxiliary string,
    /// name/value-list, and embedded-content records are still read so retained events can be
    /// decoded correctly.
    ///
    /// The filter is responsible for retaining a structurally consistent set of events. For
    /// example, retaining a finish event while dropping its corresponding start event can produce
    /// a log that downstream consumers cannot interpret correctly.
    /// </remarks>
    public delegate bool BinaryLogEventFilter(BinaryLogEventMetadata metadata);

    internal sealed class BinaryLogEventFilterException : Exception
    {
        public BinaryLogEventFilterException(Exception innerException)
            : base("The binary log event filter threw an exception.", innerException)
        {
        }
    }
}
