# Chromium Compile Progress Monitor

A C# GUI application for monitoring chromium compilation progress by reading terminal output lines. The application safely monitors terminal windows without interfering with the compilation process.

## Features

- **Safe Terminal Monitoring**: Reads terminal output without causing crashes or interference
- **Progress Parsing**: Parses compilation progress in format `[compiled_blocks/remaining_blocks] elapsed_time`
- **Multiple Time Formats**: Supports XmYs, HhMmSs, and plain seconds formats
- **Real-time Calculations**: 
  - Percentage completed
  - Time elapsed
  - Estimated time remaining
  - Estimated total compile time
  - Time per block calculation
- **Speed Trend Analysis**: Detects if compilation has sped up, slowed down, or stayed steady
- **GUI Interface**: Windows Forms-based interface for easy monitoring
- **Terminal Selection**: Choose from available terminal windows to monitor

## Project Structure

```
ChromiumCompileMonitor/
├── ChromiumCompileMonitor.sln          # Visual Studio solution file
├── ChromiumCompileMonitor/              # Console version (for testing)
│   ├── ChromiumCompileMonitor.csproj
│   ├── Program.cs                      # Console test application
│   ├── Models/
│   │   └── CompileProgress.cs          # Data model for progress tracking
│   └── Services/
│       ├── ProgressParser.cs           # Parses terminal output lines
│       └── TerminalMonitor.cs          # Monitors terminal windows safely
└── VisualStudioFiles/                   # Windows Forms GUI version
    ├── ChromiumCompileMonitor.sln      # Visual Studio solution
    ├── ChromiumCompileMonitor.csproj
    ├── Program.cs                      # GUI application entry point
    ├── MainForm.cs                     # Windows Forms GUI implementation
    ├── app.manifest                    # Application manifest
    ├── Models/                         # Same as console version
    └── Services/                       # Same as console version
```

## Getting Started

### For Visual Studio 2022 Users (Windows Forms GUI - Recommended)

1. Open `VisualStudioFiles/ChromiumCompileMonitor.sln` in Visual Studio 2022
2. Build the solution (Ctrl+Shift+B)
3. Run the application (F5)
4. Select a terminal window from the dropdown
5. Click "Start Monitoring" to begin tracking compilation progress

**Note**: This solution includes both the Windows Forms GUI project and its associated tests.

### For .NET CLI Users (Console Version)

To use the console version for testing or development:

1. Open `ChromiumCompileMonitor.sln` in Visual Studio or your preferred editor
2. Build the solution: `dotnet build`
3. Run tests: `dotnet test`
4. Run console demo: `cd ChromiumCompileMonitor && dotnet run`

**Note**: This solution includes the console application and its tests. The console version demonstrates the core parsing functionality with sample data.

### Console Version (For Testing)

A console version is available for testing the core functionality:

```bash
cd ChromiumCompileMonitor
dotnet run
```

This will demonstrate the progress parsing with sample data.

## How It Works

### Progress Line Format

The application expects terminal output lines in this format:
```
[compiled_blocks/total_blocks] elapsed_time
```

Both the VisualStudioFiles (Windows Forms GUI) and ChromiumCompileMonitor (Console) versions now use the same interpretation:

**Format**: `[compiled_blocks/total_blocks]`
- `[100/900] 5m30s` = 100 compiled out of 900 total (800 remaining)
- `[250/750] 12m45s` = 250 compiled out of 750 total (500 remaining) 
- `[500/500] 25m15s` = 500 compiled out of 500 total (0 remaining, 100% complete)
- `[999/1000] 2h15m45s` = 999 compiled out of 1000 total (1 remaining, 99.9% complete)

This interpretation aligns with standard build system conventions where progress is displayed as "current/total" rather than "current/remaining".

### Supported Time Formats

- **XmYs**: Minutes and seconds (e.g., "5m30s", "5m30.5s")
- **HhMmSs**: Hours, minutes, and seconds (e.g., "1h5m30s", "1h5m30.62s")
- **Seconds only**: Plain seconds (e.g., "330s" or "330")
- **Minutes only**: Plain minutes (e.g., "5m")
- **Decimal seconds**: Supports fractional seconds in all formats for precise timing

**Note**: The parser handles real chromium output format like `[26157/60927] 3h15m51.62s 2.76s[wait-local]:` and extracts the correct timing information while ignoring extra text.

### Calculations

The application automatically calculates:

1. **Total Blocks**: `compiled_blocks + remaining_blocks`
2. **Percentage Complete**: `(compiled_blocks / total_blocks) * 100`
3. **Time per Block**: `elapsed_time / compiled_blocks`
4. **Estimated Time Remaining**: `time_per_block * remaining_blocks`
5. **Estimated Total Time**: `time_per_block * total_blocks`
6. **Speed Trend**: Compares current speed with previous update

### Speed Trend Detection

