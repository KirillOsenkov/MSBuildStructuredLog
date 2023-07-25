using System;
using System.Runtime.InteropServices;

namespace StructuredLogger;

public class PlatformUtilities
{
    public static bool HasThreads => !_isBrowser;

    private static readonly bool _isBrowser = RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));
}
