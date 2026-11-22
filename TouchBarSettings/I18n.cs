using System;
using System.Collections.Generic;

namespace TouchBarSettings
{
    public static class I18n
    {
        private static readonly Dictionary<string, (string Ru, string En)> Strings = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            // Header
            { "AppTitle", ("Touch Bar Control Center", "Touch Bar Control Center") },
            { "AppSubtitle", ("Конфигуратор параметров, виджетов и персонажа панели Touch Bar (Windows 11)", "Configuration center for Touch Bar settings, widgets & pet companion (Windows 11)") },
            { "ServiceActive", ("Служба активна", "Service Active") },
            { "LanguageLabel", ("Язык:", "Language:") },
            { "LangAuto", ("Авто (Система)", "Auto (System)") },
            { "LangRu", ("Русский", "Russian") },
            { "LangEn", ("English", "English") },

            // Tab 1: General & Display
            { "TabDisplay", ("⚙  Экран и система", "⚙  Display & System") },
            { "EscKeyTitle", ("Виртуальная кнопка ESC", "Virtual ESC Key") },
            { "EscKeyDesc", ("Отображать виртуальную клавишу ESC в левом краю Touch Bar для моделей MacBook Pro без физической кнопки (2016–2019).", "Display a virtual ESC key on the left edge of Touch Bar for MacBook Pro models without a physical key (2016–2019).") },
            { "EscModeAuto", ("Авто (по модели Mac)", "Auto (by Mac model)") },
            { "EscModeAlwaysOn", ("Всегда показывать", "Always show") },
            { "EscModeAlwaysOff", ("Скрыть", "Hide") },
            { "OledBrightnessTitle", ("Яркость OLED-панели Touch Bar", "Touch Bar OLED Brightness") },
            { "OledBrightnessDesc", ("Регулировка аппаратной яркости дисплея Touch Bar (10%–100%)", "Adjust hardware brightness of the Touch Bar OLED display (10%–100%)") },
            { "SyncBrightness", ("Привязать к яркости основного дисплея", "Sync with main display brightness") },
            { "SleepTimeoutTitle", ("Тайм-аут автовыключения экрана (OLED Sleep)", "OLED Sleep Timeout") },
            { "SleepTimeoutDesc", ("Отключает пиксели при бездействии для защиты от выгорания OLED. Мгновенно просыпается при касании или нажатии клавиш.", "Turns off pixels when idle to protect OLED from burn-in. Wakes up immediately on touch or keypress.") },
            { "Timeout15s", ("15 секунд", "15 seconds") },
            { "Timeout30s", ("30 секунд", "30 seconds") },
            { "Timeout1m", ("1 минута", "1 minute") },
            { "Timeout3m", ("3 минуты", "3 minutes") },
            { "TimeoutNever", ("Никогда (выкл)", "Never (disabled)") },
            { "FnKeysTitle", ("Ряд клавиш F1–F12 при удержании Fn", "F1–F12 function row on Fn hold") },
            { "FnKeysDesc", ("Отображать функциональный ряд клавиш F1–F12 при нажатии и удержании физической кнопки Fn.", "Display F1–F12 function key row when holding the physical Fn key.") },
            { "AutoStartTitle", ("Автозапуск с Windows в трей", "Start with Windows (System Tray)") },
            { "AutoStartDesc", ("Запускать панель Touch Bar в Планировщике Windows с повышенными правами (без окна UAC, включая работу от аккумулятора).", "Launch Touch Bar host via Windows Task Scheduler with elevated rights (no UAC prompt, runs on battery).") },

