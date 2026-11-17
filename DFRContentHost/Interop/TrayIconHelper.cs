using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace DFRContentHost.Interop
{
    public static class TrayIconHelper
    {
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 101;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        private const uint TPM_BOTTOMALIGN = 0x0020;
        private const uint TPM_RIGHTALIGN = 0x0008;
        private const uint TPM_RETURNCMD = 0x0100;

        private const int CMD_SETTINGS = 1001;
        private const int CMD_RESTART = 1002;
        private const int CMD_EXIT = 1003;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        private static IntPtr _hwnd;
        private static NOTIFYICONDATA _nid;
        private static WndProcDelegate _wndProc;
        private static Thread _trayThread;
        private static bool _isRunning = false;
        public static string CurrentLanguage { get; set; } = "ru";

        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _trayThread = new Thread(TrayLoop)
            {
                IsBackground = true,
                Name = "TouchBarTrayThread"
            };
            _trayThread.SetApartmentState(ApartmentState.STA);
            _trayThread.Start();
        }

        private static void TrayLoop()
        {
            try
            {
                string className = "TouchBarTrayWndClass_" + Guid.NewGuid().ToString("N");
                _wndProc = CustomWndProc;

                var wc = new WNDCLASS
                {
                    lpfnWndProc = _wndProc,
                    lpszClassName = className,
                    hInstance = Process.GetCurrentProcess().Handle
                };

                RegisterClass(ref wc);

                _hwnd = CreateWindowEx(0, className, "TouchBarTrayWindow", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (_hwnd == IntPtr.Zero) return;

                // Load standard Application Icon (IDI_APPLICATION = 32512)
                IntPtr hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512);

                _nid = new NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwnd,
                    uID = 100,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                    uCallbackMessage = WM_TRAYICON,
                    hIcon = hIcon,
                    szTip = "Touch Bar (DFRContentHost) — Настройки и статус"
                };

                Shell_NotifyIcon(NIM_ADD, ref _nid);

                while (GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }

                Shell_NotifyIcon(NIM_DELETE, ref _nid);
            }
            catch { }
        }

        private static IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                int eventType = (int)lParam;
                if (eventType == WM_LBUTTONUP || eventType == WM_LBUTTONDBLCLK)
                {
                    OpenConfigurator();
                }
                else if (eventType == WM_RBUTTONUP)
                {
                    ShowContextMenu(hWnd);
                }
                return IntPtr.Zero;
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void ShowContextMenu(IntPtr hWnd)
        {
            try
            {
                bool isRu = string.Equals(CurrentLanguage, "ru", StringComparison.OrdinalIgnoreCase);
                IntPtr hMenu = CreatePopupMenu();
                AppendMenu(hMenu, 0, CMD_SETTINGS, isRu ? "⚙  Настройки Touch Bar..." : "⚙  Touch Bar Settings...");
                AppendMenu(hMenu, 0, CMD_RESTART, isRu ? "🔄  Перезапустить Touch Bar" : "🔄  Restart Touch Bar");
                AppendMenu(hMenu, 0x00000800 /* MF_SEPARATOR */, 0, "-");
                AppendMenu(hMenu, 0, CMD_EXIT, isRu ? "❌  Выход" : "❌  Exit");

                GetCursorPos(out POINT pt);
                SetForegroundWindow(hWnd);

                uint selected = TrackPopupMenuEx(hMenu, TPM_BOTTOMALIGN | TPM_RIGHTALIGN | TPM_RETURNCMD, pt.x, pt.y, hWnd, IntPtr.Zero);
                DestroyMenu(hMenu);

                if (selected == CMD_SETTINGS)
                {
                    OpenConfigurator();
                }
                else if (selected == CMD_RESTART)
                {
                    RestartTouchBar();
                }
                else if (selected == CMD_EXIT)
                {
                    ExitTouchBar();
                }
            }
            catch { }
        }

        public static void OpenConfigurator()
        {
            try
            {
                // Check if TouchBarSettings is already running
                var existing = Process.GetProcessesByName("TouchBarSettings");
                if (existing.Length > 0)
                {
                    try
                    {
                        var handle = existing[0].MainWindowHandle;
                        if (handle != IntPtr.Zero)
                        {
                            SetForegroundWindow(handle);
                            return;
                        }
                    }
                    catch { }
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = Path.Combine(baseDir, "TouchBarSettings.exe");

                // Check multiple possible locations
                if (!File.Exists(exePath))
                {
                    exePath = Path.Combine(baseDir, "..", "TouchBarSettings.exe");
                }
                if (!File.Exists(exePath))
                {
                    exePath = Path.Combine(Directory.GetCurrentDirectory(), "TouchBarSettings.exe");
                }

                if (File.Exists(exePath))
                {
                    var psi = new ProcessStartInfo(exePath)
                    {
                        WorkingDirectory = Path.GetDirectoryName(exePath),
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                else
                {
                    // Fallback: run via dotnet if DLL exists
                    string dllPath = Path.Combine(baseDir, "TouchBarSettings.dll");
                    if (File.Exists(dllPath))
                    {
                        var psi = new ProcessStartInfo("dotnet", $"\"{dllPath}\"")
                        {
                            WorkingDirectory = Path.GetDirectoryName(dllPath),
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        Process.Start(psi);
                    }
                }
            }
            catch { }
        }

        private static void RestartTouchBar()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string restartBat = Path.Combine(baseDir, "..", "Перезапустить_TouchBar.bat");
                if (File.Exists(restartBat))
                {
                    Process.Start(new ProcessStartInfo(restartBat) { UseShellExecute = true });
                    Environment.Exit(0);
                }
            }
            catch { }
        }

        private static void ExitTouchBar()
        {
            try
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                Environment.Exit(0);
            }
            catch { }
        }

        public static void Stop()
        {
            try
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                if (_hwnd != IntPtr.Zero)
                {
                    DestroyWindow(_hwnd);
                    _hwnd = IntPtr.Zero;
                }
            }
            catch { }
        }
    }
}
