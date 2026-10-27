param(
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Session,
    [Parameter(Mandatory = $true)][string]$Profile
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
$Executable = [IO.Path]::GetFullPath($Executable)
$Session = [IO.Path]::GetFullPath($Session)
$Profile = [IO.Path]::GetFullPath($Profile)
$desktopId = 'MSBuildStructuredLog-' + [Guid]::NewGuid().ToString('N')
$supervisorSource = @'
using System;
using System.Runtime.InteropServices;
public static class DiagnosticDesktop {
  [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr devmode, int flags, uint access, IntPtr security);
  [DllImport("user32.dll", SetLastError=true)] public static extern bool CloseDesktop(IntPtr desktop);
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct StartupInfo { public int cb; public string reserved; public string desktop; public string title; public int x; public int y; public int xSize; public int ySize; public int xCountChars; public int yCountChars; public int fillAttribute; public int flags; public short showWindow; public short reserved2Size; public IntPtr reserved2; public IntPtr stdInput; public IntPtr stdOutput; public IntPtr stdError; }
  [StructLayout(LayoutKind.Sequential)] public struct ProcessInfo { public IntPtr process; public IntPtr thread; public int processId; public int threadId; }
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool CreateProcess(string app, System.Text.StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string currentDirectory, ref StartupInfo startup, out ProcessInfo info);
  [DllImport("kernel32.dll", SetLastError=true)] public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
  [DllImport("kernel32.dll", SetLastError=true)] public static extern bool CloseHandle(IntPtr handle);
}
'@
if (-not ('DiagnosticDesktop' -as [type])) {
    Add-Type $supervisorSource
}

if (Test-Path $Session) {
    try {
        $existing = Get-Content -Raw $Session | ConvertFrom-Json
        if (Get-Process -Id ([int]$existing.processId) -ErrorAction SilentlyContinue) {
            if ([IO.Path]::GetFullPath($existing.executable) -ne $Executable) {
                throw "Process $($existing.processId) is live from a different executable: $($existing.executable)"
            }
            & (Join-Path $PSScriptRoot 'Invoke-LocalControl.ps1') -Session $Session -Request '{"op":"state"}' | Out-Null
            return
        }
        Remove-Item -LiteralPath $Session
    }
    catch {
        if ($_.Exception.Message -like 'Process * is alive*') {
            throw
        }
        Remove-Item -LiteralPath $Session -ErrorAction SilentlyContinue
    }
}

$configuration = 'Diagnostic'
$build = Join-Path $Root 'src\StructuredLogViewer\StructuredLogViewer.csproj'
$buildArgs = @('build', $build, '-c', $configuration, '-p:PublicRelease=true', '-p:BinaryLogger=false')
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Diagnostic viewer build failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path $Executable)) {
    throw "Build succeeded but executable was not found at $Executable."
}

New-Item -ItemType Directory -Force -Path $Profile, (Split-Path -Parent $Session) | Out-Null
$env:MSBUILDSTRUCTUREDLOG_DATA_DIR = $Profile
$log = Join-Path $Profile 'diagnostics-bootstrap.log'
$desktop = [DiagnosticDesktop]::CreateDesktop($desktopId, [IntPtr]::Zero, [IntPtr]::Zero, 0, 0x01FF, [IntPtr]::Zero)
if ($desktop -eq [IntPtr]::Zero) {
    throw "CreateDesktop failed with Win32 error $([Runtime.InteropServices.Marshal]::GetLastWin32Error())."
}

$env:MSBUILDSTRUCTUREDLOG_DATA_DIR = $Profile
$dotnetHost = (Get-Command dotnet.exe).Source
$commandLine = '"' + $dotnetHost + '" "' + $Executable + '" --local-control "' + $Session + '"'
$startup = [DiagnosticDesktop+StartupInfo]::new()
$startup.cb = [Runtime.InteropServices.Marshal]::SizeOf($startup)
$startup.desktop = $desktopId
$processInfo = [DiagnosticDesktop+ProcessInfo]::new()
$created = [DiagnosticDesktop]::CreateProcess($dotnetHost, [Text.StringBuilder]::new($commandLine), [IntPtr]::Zero, [IntPtr]::Zero, $false, 0x08000000, [IntPtr]::Zero, $Root, [ref]$startup, [ref]$processInfo)
if (-not $created) {
    $errorCode = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
    [DiagnosticDesktop]::CloseDesktop($desktop) | Out-Null
    throw "CreateProcess failed with Win32 error $errorCode."
}
$processId = $processInfo.processId
[DiagnosticDesktop]::CloseHandle($processInfo.thread) | Out-Null

$supervisorScript = Join-Path $PSScriptRoot 'DesktopSupervisor.ps1'
$supervisorArgs = @('-NoProfile', '-WindowStyle', 'Hidden', '-File', $supervisorScript, '-DesktopHandle', $desktop.ToInt64(), '-ProcessId', $processId)
Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -ArgumentList $supervisorArgs -WindowStyle Hidden | Out-Null

$deadline = [DateTime]::UtcNow.AddSeconds(45)
while ([DateTime]::UtcNow -lt $deadline) {
    if ((Get-Process -Id $processId -ErrorAction SilentlyContinue) -eq $null) {
        [DiagnosticDesktop]::CloseDesktop($desktop) | Out-Null
        throw "Diagnostic process $processId exited during startup. See $log."
    }
    if (Test-Path $Session) {
        try {
            & (Join-Path $PSScriptRoot 'Invoke-LocalControl.ps1') -Session $Session -Request '{"op":"state"}' -TimeoutSeconds 2
            [pscustomobject]@{ status = 'ready'; processId = $processId; executable = $Executable; session = $Session; profile = $Profile; desktop = $desktopId }
                return
        }
        catch {
        }
    }
    Start-Sleep -Milliseconds 500
}

if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
    Get-Content -Raw -ErrorAction SilentlyContinue $log
    throw "Diagnostic process is alive but the internal state request timed out. Inspect $Profile."
}
