# Local viewer diagnostics

The diagnostic channel is opt-in and starts only when the viewer receives `--local-control <session.json>`. It uses a same-user named pipe, dispatches requests on the WPF dispatcher, and uses the viewer's real binlog open, search index, tree selection, and WPF rendering paths. Each request receives a completed or failed result. A client timeout does not cancel a running app operation.

From a PowerShell prompt in the repository:

```powershell
./tools/diagnostics.ps1 -Action start
./tools/diagnostics.ps1 -Action status
./tools/diagnostics.ps1 -Action state
./tools/diagnostics.ps1 -Action request -Request '{"op":"open-binlog","path":"C:\\logs\\build.binlog"}'
./tools/diagnostics.ps1 -Action request -Request '{"op":"select-node","text":"CoreCompile"}'
./tools/diagnostics.ps1 -Action screenshot
./tools/diagnostics.ps1 -Action stop
```

`start` builds the Debug `net10.0-windows` viewer, creates a fresh isolated Windows desktop, and sets `MSBUILDSTRUCTUREDLOG_DATA_DIR` for a separate local profile. The session descriptor records protocol, pipe, PID, executable path, startup time, and profile. Repeating `start` reconnects to a live responsive session; it refuses to replace a live but unresponsive process. Pass `-Session <path>` to use a named session. `screenshot` accepts `-ScreenshotPath <path>` and defaults next to the session descriptor.

The request operations are `state`, `status`, `open-binlog` with `path`, `select-node` with `text`, `screenshot` with `path`, and `shutdown`. `status` is an IPC-backed state query when a descriptor exists and reports stopped when it does not. A tree selection is model-driven through `BuildControl.SelectItem`; screenshots use WPF `RenderTargetBitmap` and do not capture native or GPU-hosted surfaces.

The channel is diagnostic development functionality. It is not built into the normal application invocation unless the process is explicitly started with the private control argument. It does not provide arbitrary shell or reflection execution.

Verified viewer scenario: [scenario-final.png](diagnostics/evidence/scenario-final.png). It shows a generated 82-node binlog opened in the viewer with the `CoreCompile scenario application` node selected.
