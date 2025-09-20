using System;
using System.Text.RegularExpressions;
using ChromiumCompileMonitor.Models;

namespace ChromiumCompileMonitor.Services
{
    public class ProgressParser
    {
        // Pattern to match [compiled_blocks/remaining_blocks] elapsed_time
        private readonly Regex _progressPattern = new Regex(
            @"\[(\d+)/(\d+)\]\s*(\S+)",
            RegexOptions.Compiled);

        private CompileProgress? _previousProgress;

        public CompileProgress? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var match = _progressPattern.Match(line.Trim());
            if (!match.Success)
                return null;

            try
            {
                var compiledBlocks = int.Parse(match.Groups[1].Value);
                var remainingBlocks = int.Parse(match.Groups[2].Value);
                var elapsedTimeStr = match.Groups[3].Value;

                var elapsedTime = ParseElapsedTime(elapsedTimeStr);
                if (!elapsedTime.HasValue)
                    return null;

                var progress = new CompileProgress
                {
                    CompiledBlocks = compiledBlocks,
                    RemainingBlocks = remainingBlocks,
                    ElapsedTime = elapsedTime.Value,
                    LastUpdate = DateTime.Now
                };

                // Calculate speed trend
                if (_previousProgress != null && _previousProgress.CompiledBlocks > 0 && progress.CompiledBlocks > 0)
                {
                    var currentSpeed = progress.TimePerBlock;
                    var previousSpeed = _previousProgress.TimePerBlock;
                    
                    const double threshold = 0.1; // 10% threshold for significant change
                    var percentChange = Math.Abs(currentSpeed - previousSpeed) / previousSpeed;

                    if (percentChange < threshold)
                    {
                        progress.SpeedTrend = "Steady";
                    }
                    else if (currentSpeed < previousSpeed)
                    {
                        progress.SpeedTrend = "Sped up";
                    }
                    else
                    {
                        progress.SpeedTrend = "Slowed down";
                    }
                }
                else
                {
                    progress.SpeedTrend = "Initial";
                }

                _previousProgress = progress;
                return progress;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private TimeSpan? ParseElapsedTime(string timeStr)
        {
            try
            {
                // Handle various time formats
                // HhMmSs format (e.g., "1h30m45s") - Check this FIRST
                var hoursMinutesSecondsPattern = new Regex(@"(\d+)h(\d+)m(\d+)s");
                var match = hoursMinutesSecondsPattern.Match(timeStr);
                if (match.Success)
                {
                    var hours = int.Parse(match.Groups[1].Value);
                    var minutes = int.Parse(match.Groups[2].Value);
                    var seconds = int.Parse(match.Groups[3].Value);
                    return new TimeSpan(hours, minutes, seconds);
                }

                // XmYs format (e.g., "5m30s")
                var minutesSecondsPattern = new Regex(@"(\d+)m(\d+)s");
                match = minutesSecondsPattern.Match(timeStr);
                if (match.Success)
                {
                    var minutes = int.Parse(match.Groups[1].Value);
                    var seconds = int.Parse(match.Groups[2].Value);
                    return new TimeSpan(0, minutes, seconds);
                }

                // Just minutes format (e.g., "5m")
                var minutesOnlyPattern = new Regex(@"(\d+)m$");
                match = minutesOnlyPattern.Match(timeStr);
                if (match.Success)
                {
                    var minutes = int.Parse(match.Groups[1].Value);
                    return new TimeSpan(0, minutes, 0);
                }

                // Just seconds format (e.g., "30s" or plain "30")
                var secondsOnlyPattern = new Regex(@"(\d+)s?$");
                match = secondsOnlyPattern.Match(timeStr);
                if (match.Success)
                {
                    var seconds = int.Parse(match.Groups[1].Value);
                    return new TimeSpan(0, 0, seconds);
                }

                // Try parsing as plain integer (seconds)
                if (int.TryParse(timeStr, out var totalSeconds))
                {
                    return new TimeSpan(0, 0, totalSeconds);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}