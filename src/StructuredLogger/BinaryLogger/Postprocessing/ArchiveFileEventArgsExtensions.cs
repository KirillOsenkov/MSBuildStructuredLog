﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging;

public static class ArchiveFileEventArgsExtensions
{
    public static Action<ArchiveFileEventArgs> ToArchiveFileHandler(this Action<StringReadEventArgs> stringHandler)
    {
        return args =>
        {
            var pathArgs = new StringReadEventArgs(args.ArchiveFile.FullPath);
            stringHandler(pathArgs);
            var contentArgs = new StringReadEventArgs(args.ArchiveFile.Text);
            stringHandler(contentArgs);

            args.ArchiveFile = new ArchiveFile(pathArgs.StringToBeUsed, contentArgs.StringToBeUsed);
        };
    }
}
