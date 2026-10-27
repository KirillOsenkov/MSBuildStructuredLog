param(
    [Parameter(Mandatory = $true)][Int64]$DesktopHandle,
    [Parameter(Mandatory = $true)][int]$ProcessId
)

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DiagnosticDesktopCloser { [DllImport("user32.dll", SetLastError=true)] public static extern bool CloseDesktop(IntPtr desktop); }
'@
try {
    while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
        Start-Sleep -Seconds 1
    }
}
finally {
    [DiagnosticDesktopCloser]::CloseDesktop([IntPtr]$DesktopHandle) | Out-Null
}
