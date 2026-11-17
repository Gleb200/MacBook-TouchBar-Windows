using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using ReactiveUI;

namespace DFRContentHost.Interop
{
    public class AppWindowInfo : ReactiveObject
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
        public Avalonia.Media.Imaging.Bitmap AppIcon { get; set; }
        public bool HasAppIcon => AppIcon != null;
        public string IconGlyph { get; set; }
        public bool HasIconGlyph => !HasAppIcon && !string.IsNullOrEmpty(IconGlyph);
        public string AppName { get; set; }
        public ICommand SwitchCommand { get; set; }
        public ICommand CloseCommand { get; set; }

        private bool _isActiveApp;
        public bool IsActiveApp
        {
            get => _isActiveApp;
            set
            {
                this.RaiseAndSetIfChanged(ref _isActiveApp, value);
                this.RaisePropertyChanged(nameof(PillBackground));
                this.RaisePropertyChanged(nameof(PillBorderBrush));
                this.RaisePropertyChanged(nameof(TitleForeground));
                this.RaisePropertyChanged(nameof(BorderThicknessVal));
                this.RaisePropertyChanged(nameof(ShowActiveDot));
                this.RaisePropertyChanged(nameof(ActiveDotOpacity));
            }
        }

        private static readonly Avalonia.Media.IBrush BrushPillActiveBg = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3A3A3E"));
        private static readonly Avalonia.Media.IBrush BrushPillNormalBg = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#262629"));
        private static readonly Avalonia.Media.IBrush BrushPillActiveBorder = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0A84FF"));
        private static readonly Avalonia.Media.IBrush BrushPillNormalBorder = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#262629"));

        private static readonly Avalonia.Media.IBrush BrushTitleActive = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFFFFF"));
        private static readonly Avalonia.Media.IBrush BrushTitleNormal = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AEAEB2"));

        public Avalonia.Media.IBrush PillBackground => IsActiveApp ? BrushPillActiveBg : BrushPillNormalBg;
        public Avalonia.Media.IBrush PillBorderBrush => IsActiveApp ? BrushPillActiveBorder : BrushPillNormalBorder;
        public Avalonia.Media.IBrush TitleForeground => IsActiveApp ? BrushTitleActive : BrushTitleNormal;
        public double BorderThicknessVal => IsActiveApp ? 1.5 : 1.0;
        public bool ShowActiveDot => IsActiveApp;
        public double ActiveDotOpacity => IsActiveApp ? 1.0 : 0.0;

        private double _tabWidth = 140.0;
        public double TabWidth
        {
            get => _tabWidth;
            set
            {
                if (Math.Abs(_tabWidth - value) > 0.5)
                {
                    this.RaiseAndSetIfChanged(ref _tabWidth, value);
                    this.RaisePropertyChanged(nameof(TextMaxWidth));
                    this.RaisePropertyChanged(nameof(ShowCloseButton));
                    this.RaisePropertyChanged(nameof(DisplayTitle));
                }
            }
        }

        public bool ShowCloseButton => TabWidth >= 64.0;
        public double TextMaxWidth => Math.Max(10.0, TabWidth - (ShowCloseButton ? 44.0 : 26.0));