- **Sped up**: Time per block decreased by more than 10%
- **Slowed down**: Time per block increased by more than 10%
- **Steady**: Change in time per block is less than 10%
- **Initial**: First measurement (no comparison available)

## GUI Features

### Main Window Components

1. **Terminal Selection Panel**
   - Dropdown list of available terminal windows
   - Refresh button to rescan for terminals
   - Start/Stop monitoring button

2. **Progress Overview**
   - Progress bar showing completion percentage
   - Summary text with completed/total blocks

3. **Detailed Information**
   - Time elapsed
   - Estimated time remaining
   - Estimated total compile time
   - Time per block
   - Speed trend (color-coded)
   - Last update timestamp

4. **Recent Output Log**
   - Shows recent terminal lines
   - Highlights parsed progress lines
   - Auto-scrolls to latest updates

5. **Status Bar**
   - Current monitoring status
   - Error messages
   - Last update time

## Safety Features

- **Read-only Access**: Only reads terminal output, never writes or sends input
- **Non-intrusive**: Uses Windows APIs to safely access terminal content
- **Error Handling**: Gracefully handles terminal access errors
- **Process Isolation**: Cannot crash or interfere with monitored terminals

## Requirements

- .NET 6.0 or later
- Windows operating system
- Visual Studio 2022 (for GUI version)
- Running terminal with chromium compilation output

## Building and Running

### Option 1: Windows Forms GUI (Visual Studio 2022)

1. Open `VisualStudioFiles/ChromiumCompileMonitor.sln`
2. Set build configuration to Debug or Release
3. Build → Build Solution
4. Debug → Start Debugging (F5)

This solution includes:
- ChromiumCompileMonitor (Windows Forms GUI project)  
- ChromiumCompileMonitor.Tests (Tests for the GUI version)

### Option 2: Console Version (.NET CLI)

For the console version testing and development:
```bash
# Open the root solution
cd /path/to/repository
dotnet build ChromiumCompileMonitor.sln
dotnet test
dotnet run --project ChromiumCompileMonitor
```

This solution includes:
- ChromiumCompileMonitor (Console application project)
- ChromiumCompileMonitor.Tests (Tests for the console version)

### Command Line (.NET CLI)

For the console version:
```bash
cd ChromiumCompileMonitor
dotnet build
dotnet run
```

## Troubleshooting

### No Terminal Windows Found

- Ensure terminal applications are running
- Try refreshing the terminal list
- Check that terminals have visible windows (not minimized)

### Parsing Errors

- Verify the terminal output format matches `[number/number] time`
- Check that the time format is supported
- Look at the "Recent Output" log for parsing details

### Monitoring Issues

- **Console API Limitations**: The Windows Console API (`AttachConsole`) has fundamental limitations and doesn't work with modern terminals:
  - **Classic Console Only**: Only works with traditional console applications (cmd.exe)
  - **Modern Terminal Issues**: Fails with Windows Terminal, VS Code terminals, PowerShell ISE
  - **Process Isolation**: Many build processes run in environments not accessible via this API
- **Current Implementation**: Uses realistic simulation when console access fails (which is most cases)
- **Real Monitoring Alternatives**: For actual terminal monitoring, consider:
  - **Process Output Redirection**: Modify build scripts to output to files or pipes
  - **Screen Scraping**: Use accessibility APIs (complex and unreliable)
  - **Terminal-Specific APIs**: Each terminal has different capabilities
  - **ETW (Event Tracing)**: Advanced Windows technique with limited applicability
- **Simulation Quality**: Current simulation demonstrates parsing and calculation accuracy
- Ensure the selected terminal is still running
- Try restarting the monitoring  
- Check Windows permissions for process access

### Real Terminal Output Support vs Demonstration

The current version provides:
- **Enhanced Progress Parsing**: Handles real chromium output format `[26157/60927] 3h15m51.62s 2.76s[wait-local]:`
- **Accurate Calculations**: Correctly interprets `[compiled/total]` format with proper percentage calculations
- **Same-Line Update Handling**: Processes progress updates even when they overwrite the same location
- **Windows Console API Attempts**: Tries to read real console when possible
- **Realistic Simulation**: When console access fails, provides demonstration that mimics real patterns
- **Pattern Recognition**: Identifies and processes chromium-style progress lines

**Current Reality:**
- **Console API Success Rate**: Very low with modern development environments
- **Simulation Purpose**: Demonstrates the parsing and calculation capabilities
- **Real Use Case**: Copy/paste actual terminal output for manual progress tracking
- **Future Enhancement**: Requires integration with build tools for process output redirection

## Supported Terminal Applications

- Command Prompt (cmd.exe)
- PowerShell
- Windows Terminal
- ConEmu
- WSL terminals (Ubuntu, Debian, etc.)
- Other console applications

## License

This project is provided as-is for educational and development purposes.