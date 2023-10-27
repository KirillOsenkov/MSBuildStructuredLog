// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Build.Logging;

/// <summary>
/// Event arguments for <see cref="IBuildFileReader.ArchiveFileEncountered"/> event.
/// </summary>
public sealed class ArchiveFileEventArgs : EventArgs
{
    public ArchiveFileEventArgs(ArchiveFile archiveFile) =>
        ArchiveFile = archiveFile;

    
    public ArchiveFile ArchiveFile { get; set; }
}