        public string DisplayTitle
        {
            get
            {
                if (string.IsNullOrEmpty(AppName)) return "";
                // Average character width for Segoe UI 11pt medium is around 6.8 pixels
                double maxW = TextMaxWidth;
                int maxChars = Math.Max(2, (int)(maxW / 6.8));
                if (AppName.Length > maxChars)
                {
                    if (maxChars <= 3) return AppName.Substring(0, Math.Min(2, AppName.Length)) + "..";
                    return AppName.Substring(0, maxChars - 1) + "…";
                }
                return AppName;
            }
        }
    }

    public static class WindowSwitcherHelper
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_MENU = 0x12;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
        private const uint GW_OWNER = 4;

        private const uint WM_CLOSE = 0x0010;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint GA_ROOT = 2;
        private const uint GA_ROOTOWNER = 3;

        private static readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> _iconCache =
            new Dictionary<string, Avalonia.Media.Imaging.Bitmap>(StringComparer.OrdinalIgnoreCase);

        // Persistent stable order list so apps never jump or swap places!
        private static readonly List<IntPtr> _stableWindowOrder = new List<IntPtr>();
        private static readonly object _orderLock = new object();

        public static List<AppWindowInfo> GetRunningWindows()
        {
            var currentWindows = new Dictionary<IntPtr, AppWindowInfo>();
            var seenProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IntPtr fg = GetForegroundWindow();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                // Ignore windows with owners (dialogs, popups, toolbars)
                if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero) return true;

                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString().Trim();

                if (string.IsNullOrEmpty(title) ||
                    title == "Program Manager" ||
                    title == "Settings" ||
                    title == "Параметры" ||
                    title == "Touch Bar Content Host" ||
                    title == "Windows Input Experience" ||
                    title == "Windows Shell Experience Host" ||
                    title.StartsWith("Интерфейс", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("ввода текста", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                GetWindowThreadProcessId(hWnd, out uint pid);
                string procName = "";
                string exePath = "";

                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProcess != IntPtr.Zero)
                {
                    var pathSb = new StringBuilder(1024);
                    int size = pathSb.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, pathSb, ref size))
                    {
                        exePath = pathSb.ToString();
                        procName = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
                    }
                    CloseHandle(hProcess);
                }

                if (string.IsNullOrEmpty(procName))
                {
                    try
                    {
                        using var p = Process.GetProcessById((int)pid);
                        procName = p.ProcessName.ToLowerInvariant();
                    }
                    catch
                    {
                        procName = "app";
                    }
                }

                if (procName == "applicationframehost" ||
                    procName == "shellexperiencehost" ||
                    procName == "textinputhost" ||
                    procName == "searchhost" ||
                    procName == "searchapp" ||
                    procName == "startmenuexperiencehost" ||
                    procName == "ctfmon" ||
                    procName == "systemsettings" ||
                    procName == "lockapp")
                {
                    return true;
                }

                // Support up to 25 open apps with smooth horizontal scrolling & adaptive shrinking
                if (seenProcesses.Contains(procName) && currentWindows.Count >= 25)
                    return true;

                seenProcesses.Add(procName);

                var (glyph, name) = GetAppPresentation(procName, title);
                var icon = GetOrExtractIcon(procName, exePath);

                bool isActive = (hWnd == fg || 
                                 GetAncestor(fg, GA_ROOT) == hWnd || 
                                 GetAncestor(fg, GA_ROOTOWNER) == hWnd);

                var appInfo = new AppWindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = procName,
                    AppIcon = icon,
                    IconGlyph = glyph,
                    AppName = name,
                    IsActiveApp = isActive
                };
                appInfo.SwitchCommand = ReactiveCommand.Create(() =>
                {
                    try { File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] SwitchCommand FIRED for {appInfo.AppName} ({appInfo.Handle})\n"); } catch { }
                    if (ViewModels.MainViewModel.Instance != null)
                    {
                        foreach (var app in ViewModels.MainViewModel.Instance.RunningApps)
                        {
                            app.IsActiveApp = (app.Handle == appInfo.Handle);
                        }
                        try { Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                    }
                    SwitchOrMinimize(appInfo.Handle);
                });
                appInfo.CloseCommand = ReactiveCommand.Create(() =>
                {
                    try { File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] CloseCommand FIRED for {appInfo.AppName} ({appInfo.Handle})\n"); } catch { }
                    CloseWindow(appInfo.Handle);
                    lock (_orderLock)
                    {
                        _stableWindowOrder.Remove(appInfo.Handle);
                    }
                    ViewModels.MainViewModel.Instance?.RemoveApp(appInfo.Handle);
                });

                currentWindows[hWnd] = appInfo;

                return currentWindows.Count < 25;
            }, IntPtr.Zero);

            lock (_orderLock)
            {
                // 1. Remove windows that were closed
                _stableWindowOrder.RemoveAll(h => !currentWindows.ContainsKey(h));

                // 2. Add new windows strictly to the end
                foreach (var kvp in currentWindows)
                {
                    if (!_stableWindowOrder.Contains(kvp.Key))
                    {
                        _stableWindowOrder.Add(kvp.Key);
                    }
                }

                // 3. Return items strictly in stable order so they never swap positions
                var orderedResult = new List<AppWindowInfo>();
                foreach (var h in _stableWindowOrder)
                {
                    if (currentWindows.TryGetValue(h, out var info))
                    {
                        orderedResult.Add(info);
                    }
                }

                return orderedResult;
            }
        }

        private static Avalonia.Media.Imaging.Bitmap GetOrExtractIcon(string procName, string exePath)
        {
            if (string.IsNullOrEmpty(procName)) return null;

            if (_iconCache.TryGetValue(procName, out var cached))
            {
                return cached;
            }

            try
            {
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    using var ico = Icon.ExtractAssociatedIcon(exePath);
                    if (ico != null)
                    {
                        using var bmp = ico.ToBitmap();
                        using var ms = new MemoryStream();
                        bmp.Save(ms, ImageFormat.Png);
                        ms.Position = 0;
                        var avaloniaBmp = new Avalonia.Media.Imaging.Bitmap(ms);
                        _iconCache[procName] = avaloniaBmp;
                        return avaloniaBmp;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static (string glyph, string name) GetAppPresentation(string procName, string title)
        {
            switch (procName)
            {
                case "chrome": return ("\uE774", "Chrome");
                case "msedge": return ("\uE774", "Edge");
                case "firefox": return ("\uE774", "Firefox");
                case "telegram": return ("\uE122", "Telegram");
                case "spotify": return ("\uE189", "Spotify");
                case "code": return ("\uE943", "VS Code");
                case "explorer": return ("\uE8B7", "Explorer");
                case "discord": return ("\uE134", "Discord");
                case "notepad": return ("\uE70F", "Notepad");
                case "windowsterminal": return ("\uE756", "Terminal");
                case "cmd": return ("\uE756", "cmd");
                case "powershell": return ("\uE756", "PowerShell");
                default:
                    string clean = title;
                    if (clean.Length > 10) clean = clean.Substring(0, 9) + "…";
                    return ("\uE71D", clean);
            }
        }

        public static void SwitchOrMinimize(IntPtr hWnd)
        {
            try
            {
                try { File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] SwitchOrMinimize called for {hWnd}\n"); } catch { }
                IntPtr fg = GetForegroundWindow();
                bool isForegroundActive = (fg == hWnd || 
                                           GetAncestor(fg, GA_ROOT) == hWnd || 
                                           GetAncestor(fg, GA_ROOTOWNER) == hWnd);

                if (isForegroundActive && !IsIconic(hWnd))
                {
                    try { File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] Window is foreground -> minimizing it\n"); } catch { }
                    ShowWindow(hWnd, SW_MINIMIZE);
                    return;
                }

                try { File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] Window is not foreground -> activating it\n"); } catch { }

                // Bypass Windows SetForegroundWindow restrictions by pulsing dummy Alt key
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                uint foreThread = GetWindowThreadProcessId(fg, out _);
                uint appThread = GetCurrentThreadId();

                if (foreThread != 0 && foreThread != appThread)
                {
                    AttachThreadInput(appThread, foreThread, true);
                    BringWindowToTop(hWnd);
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    else
                    {
                        ShowWindow(hWnd, SW_SHOW);
                    }
                    SetForegroundWindow(hWnd);
                    AttachThreadInput(appThread, foreThread, false);
                }
                else
                {
                    BringWindowToTop(hWnd);
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    else
                    {
                        ShowWindow(hWnd, SW_SHOW);
                    }
                    SetForegroundWindow(hWnd);
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] SwitchOrMinimize error: {ex.Message}\n"); } catch { }
            }
        }

        public static void CloseWindow(IntPtr hWnd)
        {
            try
            {
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
            }
        }
    }
}
