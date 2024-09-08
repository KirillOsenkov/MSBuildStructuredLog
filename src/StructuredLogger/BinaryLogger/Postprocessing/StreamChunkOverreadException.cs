﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class StreamChunkOverReadException : Exception
    {
        public StreamChunkOverReadException()
        {
        }

        public StreamChunkOverReadException(string message) : base(message)
        {
        }

        public StreamChunkOverReadException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
