using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Device.Gpio;

namespace PumpTrackTimer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private const string TimeDisplayFormat = @"mm\:ss\.fff";
        private const string StartLable = "Start";
        private const string StopLable = "Stop";
        private const int HoldOffSeconds = 5;
        private const int MaxTimeSeconds = 60;
        private const int MinTimeSeconds = 20;

        private const int gpioPinNumber = 18;

        public MainViewModel()
        {
            Timer.Interval = TimeSpan.FromMilliseconds(100);
            Timer.Tick += TimerTick;

            GpioController.OpenPin(gpioPinNumber, PinMode.InputPullUp);
            GpioController.RegisterCallbackForPinValueChangedEvent(gpioPinNumber, PinEventTypes.Falling, PinChangeEvent);
        }

        public void StartStop()
        {
            LastTrigger = DateTime.UtcNow;

            if (Timer.IsEnabled)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }

        public void Reset()
        {
            Timer.Stop();
            TimerDisplay = TimeSpan.Zero.ToString(TimeDisplayFormat);

            StartStopLabel = StartLable;
        }

        public void ClearHistory()
        {
            Index = 1;
            Times.Clear();
        }

        private void Start()
        {
            Timer.Start();

            StartTime = DateTime.UtcNow;
            StartStopLabel = StopLable;
        }

        private void Stop()
        {
            var time = RunningTime();

            Timer.Stop();

            TimerDisplay = time.ToString(TimeDisplayFormat);
            Times.Add(new(Index++, TimerDisplay));

            StartStopLabel = StartLable;
        }

        private void TimerTick(object? sender, EventArgs e)
        {
            var time = RunningTime();

            TimerDisplay = time.ToString(TimeDisplayFormat);

            if (time > TimeSpan.FromSeconds(MaxTimeSeconds))
            {
                Stop();
            }
        }

        private void PinChangeEvent(object sender, PinValueChangedEventArgs args)
        {
            Dispatcher.UIThread.Invoke(() => ExternalTrigger());
        }

        private void ExternalTrigger()
        {
            if (ExternalTriggerEnabled is false)
            {
                return;
            }

            if (DateTime.UtcNow - LastTrigger < TimeSpan.FromSeconds(HoldOffSeconds))
            {
                return;
            }

            if (RunningTime() < TimeSpan.FromSeconds(MinTimeSeconds))
            {
                return;
            }

            StartStop();
        }

        private TimeSpan RunningTime()
        {
            return DateTime.UtcNow - StartTime;
        }

        public bool ExternalTriggerEnabled { get; set; } = true;

        private readonly DispatcherTimer Timer = new();
        private readonly GpioController GpioController = new();

        private DateTime StartTime;
        private DateTime LastTrigger;
        private int Index = 1;

        public ObservableCollection<TimeRecord> Times { get; private set; } = new();

        private string _timerDisplay = TimeSpan.Zero.ToString(TimeDisplayFormat);
        public string TimerDisplay
        {
            get => _timerDisplay;
            set => this.RaiseAndSetIfChanged(ref _timerDisplay, value);
        }

        private string _startStopLabel = StartLable;
        public string StartStopLabel
        {
            get => _startStopLabel;
            set => this.RaiseAndSetIfChanged(ref _startStopLabel, value);
        }
    }

    public record TimeRecord(int Index, string Time);
}
