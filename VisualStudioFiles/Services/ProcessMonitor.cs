using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChromiumCompileMonitor.Services
{
    /// <summary>
    /// Monitors build processes by launching them directly and capturing their output.
    /// This provides real-time access to actual build output, unlike terminal monitoring approaches.
    /// </summary>
    public class ProcessMonitor
    {
        public event Action<string>? OutputLineReceived;
        public event Action<string>? ErrorLineReceived;
        public event Action<int>? ProcessExited;

        private Process? _monitoredProcess;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly StringBuilder _outputBuffer = new();
        private readonly StringBuilder _errorBuffer = new();

        /// <summary>
        /// Starts monitoring a build process by launching it directly.
        /// This captures stdout and stderr in real-time.
        /// </summary>
        /// <param name="executable">Path to the executable (e.g., "ninja", "make", "autoninja")</param>
        /// <param name="arguments">Command line arguments</param>
        /// <param name="workingDirectory">Working directory for the process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<bool> StartProcessMonitoringAsync(
            string executable, 
            string arguments = "", 
            string workingDirectory = "", 
            CancellationToken cancellationToken = default)
        {
            try
            {
                StopMonitoring();

                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                _monitoredProcess = new Process { StartInfo = processStartInfo };

                // Set up real-time output monitoring
                _monitoredProcess.OutputDataReceived += OnOutputDataReceived;
                _monitoredProcess.ErrorDataReceived += OnErrorDataReceived;
                _monitoredProcess.Exited += OnProcessExited;
                _monitoredProcess.EnableRaisingEvents = true;

                // Start the process
                if (!_monitoredProcess.Start())
                {
                    return false;
                }

                // Begin asynchronous reading of output and error streams
                _monitoredProcess.BeginOutputReadLine();
                _monitoredProcess.BeginErrorReadLine();

                return true;
            }
            catch (Exception)
            {
                StopMonitoring();
                return false;
            }
        }

        /// <summary>
        /// Monitors an existing process by periodically reading from a log file or output file.
        /// Useful when the build process outputs progress to a file.
        /// </summary>
        /// <param name="logFilePath">Path to the log file to monitor</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task StartFileMonitoringAsync(string logFilePath, CancellationToken cancellationToken = default)
        {
            try
            {
                StopMonitoring();

                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                await Task.Run(async () =>
                {
                    var lastPosition = 0L;
                    var lastWriteTime = DateTime.MinValue;

                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            if (File.Exists(logFilePath))
                            {
                                var fileInfo = new FileInfo(logFilePath);
                                
                                // Check if file has been modified
                                if (fileInfo.LastWriteTime > lastWriteTime)
                                {
                                    lastWriteTime = fileInfo.LastWriteTime;
                                    
                                    // Read new content from the file
                                    using var fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                    
                                    if (fileStream.Length > lastPosition)
                                    {
                                        fileStream.Seek(lastPosition, SeekOrigin.Begin);
                                        
                                        using var reader = new StreamReader(fileStream, Encoding.UTF8);
                                        string? line;
                                        while ((line = await reader.ReadLineAsync()) != null)
                                        {
                                            OutputLineReceived?.Invoke(line);
                                        }
                                        
                                        lastPosition = fileStream.Position;
                                    }
                                }
                            }

                            await Task.Delay(500, _cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception)
                        {
                            // Continue monitoring even if we encounter errors
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                    }
                }, _cancellationTokenSource.Token);
            }
            catch (Exception)
            {
                StopMonitoring();
            }
        }

        /// <summary>
        /// Monitors a named pipe for output. Useful when the build process is configured to output to a pipe.
        /// </summary>
        /// <param name="pipeName">Name of the named pipe</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task StartPipeMonitoringAsync(string pipeName, CancellationToken cancellationToken = default)
        {
            try
            {
                StopMonitoring();

                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                await Task.Run(async () =>
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            using var pipeStream = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.In);
                            
                            await pipeStream.ConnectAsync(5000, _cancellationTokenSource.Token);
                            
                            using var reader = new StreamReader(pipeStream, Encoding.UTF8);
                            
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null && !_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                OutputLineReceived?.Invoke(line);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception)
                        {
                            // Retry connection after delay
                            await Task.Delay(2000, _cancellationTokenSource.Token);
                        }
                    }
                }, _cancellationTokenSource.Token);
            }
            catch (Exception)
            {
                StopMonitoring();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _outputBuffer.AppendLine(e.Data);
                OutputLineReceived?.Invoke(e.Data);
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _errorBuffer.AppendLine(e.Data);
                ErrorLineReceived?.Invoke(e.Data);
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            if (_monitoredProcess != null)
            {
                ProcessExited?.Invoke(_monitoredProcess.ExitCode);
            }
        }

        public void StopMonitoring()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_monitoredProcess != null && !_monitoredProcess.HasExited)
                {
                    _monitoredProcess.Kill();
                }
                
                _monitoredProcess?.Dispose();
                _monitoredProcess = null;
                
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                _outputBuffer.Clear();
                _errorBuffer.Clear();
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }

        public bool IsMonitoring => _monitoredProcess != null && !_monitoredProcess.HasExited;

        public string GetCapturedOutput() => _outputBuffer.ToString();
        public string GetCapturedError() => _errorBuffer.ToString();
    }
}