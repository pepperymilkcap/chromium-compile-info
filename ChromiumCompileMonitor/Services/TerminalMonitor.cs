using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChromiumCompileMonitor.Services
{
    public class TerminalInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public IntPtr WindowHandle { get; set; }

        public override string ToString()
        {
            return $"{ProcessName} - {WindowTitle} (PID: {ProcessId})";
        }
    }

    public class TerminalMonitor
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public event Action<string>? NewLineReceived;

        private TerminalInfo? _monitoredTerminal;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly HashSet<string> _seenLines = new();

        public async Task<List<TerminalInfo>> GetAvailableTerminalsAsync()
        {
            return await Task.Run(() =>
            {
                var terminals = new List<TerminalInfo>();
                var windows = new List<(IntPtr handle, string title, uint processId)>();

                // Enumerate all visible windows
                EnumWindows((hWnd, lParam) =>
                {
                    if (!IsWindowVisible(hWnd))
                        return true;

                    var length = GetWindowTextLength(hWnd);
                    if (length == 0)
                        return true;

                    var builder = new StringBuilder(length + 1);
                    GetWindowText(hWnd, builder, builder.Capacity);
                    var title = builder.ToString();

                    if (string.IsNullOrWhiteSpace(title))
                        return true;

                    GetWindowThreadProcessId(hWnd, out var processId);
                    windows.Add((hWnd, title, processId));

                    return true;
                }, IntPtr.Zero);

                // Filter for terminal-like processes
                var terminalProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "cmd", "powershell", "WindowsTerminal", "wt", "ConEmu", "ConEmu64",
                    "mintty", "bash", "ubuntu", "kali", "debian", "opensuse"
                };

                foreach (var (handle, title, processId) in windows)
                {
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        var processName = process.ProcessName;

                        if (terminalProcessNames.Contains(processName) || 
                            title.Contains("Command Prompt", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("PowerShell", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("Terminal", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("WSL", StringComparison.OrdinalIgnoreCase))
                        {
                            terminals.Add(new TerminalInfo
                            {
                                ProcessId = (int)processId,
                                ProcessName = processName,
                                WindowTitle = title,
                                WindowHandle = handle
                            });
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore processes we can't access
                    }
                }

                return terminals.DistinctBy(t => t.ProcessId).ToList();
            });
        }

        public async Task StartMonitoringAsync(TerminalInfo terminal, CancellationToken cancellationToken = default)
        {
            _monitoredTerminal = terminal;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _seenLines.Clear();

            await Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Monitor the terminal process for output
                        // Note: This is a simplified approach. In a real implementation,
                        // you might need to use more sophisticated methods to capture
                        // terminal output without interfering with the process.
                        
                        await MonitorProcessOutputAsync(terminal.ProcessId);
                        await Task.Delay(1000, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        // Continue monitoring even if we encounter errors
                        await Task.Delay(2000, _cancellationTokenSource.Token);
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        private async Task MonitorProcessOutputAsync(int processId)
        {
            // This is a placeholder for actual terminal output monitoring
            // In a real implementation, you would need to use platform-specific APIs
            // to safely read terminal output without interfering with the process
            
            // For demonstration purposes, we'll simulate receiving lines
            // In practice, you might use:
            // - Windows Console API to read from console buffers
            // - ETW (Event Tracing for Windows) to monitor console events
            // - Accessibility APIs to read terminal content
            // - Or hook into the terminal's scrollback buffer
            
            await Task.Delay(100);
            
            // Simulate periodic progress updates for testing
            if (DateTime.Now.Second % 5 == 0)
            {
                var random = new Random();
                var compiled = random.Next(1, 1000);
                var remaining = random.Next(100, 2000);
                var elapsed = $"{random.Next(1, 60)}m{random.Next(0, 60)}s";
                
                var simulatedLine = $"[{compiled}/{remaining}] {elapsed}";
                ProcessNewLine(simulatedLine);
            }
        }

        private void ProcessNewLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || _seenLines.Contains(line))
                return;

            _seenLines.Add(line);
            
            // Keep only the last 1000 lines to prevent memory issues
            if (_seenLines.Count > 1000)
            {
                var oldest = _seenLines.First();
                _seenLines.Remove(oldest);
            }

            NewLineReceived?.Invoke(line);
        }

        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _monitoredTerminal = null;
            _seenLines.Clear();
        }

        public bool IsMonitoring => _monitoredTerminal != null && 
                                   _cancellationTokenSource != null && 
                                   !_cancellationTokenSource.Token.IsCancellationRequested;
    }
}