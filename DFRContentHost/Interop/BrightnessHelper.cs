using System;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DFRContentHost.Interop
{
    public static class BrightnessHelper
    {
        private static int _cachedBrightness = -1;
        private static bool _initialized = false;
        private static volatile int _targetBrightness = -1;
        private static readonly object _brightLock = new object();
        private static bool _brightWorkerRunning = false;
        private static int _lastAppliedBrightness = -1;
        private static bool _autoBrightnessEnabled = false;

        public static bool IsSettingInternally { get; set; } = false;
        public static event Action<int> BrightnessNotificationReceived;

        public static bool IsAutoBrightnessEnabled => QueryIsAutoBrightnessEnabled();

        public static bool QueryIsAutoBrightnessEnabled()
        {
            try
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
                if (baseKey != null)
                {
                    var activeScheme = baseKey.GetValue("ActivePowerScheme") as string;
                    if (!string.IsNullOrEmpty(activeScheme))
                    {
                        using var subKey = baseKey.OpenSubKey($@"{activeScheme}\7516b95f-f776-4464-8c53-06167f40cc99\fbd9aa66-9553-4097-ba44-ed6e9d65eab8");
                        if (subKey != null)
                        {
                            var ac = subKey.GetValue("ACSettingIndex");
                            var dc = subKey.GetValue("DCSettingIndex");
                            if (ac is int acVal && acVal > 0) return true;
                            if (dc is int dcVal && dcVal > 0) return true;
                        }
                    }
                }
            }
            catch { }
            return _autoBrightnessEnabled;
        }

        public static void SetAutoBrightness(bool enabled)
        {
            _autoBrightnessEnabled = enabled;
            Task.Run(() =>
            {
                try
                {
                    uint val = enabled ? 1u : 0u;
                    var psi = new ProcessStartInfo("powercfg", $"/SETACVALUEINDEX SCHEME_CURRENT SUB_VIDEO ADAPTBRIGHT {val}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using (var p = Process.Start(psi)) { p?.WaitForExit(2000); }

                    psi.Arguments = $"/SETDCVALUEINDEX SCHEME_CURRENT SUB_VIDEO ADAPTBRIGHT {val}";
                    using (var p = Process.Start(psi)) { p?.WaitForExit(2000); }

                    psi.Arguments = "/SETACTIVE SCHEME_CURRENT";
                    using (var p = Process.Start(psi)) { p?.WaitForExit(2000); }

                    if (enabled)
                    {
                        var scPsi = new ProcessStartInfo("sc.exe", "start SensrSvc")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (var p = Process.Start(scPsi)) { p?.WaitForExit(1000); }
                    }
                }
                catch { }
            });
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _autoBrightnessEnabled = QueryIsAutoBrightnessEnabled();

            Task.Run(() =>
            {
                // Initial fetch
                int initial = QueryCurrentBrightness();
                if (initial >= 0)
                {
                    _cachedBrightness = initial;
                    _lastAppliedBrightness = initial;
                    BrightnessNotificationReceived?.Invoke(initial);
                }

                // 1. Try WMI Event Watcher
                try
                {
                    var watcher = new ManagementEventWatcher(new ManagementScope(@"root\wmi"), new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
                    watcher.EventArrived += (s, e) =>
                    {
                        try
                        {
                            int b = Convert.ToInt32(e.NewEvent["Brightness"]);
                            if (!IsSettingInternally && b >= 0 && b <= 100 && b != _cachedBrightness)
                            {
                                _cachedBrightness = b;
                                _lastAppliedBrightness = b;
                                BrightnessNotificationReceived?.Invoke(b);
                            }
                        }
                        catch { }
                    };
                    watcher.Start();
                }
                catch { }

                // 2. Continuous background polling fallback (every 1.5s)
                while (true)
                {
                    Thread.Sleep(1500);
                    if (!IsSettingInternally && !_brightWorkerRunning)
                    {
                        int current = QueryCurrentBrightness();
                        if (current >= 0 && current != _cachedBrightness)
                        {
                            _cachedBrightness = current;
                            _lastAppliedBrightness = current;
                            BrightnessNotificationReceived?.Invoke(current);
                        }
                    }
                }
            });
        }

        public static int QueryCurrentBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    int val = Convert.ToInt32(obj["CurrentBrightness"]);
                    return val;
                }
            }
            catch { }
            return -1;
        }

        public static int GetBrightness()
        {
            Initialize();
            int current = QueryCurrentBrightness();
            if (current >= 0)
            {
                _cachedBrightness = current;
                return current;
            }
            return _cachedBrightness >= 0 ? _cachedBrightness : 70;
        }

        public static void SetBrightness(int percent)
        {
            percent = Math.Max(0, Math.Min(100, percent));
            _cachedBrightness = percent;
            _targetBrightness = percent;

            lock (_brightLock)
            {
                if (!_brightWorkerRunning)
                {
                    _brightWorkerRunning = true;
                    Task.Run(BrightnessWorkerLoop);
                }
            }
        }

        private static void BrightnessWorkerLoop()
        {
            while (true)
            {
                int target;
                lock (_brightLock)
                {
                    target = _targetBrightness;
                }

                if (target >= 0 && target != _lastAppliedBrightness)
                {
                    try
                    {
                        IsSettingInternally = true;
                        using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            try
                            {
                                using var inParams = obj.GetMethodParameters("WmiSetBrightness");
                                inParams["Timeout"] = (uint)1;
                                inParams["Brightness"] = (byte)target;
                                obj.InvokeMethod("WmiSetBrightness", inParams, null);
                            }
                            catch
                            {
                                obj.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)target });
                            }
                            _lastAppliedBrightness = target;
                            break;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        IsSettingInternally = false;
                    }
                }

                Thread.Sleep(60);

                lock (_brightLock)
                {
                    if (_targetBrightness == _lastAppliedBrightness)
                    {
                        _brightWorkerRunning = false;
                        break;
                    }
                }
            }
        }
    }
}
