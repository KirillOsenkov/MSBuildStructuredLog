using System;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Logging.StructuredLogger;

public class PlatformUtilities
{
    public static bool HasThreads => !_isBrowser && !_isWasi;

    public static bool HasTempStorage => !_isWasi;
    public static bool HasColor => !_isWasi;


    private static readonly bool _isBrowser = RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER"));
    private static readonly bool _isWasi = RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI"));
}
