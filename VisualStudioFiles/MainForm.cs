using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ChromiumCompileMonitor.Models;
using ChromiumCompileMonitor.Services;

namespace ChromiumCompileMonitor
{
    public partial class MainForm : Form
    {
        private readonly TerminalMonitor _terminalMonitor;
        private readonly ProgressParser _progressParser;
        private List<TerminalInfo> _availableTerminals = new();
        private TerminalInfo? _selectedTerminal;
        private CompileProgress? _currentProgress;
        private readonly StringBuilder _logOutput = new();
        private CancellationTokenSource? _monitoringCancellationTokenSource;

        // UI Controls
        private ComboBox _terminalComboBox = null!;
        private Button _refreshButton = null!;
        private Button _startStopButton = null!;
        private ProgressBar _progressBar = null!;
        private Label _progressLabel = null!;
        private Label _elapsedTimeLabel = null!;
        private Label _estimatedRemainingLabel = null!;
        private Label _estimatedTotalLabel = null!;
        private Label _timePerBlockLabel = null!;
        private Label _speedTrendLabel = null!;
        private Label _lastUpdateLabel = null!;
        private TextBox _logTextBox = null!;
        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _statusLabel = null!;
        private System.Windows.Forms.Timer _updateTimer = null!;

        public MainForm()
        {
            _terminalMonitor = new TerminalMonitor();
            _progressParser = new ProgressParser();
            
            _terminalMonitor.NewLineReceived += OnNewLineReceived;
            
            InitializeComponent();
            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;

            // Initialize update timer for UI refreshes
            _updateTimer = new System.Windows.Forms.Timer();
            _updateTimer.Interval = 100; // 100ms refresh rate
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void InitializeComponent()
        {
            Text = "Chromium Compile Progress Monitor";
            Size = new Size(800, 600);
            MinimumSize = new Size(600, 500);
            StartPosition = FormStartPosition.CenterScreen;

            // Create main layout
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };

            // Set up row styles with adequate heights to prevent text cutoff
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));  // Terminal selection - increased height for dropdown text
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));  // Progress overview - increased height for progress text
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Detailed info
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));   // Status bar - increased height for status text

            // Terminal Selection Group
            var terminalGroup = CreateTerminalSelectionGroup();
            mainPanel.Controls.Add(terminalGroup, 0, 0);

            // Progress Overview Group
            var progressGroup = CreateProgressOverviewGroup();
            mainPanel.Controls.Add(progressGroup, 0, 1);

            // Detailed Information Group
            var detailsGroup = CreateDetailedInfoGroup();
            mainPanel.Controls.Add(detailsGroup, 0, 2);

            // Status Strip
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Ready - Select a terminal to monitor");
            _statusStrip.Items.Add(_statusLabel);
            mainPanel.Controls.Add(_statusStrip, 0, 3);

            Controls.Add(mainPanel);
        }

        private GroupBox CreateTerminalSelectionGroup()
        {
            var group = new GroupBox
            {
                Text = "Terminal Selection",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

            _terminalComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(5),
                Font = new Font(Font.FontFamily, 9, FontStyle.Regular) // Ensure readable font size
            };
            _terminalComboBox.SelectedIndexChanged += TerminalComboBox_SelectedIndexChanged;

            _refreshButton = new Button
            {
                Text = "Refresh",
                Dock = DockStyle.Fill,
                Margin = new Padding(5)
            };
            _refreshButton.Click += RefreshButton_Click;

            _startStopButton = new Button
            {
                Text = "Start Monitoring",
                Dock = DockStyle.Fill,
                Margin = new Padding(5)
            };
            _startStopButton.Click += StartStopButton_Click;

            panel.Controls.Add(_terminalComboBox, 0, 0);
            panel.Controls.Add(_refreshButton, 1, 0);
            panel.Controls.Add(_startStopButton, 2, 0);

            group.Controls.Add(panel);
            return group;
        }

        private GroupBox CreateProgressOverviewGroup()
        {
            var group = new GroupBox
            {
                Text = "Compile Progress",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(3)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Increased height for progress bar
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(5),
                Maximum = 100
            };

            _progressLabel = new Label
            {
                Text = "0.0% Complete (0/0 blocks)",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(5, 3, 5, 3) // Add proper margins
            };

            panel.Controls.Add(_progressBar, 0, 0);
            panel.Controls.Add(_progressLabel, 0, 1);

            group.Controls.Add(panel);
            return group;
        }

        private GroupBox CreateDetailedInfoGroup()
        {
            var group = new GroupBox
            {
                Text = "Detailed Information",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); // Increased height for info panel to prevent text cutoff
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Info panel
            var infoPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Padding = new Padding(8)
            };
            infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); // Increased width
            infoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            
            // Add row styles for proper spacing and prevent text cutoff
            for (int i = 0; i < 6; i++)
            {
                infoPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Increased row height to prevent cutoff
            }

            var labelFont = new Font(Font, FontStyle.Bold);

            // Time Information with improved spacing
            infoPanel.Controls.Add(new Label { Text = "Time Elapsed:", Font = labelFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) }, 0, 0);
            _elapsedTimeLabel = new Label { Text = "00:00:00", TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) };
            infoPanel.Controls.Add(_elapsedTimeLabel, 1, 0);

            infoPanel.Controls.Add(new Label { Text = "Estimated Time Remaining:", Font = labelFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) }, 0, 1);
            _estimatedRemainingLabel = new Label { Text = "00:00:00", TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) };
            infoPanel.Controls.Add(_estimatedRemainingLabel, 1, 1);

            infoPanel.Controls.Add(new Label { Text = "Estimated Total Time:", Font = labelFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) }, 0, 2);
            _estimatedTotalLabel = new Label { Text = "00:00:00", TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) };
            infoPanel.Controls.Add(_estimatedTotalLabel, 1, 2);

            infoPanel.Controls.Add(new Label { Text = "Time per Block:", Font = labelFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) }, 0, 3);
            _timePerBlockLabel = new Label { Text = "0.00 seconds", TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) };
            infoPanel.Controls.Add(_timePerBlockLabel, 1, 3);

            infoPanel.Controls.Add(new Label { Text = "Speed Trend:", Font = labelFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) }, 0, 4);
            _speedTrendLabel = new Label { Text = "Unknown", TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) };
            infoPanel.Controls.Add(_speedTrendLabel, 1, 4);

            infoPanel.Controls.Add(new Label { Text = "Last Update:", Font = labelFont, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) }, 0, 5);
            _lastUpdateLabel = new Label { Text = "--:--:--", TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 0, 4) };
            infoPanel.Controls.Add(_lastUpdateLabel, 1, 5);

            // Log output
            var logPanel = new Panel { Dock = DockStyle.Fill };
            var logLabel = new Label
            {
                Text = "Recent Output:",
                Font = labelFont,
                Height = 25, // Increased height to prevent cutoff
                Dock = DockStyle.Top
            };

            _logTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9),
                Dock = DockStyle.Fill
            };

            logPanel.Controls.Add(_logTextBox);
            logPanel.Controls.Add(logLabel);

            mainPanel.Controls.Add(infoPanel, 0, 0);
            mainPanel.Controls.Add(logPanel, 0, 1);

            group.Controls.Add(mainPanel);
            return group;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                // Ensure UI is fully initialized before starting async operations
                await Task.Delay(100); // Short delay to ensure UI components are ready
                await RefreshTerminals();
            }
            catch (Exception ex)
            {
                if (_statusLabel != null) _statusLabel.Text = $"Error during startup: {ex.Message}";
                MessageBox.Show($"Error during application startup: {ex.Message}", "Startup Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _updateTimer?.Stop();
                _updateTimer?.Dispose();
                StopMonitoring();
            }
            catch (Exception ex)
            {
                // Log error but don't prevent form closing
                System.Diagnostics.Debug.WriteLine($"Error during form closing: {ex.Message}");
            }
        }

        private void TerminalComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                _selectedTerminal = _terminalComboBox.SelectedItem as TerminalInfo;
                UpdateStartStopButtonState();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error selecting terminal: {ex.Message}";
            }
        }

        private async void RefreshButton_Click(object sender, EventArgs e)
        {
            try
            {
                await RefreshTerminals();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error refreshing terminals: {ex.Message}";
                MessageBox.Show($"Error refreshing terminal list: {ex.Message}", "Refresh Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void StartStopButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_terminalMonitor.IsMonitoring)
                {
                    StopMonitoring();
                }
                else
                {
                    await StartMonitoring();
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error toggling monitoring: {ex.Message}";
                MessageBox.Show($"Error starting/stopping monitoring: {ex.Message}", "Monitoring Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private async Task RefreshTerminals()
        {
            try
            {
                if (_statusLabel != null) _statusLabel.Text = "Scanning for terminal windows...";
                if (_refreshButton != null) _refreshButton.Enabled = false;
                
                var terminals = await _terminalMonitor.GetAvailableTerminalsAsync();
                _availableTerminals = terminals;
                if (_terminalComboBox != null)
                {
                    _terminalComboBox.DataSource = null;
                    _terminalComboBox.DataSource = _availableTerminals;
                }
                
                if (terminals.Any())
                {
                    if (_statusLabel != null) _statusLabel.Text = $"Found {terminals.Count} terminal window(s)";
                    if (_selectedTerminal == null && _terminalComboBox != null && _terminalComboBox.Items.Count > 0)
                    {
                        _terminalComboBox.SelectedIndex = 0;
                    }
                }
                else
                {
                    if (_statusLabel != null) _statusLabel.Text = "No terminal windows found";
                }
            }
            catch (Exception ex)
            {
                if (_statusLabel != null) _statusLabel.Text = $"Error scanning terminals: {ex.Message}";
                MessageBox.Show($"Error scanning for terminals: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_refreshButton != null) _refreshButton.Enabled = true;
            }
        }

        private async Task StartMonitoring()
        {
            if (_selectedTerminal == null)
            {
                MessageBox.Show("Please select a terminal to monitor.", "No Terminal Selected", 
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _monitoringCancellationTokenSource = new CancellationTokenSource();
                if (_statusLabel != null) _statusLabel.Text = $"Starting monitoring of {_selectedTerminal.ProcessName}...";
                
                // Clear previous data
                _currentProgress = null;
                _logOutput.Clear();
                
                if (_startStopButton != null)
                {
                    _startStopButton.Text = "Stop Monitoring";
                    _startStopButton.Enabled = true;
                }
                if (_refreshButton != null) _refreshButton.Enabled = false;
                if (_terminalComboBox != null) _terminalComboBox.Enabled = false;

                await _terminalMonitor.StartMonitoringAsync(_selectedTerminal, _monitoringCancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                if (_statusLabel != null) _statusLabel.Text = $"Error starting monitoring: {ex.Message}";
                MessageBox.Show($"Error starting monitoring: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                StopMonitoring();
            }
        }

        private void StopMonitoring()
        {
            try
            {
                _monitoringCancellationTokenSource?.Cancel();
                _monitoringCancellationTokenSource?.Dispose();
                _monitoringCancellationTokenSource = null;
                
                _terminalMonitor.StopMonitoring();
                
                if (_statusLabel != null) _statusLabel.Text = "Monitoring stopped";
                if (_startStopButton != null)
                {
                    _startStopButton.Text = "Start Monitoring";
                    _startStopButton.Enabled = true;
                }
                if (_refreshButton != null) _refreshButton.Enabled = true;
                if (_terminalComboBox != null) _terminalComboBox.Enabled = true;
            }
            catch (Exception ex)
            {
                if (_statusLabel != null) _statusLabel.Text = $"Error stopping monitoring: {ex.Message}";
            }
        }

        private void OnNewLineReceived(string line)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(OnNewLineReceived), line);
                return;
            }

            try
            {
                // Add to log
                if (_logOutput.Length > 10000) // Keep log size manageable
                {
                    var lines = _logOutput.ToString().Split('\n');
                    _logOutput.Clear();
                    _logOutput.AppendLine(string.Join("\n", lines.Skip(lines.Length / 2)));
                }
                
                _logOutput.AppendLine($"[{DateTime.Now:HH:mm:ss}] {line}");

                // Try to parse progress
                var progress = _progressParser.ParseLine(line);
                if (progress != null)
                {
                    _currentProgress = progress;
                    if (_statusLabel != null) _statusLabel.Text = $"Monitoring - Last update: {progress.LastUpdate:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                _logOutput.AppendLine($"[{DateTime.Now:HH:mm:ss}] Error processing line: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            try
            {
                // Update log text box
                if (_logTextBox != null && _logTextBox.Text != _logOutput.ToString())
                {
                    _logTextBox.Text = _logOutput.ToString();
                    _logTextBox.SelectionStart = _logTextBox.Text.Length;
                    _logTextBox.ScrollToCaret();
                }

                if (_currentProgress != null)
                {
                    // Update progress bar and label
                    if (_progressBar != null)
                        _progressBar.Value = Math.Min(100, Math.Max(0, (int)_currentProgress.PercentageCompleted));
                    if (_progressLabel != null)
                        _progressLabel.Text = $"{_currentProgress.PercentageCompleted:F1}% Complete ({_currentProgress.CompiledBlocks}/{_currentProgress.TotalBlocks} blocks)";

                    // Update time information
                    if (_elapsedTimeLabel != null)
                        _elapsedTimeLabel.Text = _currentProgress.ElapsedTime.ToString(@"hh\:mm\:ss");
                    if (_estimatedRemainingLabel != null)
                        _estimatedRemainingLabel.Text = _currentProgress.EstimatedTimeRemaining.ToString(@"hh\:mm\:ss");
                    if (_estimatedTotalLabel != null)
                        _estimatedTotalLabel.Text = _currentProgress.EstimatedTotalTime.ToString(@"hh\:mm\:ss");
                    if (_timePerBlockLabel != null)
                        _timePerBlockLabel.Text = $"{_currentProgress.TimePerBlock:F2} seconds";
                    if (_lastUpdateLabel != null)
                        _lastUpdateLabel.Text = _currentProgress.LastUpdate.ToString("HH:mm:ss");

                    // Update speed trend with color
                    if (_speedTrendLabel != null)
                    {
                        _speedTrendLabel.Text = _currentProgress.SpeedTrend;
                        _speedTrendLabel.ForeColor = _currentProgress.SpeedTrend switch
                        {
                            "Sped up" => Color.Green,
                            "Slowed down" => Color.Red,
                            "Steady" => Color.Blue,
                            _ => Color.Black
                        };
                    }
                }
                else
                {
                    // Reset to default values
                    if (_progressBar != null) _progressBar.Value = 0;
                    if (_progressLabel != null) _progressLabel.Text = "0.0% Complete (0/0 blocks)";
                    if (_elapsedTimeLabel != null) _elapsedTimeLabel.Text = "00:00:00";
                    if (_estimatedRemainingLabel != null) _estimatedRemainingLabel.Text = "00:00:00";
                    if (_estimatedTotalLabel != null) _estimatedTotalLabel.Text = "00:00:00";
                    if (_timePerBlockLabel != null) _timePerBlockLabel.Text = "0.00 seconds";
                    if (_speedTrendLabel != null)
                    {
                        _speedTrendLabel.Text = "Unknown";
                        _speedTrendLabel.ForeColor = Color.Black;
                    }
                    if (_lastUpdateLabel != null) _lastUpdateLabel.Text = "--:--:--";
                }
            }
            catch (Exception)
            {
                // Ignore UI update errors
            }
        }

        private void UpdateStartStopButtonState()
        {
            try
            {
                if (_startStopButton != null)
                {
                    if (!_terminalMonitor.IsMonitoring)
                    {
                        _startStopButton.Enabled = _selectedTerminal != null;
                        _startStopButton.Text = "Start Monitoring";
                    }
                    else
                    {
                        _startStopButton.Enabled = true;
                        _startStopButton.Text = "Stop Monitoring";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating start/stop button state: {ex.Message}");
            }
        }
    }
}