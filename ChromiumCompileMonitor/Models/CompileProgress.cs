using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChromiumCompileMonitor.Models
{
    public class CompileProgress : INotifyPropertyChanged
    {
        private int _compiledBlocks;
        private int _remainingBlocks;
        private TimeSpan _elapsedTime;
        private double _percentageCompleted;
        private TimeSpan _estimatedTimeRemaining;
        private TimeSpan _estimatedTotalTime;
        private double _timePerBlock;
        private string _speedTrend = "Unknown";
        private DateTime _lastUpdate = DateTime.Now;

        public int CompiledBlocks
        {
            get => _compiledBlocks;
            set
            {
                _compiledBlocks = value;
                OnPropertyChanged();
                UpdateCalculations();
            }
        }

        public int RemainingBlocks
        {
            get => _remainingBlocks;
            set
            {
                _remainingBlocks = value;
                OnPropertyChanged();
                UpdateCalculations();
            }
        }

        public int TotalBlocks => CompiledBlocks + RemainingBlocks;

        public TimeSpan ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                _elapsedTime = value;
                OnPropertyChanged();
                UpdateCalculations();
            }
        }

        public double PercentageCompleted
        {
            get => _percentageCompleted;
            private set
            {
                _percentageCompleted = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan EstimatedTimeRemaining
        {
            get => _estimatedTimeRemaining;
            private set
            {
                _estimatedTimeRemaining = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan EstimatedTotalTime
        {
            get => _estimatedTotalTime;
            private set
            {
                _estimatedTotalTime = value;
                OnPropertyChanged();
            }
        }

        public double TimePerBlock
        {
            get => _timePerBlock;
            private set
            {
                _timePerBlock = value;
                OnPropertyChanged();
            }
        }

        public string SpeedTrend
        {
            get => _speedTrend;
            set
            {
                _speedTrend = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set
            {
                _lastUpdate = value;
                OnPropertyChanged();
            }
        }

        private void UpdateCalculations()
        {
            if (TotalBlocks > 0)
            {
                PercentageCompleted = (double)CompiledBlocks / TotalBlocks * 100;
            }

            if (CompiledBlocks > 0 && ElapsedTime.TotalSeconds > 0)
            {
                TimePerBlock = ElapsedTime.TotalSeconds / CompiledBlocks;
                EstimatedTimeRemaining = TimeSpan.FromSeconds(TimePerBlock * RemainingBlocks);
                EstimatedTotalTime = TimeSpan.FromSeconds(TimePerBlock * TotalBlocks);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}