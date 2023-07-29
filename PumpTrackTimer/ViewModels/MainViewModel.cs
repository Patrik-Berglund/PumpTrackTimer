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
        private readonly ButtonStyle StartButton = new("Start", "Green");
        private readonly ButtonStyle StopButton = new("Stop", "Red");

        private const int HoldOffSeconds = 5;
        private const int MaxTimeSeconds = 120;
        private const int MinTimeSeconds = 10;

        private const int GpioPinNumber = 4;

        public MainViewModel()
        {
            _startStopButton = StartButton;

            Timer.Interval = TimeSpan.FromMilliseconds(100);
            Timer.Tick += TimerTick;

#if !DEBUG
            GpioController.OpenPin(GpioPinNumber);
            GpioController.RegisterCallbackForPinValueChangedEvent(GpioPinNumber, PinEventTypes.Falling, PinChangeEvent);
#endif
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
            StartStopButton = StartButton;

            LastTrigger = DateTime.MinValue;
            StartTime = DateTime.MinValue;
        }

        public void ClearHistory()
        {
            Index = 1;
            Times.Clear();
        }

        private void Start()
        {
            StartTime = DateTime.UtcNow;

            Timer.Start();

            StartStopButton = StopButton;
        }

        private void Stop()
        {
            if (Timer.IsEnabled)
            {
                var time = RunningTime();

                Timer.Stop();

                TimerDisplay = time.ToString(TimeDisplayFormat);
                Times.Insert(0, new(Index++, TimerDisplay, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));
            }

            StartStopButton = StartButton;
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
#if !DEBUG
        private readonly GpioController GpioController = new();
#endif
        private DateTime StartTime = DateTime.MinValue;
        private DateTime LastTrigger = DateTime.MinValue;
        private int Index = 1;

        public ObservableCollection<TimeRecord> Times { get; } = new();

        private string _timerDisplay = TimeSpan.Zero.ToString(TimeDisplayFormat);
        public string TimerDisplay
        {
            get => _timerDisplay;
            set => this.RaiseAndSetIfChanged(ref _timerDisplay, value);
        }

        private ButtonStyle _startStopButton;
        public ButtonStyle StartStopButton
        {
            get => _startStopButton;
            set => this.RaiseAndSetIfChanged(ref _startStopButton, value);
        }
    }

    public record ButtonStyle(string Label, string Color);
    public record TimeRecord(int Index, string Time, string TimeStamp);
}
