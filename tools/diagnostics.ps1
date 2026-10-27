param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('start', 'status', 'state', 'screenshot', 'request', 'stop')]
    [string]$Action,
    [string]$Request,
    [string]$Session,
    [string]$ScreenshotPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sessionRoot = Join-Path $env:LOCALAPPDATA 'MSBuildStructuredLog\Diagnostics'
if (-not $Session) {
    $Session = Join-Path $sessionRoot 'default\session.json'
}
$Session = [IO.Path]::GetFullPath($Session)
$profile = Join-Path (Split-Path -Parent $Session) 'profile'
$launcher = Join-Path $PSScriptRoot 'Start-LocalControl.ps1'
$client = Join-Path $PSScriptRoot 'Invoke-LocalControl.ps1'
$exe = Join-Path $root 'bin\StructuredLogViewer\Diagnostic\net10.0-windows\StructuredLogViewer.dll'

if ($Action -eq 'start') {
    & $launcher -Root $root -Executable $exe -Session $Session -Profile $profile
    exit $LASTEXITCODE
}

if ($Action -eq 'status' -and -not (Test-Path $Session)) {
    [pscustomobject]@{ status = 'stopped'; session = $Session }
    exit 0
}

if (-not (Test-Path $Session)) {
    throw "No diagnostic session at $Session. Run diagnostics.ps1 -Action start first."
}

if ($Action -eq 'state' -or $Action -eq 'status') {
    & $client -Session $Session -Request '{"op":"state"}'
    exit $LASTEXITCODE
}

if ($Action -eq 'screenshot') {
    if (-not $ScreenshotPath) {
        $ScreenshotPath = Join-Path (Split-Path -Parent $Session) 'screenshot.png'
    }
    $payload = @{ op = 'screenshot'; path = [IO.Path]::GetFullPath($ScreenshotPath) } | ConvertTo-Json -Compress
    & $client -Session $Session -Request $payload
    exit $LASTEXITCODE
}

if ($Action -eq 'request') {
    if (-not $Request) {
        throw '-Request is required for the request action.'
    }
    & $client -Session $Session -Request $Request
    exit $LASTEXITCODE
}

if ($Action -eq 'stop') {
    & $client -Session $Session -Request '{"op":"shutdown"}'
    exit $LASTEXITCODE
}
