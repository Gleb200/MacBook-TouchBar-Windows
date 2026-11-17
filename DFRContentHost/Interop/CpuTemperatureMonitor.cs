using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Management;
using System.Threading.Tasks;

namespace DFRContentHost.Interop
{
    public sealed class CpuTemperatureMonitor : IDisposable
    {
        private Computer _computer;
        private IHardware _cpu;
        private ISensor _packageSensor;
        private ISensor _coreAvgSensor;
        private readonly List<ISensor> _coreSensors = new List<ISensor>();
        private bool _isComputerInitialized = false;
        private readonly object _lock = new object();

        public static string CurrentMode { get; set; } = "Auto"; // "Auto", "WMFOnly", "LHMOnly", "WmiOnly", "Disabled"
        public static string ActiveSource { get; private set; } = "None";

        private static float? _lastKnownTemp = null;
        private static DateTime _lastKnownTime = DateTime.MinValue;
        private const double TempHoldSeconds = 3.5;

        private static bool _wmiFailed = false;
        private static DateTime _lastWmiAttempt = DateTime.MinValue;

        /// <summary>
        /// Adaptive Multi-Source CPU Temperature Engine:
        /// 1. WMF Control Shared Memory (fastest, low-overhead, physical MMIO on Apple T2)
        /// 2. Autonomous LibreHardwareMonitorLib DTS MSRs (IA32_THERM_STATUS)
        /// 3. Core Temp Shared Memory (CoreTempMappingObject)
        /// 4. AIDA64 Shared Memory (AIDA64_SensorValues)
        /// 5. Windows ACPI ThermalZone WMI (MSAcpi_ThermalZoneTemperature with timeout protection)
        /// </summary>
        public int? GetCpuTemperature()
        {
            if (string.Equals(CurrentMode, "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                ActiveSource = "Disabled";
                return null;
            }

            float? result = null;

            if (string.Equals(CurrentMode, "WMFOnly", StringComparison.OrdinalIgnoreCase))
            {
                result = TryReadWmfSharedMemory();
                if (result.HasValue) ActiveSource = "WMF Control";
            }
            else if (string.Equals(CurrentMode, "LHMOnly", StringComparison.OrdinalIgnoreCase))
            {
                result = TryReadLibreHardwareMonitor();
                if (result.HasValue) ActiveSource = "LibreHardware";
            }
            else if (string.Equals(CurrentMode, "WmiOnly", StringComparison.OrdinalIgnoreCase))
            {
                result = TryReadWmiThermal();
                if (result.HasValue) ActiveSource = "WMI";
            }
            else // Automatic failover mode: Try each method in priority order
            {
                // Method 1: Fast zero-overhead WMF Control Shared Memory
                result = TryReadWmfSharedMemory();
                if (result.HasValue)
                {
                    ActiveSource = "WMF Control";
                }

                // Method 2: Autonomous LibreHardwareMonitorLib
                if (!result.HasValue)
                {
                    result = TryReadLibreHardwareMonitor();
                    if (result.HasValue)
                    {
                        ActiveSource = "LibreHardware";
                    }
                }

                // Method 3: Core Temp Shared Memory
                if (!result.HasValue)
                {
                    result = TryReadCoreTempSharedMemory();
                    if (result.HasValue)
                    {
                        ActiveSource = "Core Temp";
                    }
                }

                // Method 4: AIDA64 Shared Memory
                if (!result.HasValue)
                {
                    result = TryReadAida64SharedMemory();
                    if (result.HasValue)
                    {
                        ActiveSource = "AIDA64";
                    }
                }

                // Method 5: WMI ACPI ThermalZone
                if (!result.HasValue)
                {
                    result = TryReadWmiThermal();
                    if (result.HasValue)
                    {
                        ActiveSource = "WMI";
                    }
                }
            }

            if (result.HasValue && result.Value >= 15f && result.Value <= 115f)
            {
                _lastKnownTemp = result.Value;
                _lastKnownTime = DateTime.UtcNow;
                return (int)Math.Round(result.Value);
            }

            // Anti-flicker: if read failed for 1-2 ticks, smoothly hold last valid temp
            if (_lastKnownTemp.HasValue && (DateTime.UtcNow - _lastKnownTime).TotalSeconds < TempHoldSeconds)
            {
                return (int)Math.Round(_lastKnownTemp.Value);
            }

            ActiveSource = "None";
            return null;
        }

        private static readonly string[] s_wmfSections = new[]
        {
            "Local\\WMF_CpuTemp",
            "Global\\WMF_CpuTemp",
            "WMF_CpuTemp",
            "\\Sessions\\1\\BaseNamedObjects\\WMF_CpuTemp",
            "\\Sessions\\2\\BaseNamedObjects\\WMF_CpuTemp"
        };

        private static float? TryReadWmfSharedMemory()
        {
            foreach (var name in s_wmfSections)
            {
                try
                {
                    using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
                    using var accessor = mmf.CreateViewAccessor(0, 32, MemoryMappedFileAccess.Read);

                    // 1. Direct 4-byte float at offset 0
                    float temp0 = accessor.ReadSingle(0);
                    if (!float.IsNaN(temp0) && !float.IsInfinity(temp0) && temp0 >= 15f && temp0 <= 115f)
                    {
                        return temp0;
                    }

                    // 2. Structured layout: Magic 'WMFT' (0x54464D57 / 0x574D4654) with float at offset 8
                    int magic = accessor.ReadInt32(0);
                    if (magic == 0x54464D57 || magic == 0x574D4654)
                    {
                        float temp8 = accessor.ReadSingle(8);
                        if (!float.IsNaN(temp8) && !float.IsInfinity(temp8) && temp8 >= 15f && temp8 <= 115f)
                        {
                            return temp8;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static readonly string[] s_coreTempSections = new[]
        {
            "CoreTempMappingObject",
            "Global\\CoreTempMappingObject",
            "CoreTempMappingObject_0"
        };

        private static float? TryReadCoreTempSharedMemory()
        {
            foreach (var name in s_coreTempSections)
            {
                try
                {
                    using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
                    using var accessor = mmf.CreateViewAccessor(0, 2048, MemoryMappedFileAccess.Read);

                    uint coreCnt = accessor.ReadUInt32(1024 + 512); // uiCoreCnt
                    if (coreCnt > 0 && coreCnt <= 64)
                    {
                        float sum = 0f;
                        int validCores = 0;
                        for (int i = 0; i < coreCnt; i++)
                        {
                            float t = accessor.ReadSingle(1544 + (i * 4));
                            if (!float.IsNaN(t) && !float.IsInfinity(t) && t >= 15f && t <= 115f)
                            {
                                sum += t;
                                validCores++;
                            }
                        }
                        if (validCores > 0)
                        {
                            return sum / validCores;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static readonly string[] s_aidaSections = new[]
        {
            "AIDA64_SensorValues",
            "Global\\AIDA64_SensorValues"
        };

        private static float? TryReadAida64SharedMemory()
        {
            foreach (var name in s_aidaSections)
            {
                try
                {
                    using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
                    using var accessor = mmf.CreateViewAccessor(0, 4096, MemoryMappedFileAccess.Read);
                    byte[] bytes = new byte[4096];
                    int read = accessor.ReadArray(0, bytes, 0, bytes.Length);
                    if (read > 0)
                    {
                        string xml = System.Text.Encoding.ASCII.GetString(bytes, 0, read);
                        string[] tags = new[] { "<id>TCPU</id>", "<id>TCPUPKG</id>", "<id>TCPU1</id>" };
                        foreach (var tag in tags)
                        {
                            int idx = xml.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                int valStart = xml.IndexOf("<val>", idx, StringComparison.OrdinalIgnoreCase);
                                int valEnd = xml.IndexOf("</val>", idx, StringComparison.OrdinalIgnoreCase);
                                if (valStart >= 0 && valEnd > valStart)
                                {
                                    string valStr = xml.Substring(valStart + 5, valEnd - (valStart + 5));
                                    if (float.TryParse(valStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t))
                                    {
                                        if (t >= 15f && t <= 115f) return t;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static float? TryReadWmiThermal()
        {
            if (_wmiFailed && (DateTime.UtcNow - _lastWmiAttempt).TotalSeconds < 25)
            {
                return null;
            }

            _lastWmiAttempt = DateTime.UtcNow;

            try
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                        searcher.Options.Timeout = TimeSpan.FromMilliseconds(200);
                        using var collection = searcher.Get();
                        foreach (ManagementObject mo in collection)
                        {
                            if (mo["CurrentTemperature"] != null)
                            {
                                double kelvin10 = Convert.ToDouble(mo["CurrentTemperature"]);
                                float cel = (float)((kelvin10 - 2732.0) / 10.0);
                                if (cel >= 15f && cel <= 115f)
                                {
                                    return (float?)cel;
                                }
                            }
                        }
                    }
                    catch { }
                    return null;
                });

                if (task.Wait(200))
                {
                    if (task.Result.HasValue)
                    {
                        _wmiFailed = false;
                        return task.Result;
                    }
                }
            }
            catch { }

            _wmiFailed = true;
            return null;
        }

        private float? TryReadLibreHardwareMonitor()
        {
            lock (_lock)
            {
                if (!_isComputerInitialized)
                {
                    if (!InitializeComputer()) return null;
                }

                try
                {
                    if (_cpu == null) return null;

                    // Update ONLY the CPU hardware node
                    _cpu.Update();

                    // Priority 1: CPU Package
                    if (_packageSensor != null && _packageSensor.Value.HasValue)
                    {
                        float val = _packageSensor.Value.Value;
                        if (val >= 15f && val <= 115f) return val;
                    }

                    // Priority 2: Core Average
                    if (_coreAvgSensor != null && _coreAvgSensor.Value.HasValue)
                    {
                        float val = _coreAvgSensor.Value.Value;
                        if (val >= 15f && val <= 115f) return val;
                    }

                    // Priority 3: Arithmetic average of CPU Core #1..N (excluding Distance/TjMax)
                    if (_coreSensors.Count > 0)
                    {
                        float sum = 0f;
                        int count = 0;
                        foreach (var cs in _coreSensors)
                        {
                            if (cs.Value.HasValue && cs.Value.Value > 0)
                            {
                                sum += cs.Value.Value;
                                count++;
                            }
                        }

                        if (count > 0)
                        {
                            float avg = sum / count;
                            if (avg >= 15f && avg <= 115f) return avg;
                        }
                    }

                    // Priority 4: Any temperature sensor on CPU
                    foreach (var s in _cpu.Sensors)
                    {
                        if (s.SensorType == SensorType.Temperature && s.Value.HasValue)
                        {
                            string sName = s.Name ?? "";
                            if (!sName.Contains("Distance", StringComparison.OrdinalIgnoreCase) &&
                                !sName.Contains("TjMax", StringComparison.OrdinalIgnoreCase))
                            {
                                float v = s.Value.Value;
                                if (v >= 15f && v <= 115f) return v;
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback gracefully on sensor read failure
                }

                return null;
            }
        }

        private bool InitializeComputer()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = false,
                    IsMemoryEnabled = false,
                    IsMotherboardEnabled = false,
                    IsControllerEnabled = false,
                    IsNetworkEnabled = false,
                    IsStorageEnabled = false,
                    IsBatteryEnabled = false,
                    IsPsuEnabled = false
                };

                _computer.Open();

                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType == HardwareType.Cpu)
                    {
                        _cpu = hw;
                        _cpu.Update();
                        break;
                    }
                }

                if (_cpu != null)
                {
                    DiscoverSensors();
                    _isComputerInitialized = true;
                    return true;
                }
            }
            catch
            {
                CloseInternal();
            }

            return false;
        }

        private void DiscoverSensors()
        {
            _packageSensor = null;
            _coreAvgSensor = null;
            _coreSensors.Clear();

            if (_cpu == null) return;

            foreach (var s in _cpu.Sensors)
            {
                if (s.SensorType == SensorType.Temperature)
                {
                    string name = s.Name ?? string.Empty;
                    if (name.IndexOf("Distance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("TjMax", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    if (name.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _packageSensor = s;
                    }
                    else if (name.IndexOf("Average", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _coreAvgSensor = s;
                    }
                    else if (name.IndexOf("Core", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _coreSensors.Add(s);
                    }
                }
            }
        }

        private void CloseInternal()
        {
            try
            {
                _computer?.Close();
            }
            catch { }
            finally
            {
                _computer = null;
                _cpu = null;
                _packageSensor = null;
                _coreAvgSensor = null;
                _coreSensors.Clear();
                _isComputerInitialized = false;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                CloseInternal();
            }
        }
    }
}
