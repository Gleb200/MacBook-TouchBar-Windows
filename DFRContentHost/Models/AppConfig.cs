using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DFRContentHost.Models
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
        public int TouchBarBrightness { get; set; } = 100; // 10% - 100%
        public bool SyncWithMainDisplayBrightness { get; set; } = false;
        public int SleepTimeoutSeconds { get; set; } = 0; // 0 = Never, 15, 30, 60, 180
        public bool ShowFnKeysOnFnPress { get; set; } = true;
        public bool AutoStartWithWindows { get; set; } = true;

        // 2. Pets & Custom Characters
        public bool PetEnabled { get; set; } = true;
        public string SelectedPetSkin { get; set; } = "Default"; // "Default", "Miku", "Marisa", or custom
        public double PetSpeed { get; set; } = 1.0; // 0.5x to 3.0x
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
}
