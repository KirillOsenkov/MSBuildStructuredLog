#if LOCAL_DIAGNOSTICS
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Controls;

namespace StructuredLogViewer
{
    internal sealed class LocalControlServer : IDisposable
    {
        private readonly Application application;
        private readonly string sessionPath;
        private readonly string pipeName;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private MainWindow window;
        private System.Threading.Tasks.Task listener;

        public LocalControlServer(Application application, string sessionPath)
        {
            this.application = application;
            this.sessionPath = Path.GetFullPath(sessionPath);
            pipeName = "MSBuildStructuredLog-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.GetDirectoryName(this.sessionPath));
            listener = System.Threading.Tasks.Task.Run(ListenAsync);
            File.WriteAllText(this.sessionPath, JsonSerializer.Serialize(new SessionDescriptor
            {
                Protocol = 1,
                PipeName = pipeName,
                ProcessId = Environment.ProcessId,
                Executable = Assembly.GetEntryAssembly().Location,
                StartedUtc = DateTime.UtcNow,
                Profile = Environment.GetEnvironmentVariable("MSBUILDSTRUCTUREDLOG_DATA_DIR")
            }, JsonOptions));
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public void Attach(MainWindow mainWindow)
        {
            window = mainWindow;
        }

        private async System.Threading.Tasks.Task ListenAsync()
        {
            while (!cancellation.IsCancellationRequested)
            {
                var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try
                {
                    await pipe.WaitForConnectionAsync(cancellation.Token).ConfigureAwait(false);
                    using (pipe)
                    using (pipe)
                    {
                        byte[] header = new byte[4];
                        await ReadExactlyAsync(pipe, header, cancellation.Token).ConfigureAwait(false);
                        int requestLength = BitConverter.ToInt32(header, 0);
                        if (requestLength > 0 && requestLength <= 1024 * 1024)
                        {
                            byte[] requestBytes = new byte[requestLength];
                            await ReadExactlyAsync(pipe, requestBytes, cancellation.Token).ConfigureAwait(false);
                            string line = Encoding.UTF8.GetString(requestBytes);
                            string response;
                            try
                            {
                                JsonElement request = JsonDocument.Parse(line).RootElement;
                                object result = await application.Dispatcher.InvokeAsync(() => HandleAsync(request)).Task.Unwrap().ConfigureAwait(false);
                                response = JsonSerializer.Serialize(new { status = "completed", result }, JsonOptions);
                            }
                            catch (Exception ex)
                            {
                                response = JsonSerializer.Serialize(new { status = "failed", error = ex.ToString() }, JsonOptions);
                            }

                            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                            await pipe.WriteAsync(BitConverter.GetBytes(responseBytes.Length), cancellation.Token).ConfigureAwait(false);
                            await pipe.WriteAsync(responseBytes, cancellation.Token).ConfigureAwait(false);
                            await pipe.FlushAsync(cancellation.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    pipe.Dispose();
                    break;
                }
                catch
                {
                    pipe.Dispose();
                    if (!cancellation.IsCancellationRequested)
                    {
                        await System.Threading.Tasks.Task.Delay(250).ConfigureAwait(false);
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task<object> HandleAsync(JsonElement request)
        {
            string op = request.GetProperty("op").GetString();
            if (op == "status")
            {
                return new
                {
                    processId = Environment.ProcessId,
                    executable = Assembly.GetEntryAssembly().Location,
                    startedUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                    profile = Environment.GetEnvironmentVariable("MSBUILDSTRUCTUREDLOG_DATA_DIR")
                };
            }

            if (op == "state")
            {
                var build = window.DiagnosticBuild;
                var root = build?.Build;
                return new
                {
                    processId = Environment.ProcessId,
                    executable = Assembly.GetEntryAssembly().Location,
                    title = window.Title,
                    isLoaded = window.IsLoaded,
                    isVisible = window.IsVisible,
                    logFile = build?.LogFilePath,
                    nodeCount = root?.SearchIndex?.NodeCount,
                    selectedNode = build?.DiagnosticSelectedNode
                };
            }

            if (op == "open-binlog")
            {
                string path = Path.GetFullPath(request.GetProperty("path").GetString());
                return await OpenBinlogAsync(path);
            }

            if (op == "select-node")
            {
                string query = request.GetProperty("text").GetString();
                var build = window.DiagnosticBuild?.Build ?? throw new InvalidOperationException("No binlog is open.");
                var node = build.SearchIndex?.FindNodes(query, CancellationToken.None).FirstOrDefault()?.Node;
                if (node == null)
                {
                    throw new InvalidOperationException("No node matched: " + query);
                }

                window.DiagnosticBuild.SelectItem(node);
                await application.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                return new
                {
                    requested = node.ToString(),
                    selected = window.DiagnosticBuild.DiagnosticSelectedNode,
                    selectedType = window.DiagnosticBuild.DiagnosticSelectedItem?.GetType().Name,
                    type = node.GetType().Name
                };
            }

            if (op == "screenshot")
            {
                string path = Path.GetFullPath(request.GetProperty("path").GetString());
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var size = new System.Windows.Size(window.ActualWidth, window.ActualHeight);
                window.Measure(size);
                window.Arrange(new Rect(size));
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                using (var file = File.Create(path))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(file);
                }

                return new { path, width = bitmap.PixelWidth, height = bitmap.PixelHeight };
            }

            if (op == "shutdown")
            {
                DeleteSessionDescriptor();
                _ = application.Dispatcher.InvokeAsync(application.Shutdown);
                return new { stopping = true };
            }

            throw new InvalidOperationException("Unsupported operation: " + op);
        }

        private async System.Threading.Tasks.Task<object> OpenBinlogAsync(string path)
        {
            await window.OpenLogFileForDiagnostics(path);
            return new { opened = window.DiagnosticBuild?.LogFilePath, nodeCount = window.DiagnosticBuild?.Build?.SearchIndex?.NodeCount };
        }

        private static async System.Threading.Tasks.Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, token).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException("Client closed the diagnostic request before sending its complete frame.");
                }

                offset += read;
            }
        }

        public void Dispose()
        {
            cancellation.Cancel();
            listener?.Wait(TimeSpan.FromSeconds(2));
            DeleteSessionDescriptor();
        }

        private void DeleteSessionDescriptor()
        {
            if (File.Exists(sessionPath))
            {
                try
                {
                    var descriptor = JsonSerializer.Deserialize<SessionDescriptor>(File.ReadAllText(sessionPath), JsonOptions);
                    if (descriptor?.ProcessId == Process.GetCurrentProcess().Id && descriptor.PipeName == pipeName)
                    {
                        File.Delete(sessionPath);
                    }
                }
                catch
                {
                }
            }
        }

        private sealed class SessionDescriptor
        {
            public int Protocol { get; set; }
            public string PipeName { get; set; }
            public int ProcessId { get; set; }
            public string Executable { get; set; }
            public DateTime StartedUtc { get; set; }
            public string Profile { get; set; }
        }
    }
}
#endif
