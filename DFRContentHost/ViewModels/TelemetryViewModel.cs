using Avalonia.Media;
using Avalonia.Threading;
using DFRContentHost.Interop;
using ReactiveUI;
using System;
using System.Management;
using System.Threading.Tasks;

namespace DFRContentHost.ViewModels
{
    public class TelemetryViewModel : ReactiveObject
    {
        private string _timeString = DateTime.Now.ToString("HH:mm");
        public string TimeString
        {
            get => _timeString;
            set => this.RaiseAndSetIfChanged(ref _timeString, value);
        }

        private int _batteryPercent = 100;
        public int BatteryPercent
        {
            get => _batteryPercent;
            set
            {
                this.RaiseAndSetIfChanged(ref _batteryPercent, value);
                this.RaisePropertyChanged(nameof(BatteryText));
                this.RaisePropertyChanged(nameof(BatteryGlyph));
                this.RaisePropertyChanged(nameof(BatteryColor));
            }
        }

        public string BatteryText => $"{BatteryPercent}%";

        private bool _isBatteryPresent = false;
        public bool IsBatteryPresent
        {
            get => _isBatteryPresent;
            set => this.RaiseAndSetIfChanged(ref _isBatteryPresent, value);
        }

        private bool _isCharging = false;
        public bool IsCharging
        {
            get => _isCharging;
            set
            {
                this.RaiseAndSetIfChanged(ref _isCharging, value);
                this.RaisePropertyChanged(nameof(BatteryGlyph));
                this.RaisePropertyChanged(nameof(BatteryColor));
            }
        }

        private static readonly IBrush BrushGreen = new SolidColorBrush(Color.Parse("#30D158"));
        private static readonly IBrush BrushRed = new SolidColorBrush(Color.Parse("#FF453A"));
        private static readonly IBrush BrushNormal = new SolidColorBrush(Color.Parse("#AEAEB2"));

        public IBrush BatteryColor
        {
            get
            {
                if (IsCharging) return BrushGreen;
                if (BatteryPercent <= 20) return BrushRed;
                return BrushNormal;
            }
        }

        public string BatteryGlyph
        {
            get
            {
                if (IsCharging) return "\uEBB5"; // Battery Charging
                if (BatteryPercent >= 90) return "\uEBAA"; // Battery 10
                if (BatteryPercent >= 80) return "\uEBA9";
                if (BatteryPercent >= 70) return "\uEBA8";
                if (BatteryPercent >= 60) return "\uEBA7";
                if (BatteryPercent >= 50) return "\uEBA6";
                if (BatteryPercent >= 40) return "\uEBA5";
                if (BatteryPercent >= 30) return "\uEBA4";
                if (BatteryPercent >= 20) return "\uEBA3";
                if (BatteryPercent >= 10) return "\uEBA2";
                return "\uEBA0"; // Battery empty
            }
        }

        private int _cpuTemp = 0;
        public int CpuTemp
        {
            get => _cpuTemp;
            set
            {
                this.RaiseAndSetIfChanged(ref _cpuTemp, value);
                this.RaisePropertyChanged(nameof(CpuTempText));
            }
        }

        public string CpuTempText => $"{CpuTemp}\u00B0";

        private bool _isCpuTempAvailable = false;
        public bool IsCpuTempAvailable
        {
            get => _isCpuTempAvailable;
            set => this.RaiseAndSetIfChanged(ref _isCpuTempAvailable, value);
        }

        private static readonly CpuTemperatureMonitor s_cpuMonitor = new CpuTemperatureMonitor();
        private DispatcherTimer _timer;
        private bool _isUpdating = false;

        public static void Shutdown()
        {
            try
            {
                s_cpuMonitor?.Dispose();
            }
            catch { }
        }

        public TelemetryViewModel()
        {
            UpdateMetricsSync();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(2000)
            };
            _timer.Tick += (s, e) => UpdateMetricsAsync();
            _timer.Start();
        }

        private void UpdateMetricsSync()
        {
            TimeString = DateTime.Now.ToString("HH:mm");

            if (NativeMethods.GetSystemPowerStatus(out var sps))
            {
                if (sps.BatteryFlag != 128 && sps.BatteryLifePercent <= 100)
                {
                    IsBatteryPresent = true;
                    BatteryPercent = sps.BatteryLifePercent;
                    IsCharging = (sps.ACLineStatus == 1) || ((sps.BatteryFlag & 8) != 0);
                }
                else
                {
                    IsBatteryPresent = false;
                }
            }
        }

        private void UpdateMetricsAsync()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            string time = DateTime.Now.ToString("HH:mm");

            Task.Run(() =>
            {
                bool hasBattery = false;
                int battPct = 100;
                bool charging = false;

                try
                {
                    if (NativeMethods.GetSystemPowerStatus(out var sps))
                    {
                        if (sps.BatteryFlag != 128 && sps.BatteryLifePercent <= 100)
                        {
                            hasBattery = true;
                            battPct = sps.BatteryLifePercent;
                            charging = (sps.ACLineStatus == 1) || ((sps.BatteryFlag & 8) != 0);
                        }
                    }
                }
                catch { }

                // Read CPU DTS temperature asynchronously via LibreHardwareMonitorLib without blocking UI or SMC
                int? tempVal = null;
                try
                {
                    tempVal = s_cpuMonitor.GetCpuTemperature();
                }
                catch { }

                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        TimeString = time;
                        IsBatteryPresent = hasBattery;
                        if (hasBattery)
                        {
                            BatteryPercent = battPct;
                            IsCharging = charging;
                        }
                        
                        if (tempVal.HasValue && tempVal.Value >= 15 && tempVal.Value <= 115)
                        {
                            if (CpuTemp != tempVal.Value)
                            {
                                CpuTemp = tempVal.Value;
                            }
                            if (!IsCpuTempAvailable)
                            {
                                IsCpuTempAvailable = true;
                            }
                        }
                        else
                        {
                            if (IsCpuTempAvailable)
                            {
                                IsCpuTempAvailable = false;
                            }
                        }
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                });
            });
        }
    }
}
