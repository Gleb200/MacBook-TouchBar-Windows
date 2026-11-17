using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TouchBarSettings.Models
{
    public class WidgetConfigItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public int Order { get; set; } = 0;
    }

    public class AppConfig
    {
        // Language: "Auto" (follow Windows system language), "ru", "en"
        public string Language { get; set; } = "Auto";

        public string GetEffectiveLanguage()
        {
            if (string.Equals(Language, "ru", StringComparison.OrdinalIgnoreCase)) return "ru";
            if (string.Equals(Language, "en", StringComparison.OrdinalIgnoreCase)) return "en";

            // Auto: check Windows culture
            try
            {
                var culture = System.Globalization.CultureInfo.CurrentUICulture;
                var lang = culture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (lang == "ru" || lang == "be" || lang == "uk" || lang == "kk")
                {
                    return "ru";
                }
            }
            catch { }
            return "en";
        }

        // 1. General & Display
        public string ShowEscButtonMode { get; set; } = "Auto"; // "Auto", "AlwaysOn", "AlwaysOff"
        public int TouchBarBrightness { get; set; } = 100;
        public bool SyncWithMainDisplayBrightness { get; set; } = false;
        public int SleepTimeoutSeconds { get; set; } = 0; // 0 = Never
        public bool ShowFnKeysOnFnPress { get; set; } = true;
        public bool AutoStartWithWindows { get; set; } = true;

        // 2. Pets & Custom Characters
        public bool PetEnabled { get; set; } = true;
        public string SelectedPetSkin { get; set; } = "Default";
        public double PetSpeed { get; set; } = 1.0;
        public bool PetReactToTouch { get; set; } = true;
        public bool PetSpeedUpOnHighCpu { get; set; } = true;
        public bool PetPinned { get; set; } = false; // When true, stays stationary; moved only by finger
        public double PetPinnedX { get; set; } = 350.0; // Pinned X position on Touch Bar

        // 3. Widgets & Overlays
        public List<WidgetConfigItem> Widgets { get; set; } = new List<WidgetConfigItem>();
        public string CpuTempSource { get; set; } = "Auto"; // "Auto", "WMFOnly", "Disabled"

        public bool OverlayTouchIdPrompt { get; set; } = true;
        public bool OverlayUacDialogs { get; set; } = true;
        public bool OverlayChargingBanner { get; set; } = true;

        public static AppConfig CreateDefault()
        {
            var config = new AppConfig();
            config.Widgets = new List<WidgetConfigItem>
            {
                new WidgetConfigItem { Id = "Media", Name = "Управление медиа (SMTC)", Enabled = true, Order = 0 },
                new WidgetConfigItem { Id = "Dock", Name = "Панель задач (macOS Dock)", Enabled = true, Order = 1 },
                new WidgetConfigItem { Id = "Brightness", Name = "Слайдер яркости", Enabled = true, Order = 2 },
                new WidgetConfigItem { Id = "Volume", Name = "Слайдер громкости", Enabled = true, Order = 3 },
                new WidgetConfigItem { Id = "Clock", Name = "Часы (Время)", Enabled = true, Order = 4 },
                new WidgetConfigItem { Id = "Battery", Name = "Уровень батареи", Enabled = true, Order = 5 },
                new WidgetConfigItem { Id = "CpuTemp", Name = "Температура CPU", Enabled = true, Order = 6 },
                new WidgetConfigItem { Id = "Power", Name = "Кнопка питания / Меню", Enabled = true, Order = 7 }
            };
            return config;
        }
    }

    public static class ConfigRepository
    {
        private static string _configPath;

        public static string GetConfigPath()
        {
            if (_configPath != null) return _configPath;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. If running from root and HostApp_v2\config.json exists, that is the primary config for DFRContentHost
            string hostAppConfig = Path.Combine(baseDir, "HostApp_v2", "config.json");
            if (File.Exists(hostAppConfig))
            {
                _configPath = hostAppConfig;
                return _configPath;
            }

            // 2. If running inside HostApp_v2 directly
            string localConfig = Path.Combine(baseDir, "config.json");
            if (File.Exists(localConfig))
            {
                _configPath = localConfig;
                return _configPath;
            }

            // 3. Check parent folder (e.g. if running in bin\Release\netcoreapp3.1)
            string parentHostAppConfig = Path.Combine(baseDir, "..", "..", "..", "..", "HostApp_v2", "config.json");
            if (File.Exists(parentHostAppConfig))
            {
                _configPath = Path.GetFullPath(parentHostAppConfig);
                return _configPath;
            }

            string parentConfig = Path.Combine(baseDir, "..", "config.json");
            if (File.Exists(parentConfig))
            {
                _configPath = Path.GetFullPath(parentConfig);
                return _configPath;
            }

            // Default
            _configPath = Directory.Exists(Path.Combine(baseDir, "HostApp_v2"))
                ? Path.Combine(baseDir, "HostApp_v2", "config.json")
                : localConfig;
            return _configPath;
        }

        public static AppConfig Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json, opts);
                    if (loaded != null)
                    {
                        if (loaded.Widgets == null || loaded.Widgets.Count == 0)
                        {
                            loaded.Widgets = AppConfig.CreateDefault().Widgets;
                        }
                        return loaded;
                    }
                }
            }
            catch { }

            var def = AppConfig.CreateDefault();
            Save(def);
            return def;
        }

        public static void Save(AppConfig config)
        {
            try
            {
                string path = GetConfigPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var opts = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, opts);
                File.WriteAllText(path, json);

                // Always synchronize to both root and HostApp_v2 locations so changes are never lost
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string sync1 = Path.Combine(baseDir, "config.json");
                string sync2 = Path.Combine(baseDir, "HostApp_v2", "config.json");
                string sync3 = Path.Combine(baseDir, "..", "config.json");

                foreach (var syncPath in new[] { sync1, sync2, sync3 })
                {
                    try
                    {
                        string fullSync = Path.GetFullPath(syncPath);
                        if (fullSync != path && (File.Exists(fullSync) || Directory.Exists(Path.GetDirectoryName(fullSync))))
                        {
                            File.WriteAllText(fullSync, json);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
