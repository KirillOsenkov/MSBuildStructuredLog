param(
    [Parameter(Mandatory = $true)][string]$Session,
    [Parameter(Mandatory = $true)][string]$Request,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$descriptor = Get-Content -Raw ([IO.Path]::GetFullPath($Session)) | ConvertFrom-Json
if ($descriptor.protocol -ne 1 -or -not $descriptor.pipeName -or -not $descriptor.processId -or -not $descriptor.executable) {
    throw "Invalid or unsupported diagnostic session descriptor: $Session"
}
if (-not (Get-Process -Id ([int]$descriptor.processId) -ErrorAction SilentlyContinue)) {
    throw "Recorded diagnostic process $($descriptor.processId) is not running."
}
if (-not (Test-Path $descriptor.executable)) {
    throw "Recorded executable is missing: $($descriptor.executable)"
}

$pipe = [IO.Pipes.NamedPipeClientStream]::new('.', $descriptor.pipeName, [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
$operation = 'request-' + [Guid]::NewGuid().ToString('N')
$readerType = @'
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
public static class DiagnosticPipeReader {
  public static string ReadFrame(NamedPipeClientStream pipe) {
    byte[] header = new byte[4];
    ReadExactly(pipe, header);
    int length = BitConverter.ToInt32(header, 0);
    if (length < 1 || length > 1048576) throw new InvalidDataException("Invalid response frame length: " + length);
    byte[] response = new byte[length];
    ReadExactly(pipe, response);
    return Encoding.UTF8.GetString(response);
  }
  private static void ReadExactly(Stream stream, byte[] buffer) {
    int offset = 0;
    while (offset < buffer.Length) {
      int count = stream.Read(buffer, offset, buffer.Length - offset);
      if (count == 0) throw new EndOfStreamException("Diagnostic server returned an incomplete response.");
      offset += count;
    }
  }
  public static Task<string> ReadAsync(NamedPipeClientStream pipe) => Task.Run(() => ReadFrame(pipe));
}
'@
if (-not ('DiagnosticPipeReader' -as [type])) {
    Add-Type $readerType
}
try {
    $pipe.Connect($TimeoutSeconds * 1000)
    $requestBytes = [Text.Encoding]::UTF8.GetBytes($Request)
    $header = [BitConverter]::GetBytes([int]$requestBytes.Length)
    $pipe.Write($header, 0, $header.Length)
    $pipe.Write($requestBytes, 0, $requestBytes.Length)
    $pipe.Flush()
    $responseTask = [DiagnosticPipeReader]::ReadAsync($pipe)
    if (-not $responseTask.Wait($TimeoutSeconds * 1000)) {
        throw "Timed out waiting for operation $operation. The app operation may still be running; do not retry mutations until state is inspected."
    }
    $response = $responseTask.GetAwaiter().GetResult() | ConvertFrom-Json
    if ($response.status -ne 'completed') {
        throw "Operation $operation failed: $($response.error)"
    }
    $response.result | ConvertTo-Json -Depth 20
}
finally {
    $pipe.Dispose()
}
