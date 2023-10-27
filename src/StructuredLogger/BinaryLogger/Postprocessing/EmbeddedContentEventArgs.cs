// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging
{
    internal sealed class EmbeddedContentEventArgs : EventArgs
    {
        public EmbeddedContentEventArgs(BinaryLogRecordKind contentKind, Stream contentStream)
        {
            ContentKind = contentKind;
            ContentStream = contentStream;
        }

        public BinaryLogRecordKind ContentKind { get; }
        public Stream ContentStream { get; }
    }
}
