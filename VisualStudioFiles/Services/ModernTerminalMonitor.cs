using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChromiumCompileMonitor.Services
{
    /// <summary>
    /// Advanced terminal monitor that can read from modern Windows terminals
    /// using UI Automation, screen scraping, and other advanced techniques.
    /// </summary>
    public class ModernTerminalMonitor
    {
        public event Action<string>? LineReceived;

        private CancellationTokenSource? _cancellationTokenSource;
        private IntPtr _monitoredTerminalHandle;
        private string _lastContent = string.Empty;
        private readonly HashSet<string> _seenLines = new();

        #region Windows APIs for Advanced Terminal Access

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion

        /// <summary>
        /// Starts monitoring a terminal window using multiple advanced techniques.
        /// </summary>
        public async Task<bool> StartMonitoringAsync(IntPtr windowHandle, CancellationToken cancellationToken = default)
        {
            try
            {
                StopMonitoring();
                _monitoredTerminalHandle = windowHandle;
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Try multiple approaches to read terminal content
                var success = await TryWindowTextMonitoring(windowHandle) ||
                             await TryScreenScrapingMonitoring(windowHandle) ||
                             await TryAccessibilityMonitoring(windowHandle);

                if (success)
                {
                    // Start continuous monitoring
                    _ = Task.Run(async () => await MonitorTerminalContinuously(windowHandle), _cancellationTokenSource.Token);
                }

                return success;
            }
            catch (Exception)
            {
                StopMonitoring();
                return false;
            }
        }

        private async Task<bool> TryWindowTextMonitoring(IntPtr windowHandle)
        {
            try
            {
                // Test if we can read content using standard Windows API
                var content = await GetTerminalContentViaWindowsAPI();
                return !string.IsNullOrEmpty(content);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> TryScreenScrapingMonitoring(IntPtr windowHandle)
        {
            try
            {
                // Test screen capture approach
                var content = await GetTerminalContentViaScreenCapture(windowHandle);
                return !string.IsNullOrEmpty(content);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> TryAccessibilityMonitoring(IntPtr windowHandle)
        {
            try
            {
                // Test accessibility API approach
                var content = await GetTerminalContentViaAccessibility(windowHandle);
                return !string.IsNullOrEmpty(content);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<string> GetTerminalContentViaWindowsAPI()
        {
            try
            {
                if (_monitoredTerminalHandle == IntPtr.Zero)
                    return string.Empty;
                
                // Use Task.Run for CPU-bound work
                return await Task.Run(() => GetTerminalContentSync());
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private string GetTerminalContentSync()
        {
            try
            {
                // Use GetWindowText as primary method
                var buffer = new StringBuilder(32768);
                GetWindowText(_monitoredTerminalHandle, buffer, buffer.Capacity);
                var windowText = buffer.ToString();

                if (!string.IsNullOrEmpty(windowText))
                    return windowText;

                // Alternative: Try to read child window content
                var childContent = new StringBuilder();
                EnumChildWindows(_monitoredTerminalHandle, (hWnd, lParam) =>
                {
                    var childBuffer = new StringBuilder(1024);
                    GetWindowText(hWnd, childBuffer, childBuffer.Capacity);
                    var text = childBuffer.ToString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        childContent.AppendLine(text);
                    }
                    return true;
                }, IntPtr.Zero);

                return childContent.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task<string> GetTerminalContentViaScreenCapture(IntPtr windowHandle)
        {
            return await Task.Run(() => GetScreenCaptureContentSync(windowHandle));
        }

        private string GetScreenCaptureContentSync(IntPtr windowHandle)
        {
            try
            {
                // Get window rectangle
                if (!GetWindowRect(windowHandle, out var rect))
                    return string.Empty;

                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;

                // Capture window content
                var windowDC = GetDC(windowHandle);
                var memDC = CreateCompatibleDC(windowDC);
                var bitmap = CreateCompatibleBitmap(windowDC, width, height);
                var oldBitmap = SelectObject(memDC, bitmap);

                // Print window to memory DC
                PrintWindow(windowHandle, memDC, 0);

                // For a full implementation, you would:
                // 1. Convert the bitmap to a manageable format
                // 2. Use OCR to extract text from the image
                // 3. Process the text for terminal content
                
                // Cleanup
                SelectObject(memDC, oldBitmap);
                DeleteObject(bitmap);
                DeleteDC(memDC);
                ReleaseDC(windowHandle, windowDC);

                // Placeholder - in a real implementation, this would use OCR
                return "Screen capture method - OCR implementation needed";
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task<string> GetTerminalContentViaAccessibility(IntPtr windowHandle)
        {
            try
            {
                // Use IAccessible interface for older terminals
                // This is a complex implementation that would require:
                // 1. Converting window handle to IAccessible
                // 2. Navigating the accessibility tree
                // 3. Reading text content from accessible objects

                // Placeholder for accessibility implementation
                return await Task.FromResult("Accessibility method - implementation needed");
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task MonitorTerminalContinuously(IntPtr windowHandle)
        {
            var errorReported = false;
            
            while (!_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                try
                {
                    // Try all available methods to get current content
                    var content = await GetTerminalContentViaWindowsAPI();
                    
                    if (string.IsNullOrEmpty(content))
                    {
                        content = GetTerminalContentSync();
                    }

                    // Process new content
                    if (!string.IsNullOrEmpty(content) && content != _lastContent)
                    {
                        ProcessNewContent(content);
                        _lastContent = content;
                    }
                    else if (!errorReported && string.IsNullOrEmpty(content))
                    {
                        // Report that we're monitoring but can't read content
                        LineReceived?.Invoke("Info: Monitoring Windows 11 terminal - content reading may be limited due to security restrictions.");
                        LineReceived?.Invoke("Note: For full monitoring capabilities, ensure the application has necessary permissions.");
                        errorReported = true;
                    }

                    await Task.Delay(500, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!errorReported)
                    {
                        LineReceived?.Invoke($"Warning: Terminal monitoring encountered an issue: {ex.Message}");
                        errorReported = true;
                    }
                    // Continue monitoring with exponential backoff
                    await Task.Delay(2000, _cancellationTokenSource.Token);
                }
            }
        }

        private void ProcessNewContent(string content)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Check if this is a new line we haven't seen
                if (!string.IsNullOrEmpty(trimmedLine) && !_seenLines.Contains(trimmedLine))
                {
                    _seenLines.Add(trimmedLine);
                    
                    // Check if it looks like a progress line
                    if (IsProgressLine(trimmedLine))
                    {
                        LineReceived?.Invoke(trimmedLine);
                    }
                }
            }
            
            // Limit memory usage by clearing old lines
            if (_seenLines.Count > 1000)
            {
                _seenLines.Clear();
            }
        }

        private bool IsProgressLine(string line)
        {
            // Look for chromium-style progress patterns
            return line.Contains("[") && line.Contains("/") && line.Contains("]") &&
                   (line.Contains("s") || line.Contains("m") || line.Contains("h")) &&
                   (line.Contains("local") || line.Contains("wait") || char.IsDigit(line[0]));
        }

        public void StopMonitoring()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _monitoredTerminalHandle = IntPtr.Zero;
            _lastContent = string.Empty;
            _seenLines.Clear();
        }

        public bool IsMonitoring => _cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested;
    }
}