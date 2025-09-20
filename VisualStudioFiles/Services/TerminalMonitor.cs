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
        #region Windows API Declarations (for fallback compatibility)

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadConsoleOutputCharacter(
            IntPtr hConsoleOutput,
            [Out] StringBuilder lpCharacter,
            uint nLength,
            COORD dwReadCoord,
            out uint lpNumberOfCharsRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(
            IntPtr hConsoleOutput,
            out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public ushort wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;
        }

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        public event Action<string>? NewLineReceived;

        private TerminalInfo? _monitoredTerminal;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly HashSet<string> _seenLines = new();
        private readonly List<string> _lastConsoleContent = new();
        private readonly ModernTerminalMonitor _modernTerminalMonitor = new();

        public TerminalMonitor()
        {
            // Set up modern terminal monitor event handlers
            _modernTerminalMonitor.LineReceived += OnModernTerminalLineReceived;
        }

        /// <summary>
        /// Event handler for terminal lines received from modern terminal monitoring.
        /// </summary>
        private void OnModernTerminalLineReceived(string line)
        {
            // Process lines from modern terminal monitoring
            if (IsProgressLine(line))
            {
                ProcessUpdatedLine(line);
            }
            
            // Forward all lines to listeners
            NewLineReceived?.Invoke(line);
        }

        /// <summary>
        /// Gets available terminal windows for monitoring existing Windows 11 terminals.
        /// Only returns actual existing terminal processes - does not include options to launch new processes.
        /// </summary>
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

                // Filter for terminal-like processes - enhanced for Windows 11
                var terminalProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // Windows 11 / Windows Terminal
                    "WindowsTerminal", "wt", "WindowsTerminal.exe",
                    // Traditional terminals
                    "cmd", "powershell", "pwsh", "powershell_ise",
                    // Third-party terminals
                    "ConEmu", "ConEmu64", "mintty", "alacritty", "hyper",
                    // WSL and Linux terminals
                    "bash", "ubuntu", "kali", "debian", "opensuse", "wsl"
                };

                foreach (var (handle, title, processId) in windows)
                {
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        var processName = process.ProcessName;

                        // Enhanced detection for Windows 11 terminals
                        var isTerminalProcess = terminalProcessNames.Contains(processName) || 
                            title.Contains("Command Prompt", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("PowerShell", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("Terminal", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("WSL", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("Windows Subsystem for Linux", StringComparison.OrdinalIgnoreCase);

                        if (isTerminalProcess)
                        {
                            // Specifically identify Windows 11 Terminal and modern terminals
                            var isWindows11Terminal = processName.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase) ||
                                                     processName.Equals("wt", StringComparison.OrdinalIgnoreCase) ||
                                                     title.Contains("Windows Terminal", StringComparison.OrdinalIgnoreCase);
                            
                            var isModernTerminal = isWindows11Terminal ||
                                                 title.Contains("VS Code", StringComparison.OrdinalIgnoreCase) ||
                                                 processName.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
                                                 processName.Equals("alacritty", StringComparison.OrdinalIgnoreCase);
                            
                            var displayTitle = isWindows11Terminal 
                                ? title + " (Windows 11 Terminal - Enhanced Monitoring)" 
                                : isModernTerminal 
                                    ? title + " (Modern Terminal - Enhanced Monitoring)"
                                    : title + " (Legacy Terminal - Basic Monitoring)";
                            
                            terminals.Add(new TerminalInfo
                            {
                                ProcessId = (int)processId,
                                ProcessName = processName,
                                WindowTitle = displayTitle,
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



        /// <summary>
        /// Starts monitoring an existing terminal window using advanced techniques for modern Windows 11 terminals.
        /// This only monitors existing terminal processes and does not launch new ones.
        /// </summary>
        /// <param name="terminal">Terminal information for existing terminal window</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task StartMonitoringAsync(TerminalInfo terminal, CancellationToken cancellationToken = default)
        {
            // Only monitor existing terminal windows - no process launching
            if (terminal.WindowHandle == IntPtr.Zero || terminal.ProcessId <= 0)
            {
                throw new ArgumentException("Invalid terminal: WindowHandle and ProcessId must be valid for existing terminals");
            }

            StopMonitoring();
            _monitoredTerminal = terminal;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _seenLines.Clear();
            _lastConsoleContent.Clear();

            // Determine if this is a Windows 11 / modern terminal
            var isWindows11Terminal = terminal.ProcessName.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase) ||
                                     terminal.ProcessName.Equals("wt", StringComparison.OrdinalIgnoreCase) ||
                                     terminal.WindowTitle.Contains("Windows Terminal", StringComparison.OrdinalIgnoreCase);

            var isModernTerminal = isWindows11Terminal ||
                                 terminal.WindowTitle.Contains("VS Code", StringComparison.OrdinalIgnoreCase) ||
                                 terminal.ProcessName.Equals("pwsh", StringComparison.OrdinalIgnoreCase);

            // For Windows 11 and modern terminals, try enhanced monitoring first
            if (isModernTerminal)
            {
                var modernSuccess = await _modernTerminalMonitor.StartMonitoringAsync(terminal.WindowHandle, cancellationToken);
                
                if (modernSuccess)
                {
                    // Modern terminal monitoring is active - ModernTerminalMonitor will handle events
                    return;
                }
                
                // If modern monitoring fails, notify but don't fall back to simulation
                // Modern terminals often require elevated permissions or specific configurations
                NewLineReceived?.Invoke($"Warning: Enhanced monitoring failed for {terminal.ProcessName}. Terminal content may not be visible.");
                NewLineReceived?.Invoke("Note: Windows 11 Terminal monitoring may require running the application as administrator.");
            }

            // For legacy terminals or as fallback, attempt basic console monitoring
            // This will NOT use simulation - only real console data
            await Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Basic monitoring approach - attempts to read real terminal content
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
            await Task.Run(() =>
            {
                try
                {
                    // For Windows 11 terminals, the traditional console API approach has limitations.
                    // Modern terminals like Windows Terminal use different architectures.
                    // We attempt basic console reading but rely primarily on ModernTerminalMonitor.
                    
                    var consoleContent = ReadConsoleOutput(processId);
                    if (consoleContent != null && consoleContent.Count > 0)
                    {
                        ProcessConsoleContent(consoleContent);
                        return; // Successfully read from console
                    }
                    
                    // If console reading fails, we don't simulate.
                    // The monitoring depends on ModernTerminalMonitor for Windows 11 terminals.
                    // This ensures we only show actual terminal output, not simulated data.
                }
                catch (Exception)
                {
                    // Console API failed - this is expected for modern Windows 11 terminals
                    // The ModernTerminalMonitor should handle the actual monitoring
                }
            });
        }
        }



        private void ProcessUpdatedLine(string line)
        {
            // Handle line updates (when the same line is overwritten)
            // This is crucial for chromium compilation which updates the same line
            if (!string.IsNullOrWhiteSpace(line))
            {
                // Always process the line, even if we've seen it before
                // because chromium updates the same line with new progress
                NewLineReceived?.Invoke(line);
                
                // Update our tracking but don't rely on "new lines" detection
                // since chromium overwrites the same line
                if (!_seenLines.Contains(line))
                {
                    _seenLines.Add(line);
                    
                    // Keep only the last 100 unique lines to prevent memory issues
                    if (_seenLines.Count > 100)
                    {
                        var oldest = _seenLines.First();
                        _seenLines.Remove(oldest);
                    }
                }
            }
        }

        private List<string>? ReadConsoleOutput(int processId)
        {
            var content = new List<string>();
            bool attachedToConsole = false;
            
            try
            {
                // Try to attach to the target process console
                // Note: AttachConsole may fail for various reasons:
                // - Process doesn't have a console
                // - Process is not a console application  
                // - Security restrictions
                // - Process is already attached to another console
                if (AttachConsole((uint)processId))
                {
                    attachedToConsole = true;
                    
                    // Get console output handle
                    var consoleHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                    if (consoleHandle == IntPtr.Zero || consoleHandle == new IntPtr(-1))
                        return null;

                    // Get console screen buffer info
                    if (!GetConsoleScreenBufferInfo(consoleHandle, out var bufferInfo))
                        return null;

                    // Read console content line by line
                    var bufferWidth = bufferInfo.dwSize.X;
                    var bufferHeight = bufferInfo.srWindow.Bottom - bufferInfo.srWindow.Top + 1;
                    
                    // Focus on the last few lines where progress updates typically appear
                    // This is more efficient and catches the most recent updates
                    var startLine = (short)Math.Max(bufferInfo.srWindow.Top, bufferInfo.srWindow.Bottom - 10);
                    
                    for (short y = startLine; y <= bufferInfo.srWindow.Bottom; y++)
                    {
                        var lineBuffer = new StringBuilder(bufferWidth);
                        var coord = new COORD { X = 0, Y = y };
                        
                        if (ReadConsoleOutputCharacter(consoleHandle, lineBuffer, (uint)bufferWidth, coord, out var charsRead))
                        {
                            var line = lineBuffer.ToString().TrimEnd();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                content.Add(line);
                            }
                        }
                    }
                }
                else
                {
                    // AttachConsole failed - this is common and expected
                    // Many terminal applications don't support AttachConsole from external processes
                    return null;
                }
                
                return content;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (attachedToConsole)
                {
                    try
                    {
                        FreeConsole();
                    }
                    catch (Exception)
                    {
                        // Ignore errors when freeing console
                    }
                }
            }
        }

        private void ProcessConsoleContent(List<string> consoleContent)
        {
            // Handle both new lines AND line updates (critical for chromium compilation)
            // Chromium often updates the same line rather than creating new lines
            
            foreach (var line in consoleContent)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    // Check if this line contains progress information
                    if (IsProgressLine(line))
                    {
                        // Always process progress lines, even if we've seen similar content
                        // because chromium updates the same line with new progress values
                        ProcessUpdatedLine(line);
                    }
                }
            }
            
            // Update last content for next comparison, but don't rely solely on "new lines"
            _lastConsoleContent.Clear();
            _lastConsoleContent.AddRange(consoleContent);
        }

        private bool IsProgressLine(string line)
        {
            // Check if the line contains chromium-style progress information
            // Pattern: [number/number] time... 
            return line.Contains("[") && 
                   line.Contains("/") && 
                   line.Contains("]") &&
                   (line.Contains("s") || line.Contains("m") || line.Contains("h"));
        }

        private void GenerateSimulatedData()
        {
            // This method is kept for backward compatibility
            // It delegates to the more realistic simulation
            GenerateRealisticSimulation();
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
            
            _modernTerminalMonitor.StopMonitoring();
            
            _monitoredTerminal = null;
            _seenLines.Clear();
            _lastConsoleContent.Clear();
        }

        public bool IsMonitoring => (_monitoredTerminal != null && 
                                   _cancellationTokenSource != null && 
                                   !_cancellationTokenSource.Token.IsCancellationRequested) ||
                                   _modernTerminalMonitor.IsMonitoring;
    }
}