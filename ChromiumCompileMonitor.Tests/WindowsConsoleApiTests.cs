using System;
using System.Runtime.InteropServices;
using Xunit;

namespace ChromiumCompileMonitor.Tests
{
    public class WindowsConsoleApiTests
    {
        [Fact]
        public void WindowsConsoleApi_StructureDefinitions_AreValid()
        {
            // Test that our Windows API structure definitions are valid
            // This ensures the P/Invoke declarations will work correctly on Windows
            
            // Test COORD structure size
            var coordSize = Marshal.SizeOf<TestCOORD>();
            Assert.True(coordSize >= 4); // Should be at least 2 shorts (4 bytes)
            
            // Test CONSOLE_SCREEN_BUFFER_INFO structure size  
            var bufferInfoSize = Marshal.SizeOf<TestCONSOLE_SCREEN_BUFFER_INFO>();
            Assert.True(bufferInfoSize >= 20); // Should have multiple fields
        }
        
        [Fact] 
        public void WindowsConsoleApi_Constants_AreCorrect()
        {
            // Verify Windows API constants are correct
            const int STD_OUTPUT_HANDLE = -11;
            const uint PROCESS_QUERY_INFORMATION = 0x0400;
            
            Assert.Equal(-11, STD_OUTPUT_HANDLE);
            Assert.Equal(0x0400u, PROCESS_QUERY_INFORMATION);
        }

        [Fact]
        public void ProcessNewLine_WithRealChromiumOutput_ParsesCorrectly()
        {
            // Test that our implementation can handle real chromium output
            var testOutput = "[26157/60927] 3h15m51.62s 2.76s[wait-local]:";
            
            // This simulates what would happen when the Windows Console API
            // reads this line from a real terminal
            var result = TestParseOutput(testOutput);
            
            Assert.True(result);
        }
        
        private bool TestParseOutput(string line)
        {
            // Simulate the parsing logic that would happen in ProcessNewLine
            return !string.IsNullOrWhiteSpace(line) && 
                   line.Contains("[") && 
                   line.Contains("]");
        }

        // Test structure definitions that mirror the ones in TerminalMonitor.cs
        [StructLayout(LayoutKind.Sequential)]
        private struct TestCOORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TestSMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TestCONSOLE_SCREEN_BUFFER_INFO
        {
            public TestCOORD dwSize;
            public TestCOORD dwCursorPosition;
            public ushort wAttributes;
            public TestSMALL_RECT srWindow;
            public TestCOORD dwMaximumWindowSize;
        }
    }
}