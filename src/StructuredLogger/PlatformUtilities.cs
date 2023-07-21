using System;
using System.Runtime.InteropServices;

namespace StructuredLogger;

public class PlatformUtilities
{
    public static bool HasThreads => !RuntimeInformation.IsOSPlatform(_browser);

    private static readonly OSPlatform _browser = OSPlatform.Create("BROWSER");
}