            // Tab 2: Pets & Custom Characters
            { "TabPets", ("🐾  Персонаж и скины", "🐾  Pets & Characters") },
            { "PetMasterTitle", ("Анимированный персонаж на панели", "Animated Touch Bar Companion") },
            { "PetMasterDesc", ("Компаньон ходит по свободной части тачбара, танцует под музыку и реагирует на касания.", "A pet companion walks along the free space of the Touch Bar, dances to music, and reacts to touch.") },
            { "CurrentSkinLabel", ("Текущий скин:", "Current skin:") },
            { "ImportPetBtn", ("📁  Импорт персонажа...", "📁  Import Custom Pet...") },
            { "ImportPetHint", ("Поддерживается импорт анимированных .gif или папок со спрайтами .png. Кадры автоматически оптимизируются и масштабируются по высоте до 50 px с сохранением прозрачности.", "Supports importing animated .gif or .png sprite folders. Frames are automatically scaled to 50 px height maintaining aspect ratio and alpha transparency.") },
            { "PetBehaviorTitle", ("Настройки поведения персонажа", "Pet Behavior Settings") },
            { "PetWalkSpeed", ("Скорость ходьбы:", "Walk speed:") },
            { "PetTouchReact", ("Реагировать на нажатие пальцем (перетаскивание, эмоции сердечка и сон)", "React to finger touch (dragging, heart reactions and sleeping)") },
            { "PetCpuSpeedup", ("Ускоряться при высокой загрузке CPU (персонаж начинает бежать)", "Speed up under high CPU load (pet starts running)") },
            { "PetPinned", ("📌 Закрепить персонажа на месте (передвижение только пальцем)", "📌 Pin mascot in place (finger drag only)") },

            // Tab 3: Widgets & Overlays
            { "TabWidgets", ("🧩  Виджеты и оверлеи", "🧩  Widgets & Overlays") },
            { "WidgetsOrderTitle", ("Состав и порядок виджетов панели", "Widget Order and Visibility") },
            { "WidgetsOrderDesc", ("Отмечайте галочками активные элементы панели и настраивайте их приоритет кнопками «Вверх» и «Вниз»:", "Check active widgets and arrange display order using Up / Down buttons:") },
            { "BtnMoveUp", ("▲  Вверх", "▲  Up") },
            { "BtnMoveDown", ("▼  Вниз", "▼  Down") },
            { "CpuTempTitle", ("Источник температуры процессора (CPU)", "CPU Temperature Sensor Source") },
            { "CpuTempDesc", ("Механизм автономного считывания температуры без зависаний WMI на чипах Apple T2.", "Hardware temperature polling without WMI hangs on Apple T2 chips.") },
            { "CpuTempAuto", ("Автоматически (Auto: WMF -> LibreHardware -> CoreTemp -> WMI)", "Automatic (Auto: WMF -> LibreHardware -> CoreTemp -> WMI)") },
            { "CpuTempWmf", ("Только WMF Control (Shared Memory)", "Only WMF Control (Shared Memory)") },
            { "CpuTempDisabled", ("Отключено (Disabled)", "Disabled") },
            { "OverlaysTitle", ("Системные оверлеи и события", "System Overlays & Events") },
            { "OverlayTouchId", ("Подсказка сканера Touch ID (Windows Hello при запросе отпечатка)", "Touch ID prompt (Windows Hello biometric overlay)") },
            { "OverlayUac", ("Кнопки подтверждения UAC и системных диалогов администратора", "UAC and Administrator elevation confirmation buttons") },
            { "OverlayCharging", ("Баннер и анимация подключения зарядного устройства (MagSafe / Type-C)", "MagSafe / Type-C charger connect banner and animation") },

            // Bottom bar & Tray
            { "SavedAutoNote", ("Изменения сохраняются в config.json и применяются на лету", "Changes are saved to config.json and applied instantly") },
            { "BtnResetDefaults", ("Сбросить по умолчанию", "Reset to Defaults") },
            { "BtnApply", ("Сохранить и применить", "Save & Apply") },
            { "BtnHideToTray", ("📥 Скрыть в трей", "📥 Hide to Tray") },
            { "BtnAutoStartQuick", ("🚀 Автозапуск: {0}", "🚀 Autostart: {0}") },
            { "BtnAutoStartEnable", ("🚀 Включить автозапуск", "🚀 Enable Autostart") },
            { "BtnAutoStartDisable", ("⏹ Отключить автозапуск", "⏹ Disable Autostart") },
            { "AutoStartOn", ("Вкл", "On") },
            { "AutoStartOff", ("Выкл", "Off") },
            { "TrayBalloonTitle", ("Touch Bar Control Center", "Touch Bar Control Center") },
            { "TrayBalloonText", ("Панель свёрнута в системный трей. Кликните дважды, чтобы открыть.", "Control Center minimized to system tray. Double-click to open.") },
            { "TrayOpen", ("🖥 Открыть панель управления", "🖥 Open Control Center") },
            { "TrayRestart", ("🔄 Перезапустить Touch Bar", "🔄 Restart Touch Bar") },
            { "TrayAutoStart", ("🚀 Автозапуск: {0}", "🚀 Autostart: {0}") },
            { "TrayExit", ("❌ Выход", "❌ Exit") },

            // Dialogs
            { "AppliedTitle", ("Применено", "Applied") },
            { "AppliedMsg", ("Настройки успешно сохранены в config.json и применены к Touch Bar!", "Settings successfully saved to config.json and applied to Touch Bar!") },
            { "ResetConfirmTitle", ("Сброс настроек", "Reset Settings") },
            { "ResetConfirmMsg", ("Сбросить все параметры к значениям по умолчанию?", "Reset all settings to default values?") },
            { "ImportDlgTitle", ("Выберите анимированный GIF или кадры PNG персонажа", "Select animated GIF or PNG character frames") },
            { "ImportFilter", ("Файлы анимации (*.gif;*.png)|*.gif;*.png|GIF (*.gif)|*.gif|PNG (*.png)|*.png|Все файлы (*.*)|*.*", "Animation files (*.gif;*.png)|*.gif;*.png|GIF (*.gif)|*.gif|PNG (*.png)|*.png|All files (*.*)|*.*") },
            { "PetPromptTitle", ("Имя персонажа", "Pet Name") },
            { "PetPromptMsg", ("Введите имя нового персонажа:", "Enter name for the new pet:") },
            { "ImportSuccess", ("Персонаж '{0}' успешно импортирован и активирован на панели Touch Bar!", "Pet '{0}' successfully imported and activated on Touch Bar!") },
            { "ImportError", ("Ошибка импорта: {0}", "Import error: {0}") },
            { "ErrorTitle", ("Ошибка", "Error") },
            { "BtnOk", ("ОК", "OK") },
            { "BtnCancel", ("Отмена", "Cancel") },

            // Widget IDs to localized names
            { "Widget_Media", ("Управление медиа (SMTC)", "Media Controls (SMTC)") },
            { "Widget_Dock", ("Панель задач (macOS Dock)", "Taskbar (macOS Dock)") },
            { "Widget_Brightness", ("Слайдер яркости", "Brightness Slider") },
            { "Widget_Volume", ("Слайдер громкости", "Volume Slider") },
            { "Widget_Clock", ("Часы (Время)", "Clock (Time)") },
            { "Widget_Battery", ("Уровень батареи", "Battery Status") },
            { "Widget_CpuTemp", ("Температура CPU", "CPU Temperature") },
            { "Widget_Power", ("Кнопка питания / Меню", "Power Button / Menu") }
        };

        public static string Get(string key, string lang = "ru")
        {
            if (Strings.TryGetValue(key, out var pair))
            {
                return string.Equals(lang, "ru", StringComparison.OrdinalIgnoreCase) ? pair.Ru : pair.En;
            }
            return key;
        }

        public static string GetWidgetName(string widgetId, string lang = "ru")
        {
            string key = "Widget_" + widgetId;
            if (Strings.TryGetValue(key, out var pair))
            {
                return string.Equals(lang, "ru", StringComparison.OrdinalIgnoreCase) ? pair.Ru : pair.En;
            }
            return widgetId;
        }
    }
}
