using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using WindowsInput;
using WindowsInput.Native;

namespace DFRContentHost.Interop
{
    public static class TouchIdDetector
    {
        private static readonly InputSimulator _inputSimulator = new InputSimulator();
        private static Thread _monitorThread;
        private static volatile bool _isRunning = false;
        private static bool _lastPromptState = false;
        private static IntPtr _currentPromptHwnd = IntPtr.Zero;

        public static event Action<bool, IntPtr> TouchIdPromptStateChanged;
        public static bool IsPromptActive => _lastPromptState;
        public static IntPtr CurrentPromptHwnd => _currentPromptHwnd;

        private static readonly string[] BiometricKeywords = new[]
        {
            "отпечаток",
            "палец",
            "fingerprint",
            "touch id",
            "windows hello",
            "подтверждение"
        };

        #region Win32 P/Invoke

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, [Out] byte[] pvInfo, uint nLength, out uint lpnLengthNeeded);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint DESKTOP_READOBJECTS = 0x0001;
        private const int UOI_NAME = 2;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const int VK_ESCAPE = 0x1B;

        #endregion

        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            _monitorThread = new Thread(DetectionLoop)
            {
                IsBackground = true,
                Name = "TouchIdDetectionThread",
                Priority = ThreadPriority.BelowNormal
            };
            _monitorThread.Start();
        }

        public static void Stop()
        {
            _isRunning = false;
        }

        private static void DetectionLoop()
        {
            while (_isRunning)
            {
                try
                {
                    // Check if system overlays / Touch ID is enabled in user config
                    if (Models.ConfigManager.Instance.Current.OverlayTouchIdPrompt)
                    {
                        bool active = EvaluateTouchIdPrompt(out IntPtr promptHwnd);

                        if (active != _lastPromptState || (active && promptHwnd != _currentPromptHwnd))
                        {
                            _lastPromptState = active;
                            _currentPromptHwnd = promptHwnd;

                            try
                            {
                                TouchIdPromptStateChanged?.Invoke(active, promptHwnd);
                            }
                            catch { }
                        }
                    }
                    else if (_lastPromptState)
                    {
                        _lastPromptState = false;
                        _currentPromptHwnd = IntPtr.Zero;
                        try
                        {
                            TouchIdPromptStateChanged?.Invoke(false, IntPtr.Zero);
                        }
                        catch { }
                    }
                }
                catch { }

                Thread.Sleep(250);
            }
        }

        private static bool EvaluateTouchIdPrompt(out IntPtr activeHwnd)
        {
            activeHwnd = IntPtr.Zero;

            // -------------------------------------------------------------
            // CHECK 1: Foreground Window (Active Window)
            // -------------------------------------------------------------
            IntPtr fg = GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                string procName = GetProcessNameFromHwnd(fg);
                if (IsCandidateProcess(procName))
                {
                    // Check 2: UI Automation confirmation
                    if (VerifyBiometricWithUIAutomation(fg))
                    {
                        activeHwnd = fg;
                        return true;
                    }
                }
            }

            // -------------------------------------------------------------
            // CHECK 1 (cont.): Visible Top-Level Windows of CredentialUIBroker
            // -------------------------------------------------------------
            IntPtr foundVisibleCandidate = IntPtr.Zero;
            bool foundVerified = false;

            EnumWindows((hWnd, lParam) =>
            {
                try
                {
                    if (IsWindowVisible(hWnd) && !IsIconic(hWnd))
                    {
                        if (GetWindowRect(hWnd, out RECT rect) && rect.Width > 60 && rect.Height > 60)
                        {
                            string proc = GetProcessNameFromHwnd(hWnd);
                            if (IsCandidateProcess(proc))
                            {
                                foundVisibleCandidate = hWnd;
                                if (VerifyBiometricWithUIAutomation(hWnd))
                                {
                                    foundVerified = true;
                                    return false; // Stop enumeration
                                }
                            }
                        }
                    }
                }
                catch { }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            if (foundVerified && foundVisibleCandidate != IntPtr.Zero)
            {
                activeHwnd = foundVisibleCandidate;
                return true;
            }

            // -------------------------------------------------------------
            // CHECK 3: Fallback Winlogon Desktop Detection
            // -------------------------------------------------------------
            if (IsWinlogonDesktopActive())
            {
                var procs = Process.GetProcessesByName("CredentialUIBroker");
                if (procs.Length > 0)
                {
                    activeHwnd = foundVisibleCandidate != IntPtr.Zero ? foundVisibleCandidate : fg;
                    return true;
                }
            }

            // Fallback: If CredentialUIBroker has a visible dialog with focus or valid rect
            if (foundVisibleCandidate != IntPtr.Zero)
            {
                var sb = new StringBuilder(256);
                GetWindowText(foundVisibleCandidate, sb, 256);
                string title = sb.ToString();

                if (string.IsNullOrEmpty(title) || ContainsBiometricKeyword(title))
                {
                    activeHwnd = foundVisibleCandidate;
                    return true;
                }
            }

            return false;
        }

        private static bool IsCandidateProcess(string procName)
        {
            if (string.IsNullOrEmpty(procName)) return false;
            return procName.IndexOf("CredentialUIBroker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   procName.IndexOf("LockApp", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetProcessNameFromHwnd(IntPtr hWnd)
        {
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                {
                    using var p = Process.GetProcessById((int)pid);
                    return p.ProcessName;
                }
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// Check 2: UI Automation inspection for biometric confirmation keywords
        /// </summary>
        private static bool VerifyBiometricWithUIAutomation(IntPtr hWnd)
        {
            try
            {
                var element = AutomationElement.FromHandle(hWnd);
                if (element == null) return false;

                // 1. Check window title / element name
                string rootName = element.Current.Name;
                if (!string.IsNullOrEmpty(rootName) && ContainsBiometricKeyword(rootName))
                {
                    return true;
                }

                // 2. Check all child Text elements
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
                var textElements = element.FindAll(TreeScope.Descendants, condition);

                foreach (AutomationElement item in textElements)
                {
                    try
                    {
                        string name = item.Current.Name;
                        if (!string.IsNullOrEmpty(name) && ContainsBiometricKeyword(name))
                        {
                            return true;
                        }
                    }
                    catch { }
                }

                // 3. Check window text via Win32
                var sb = new StringBuilder(512);
                GetWindowText(hWnd, sb, 512);
                if (ContainsBiometricKeyword(sb.ToString()))
                {
                    return true;
                }
            }
            catch
            {
                // UI Automation might throw on cross-session or restricted desktop
                return true; // Fallback to candidate presence
            }

            return false;
        }

        private static bool ContainsBiometricKeyword(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var kw in BiometricKeywords)
            {
                if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check 3: Check if OpenInputDesktop returns "Winlogon"
        /// </summary>
        private static bool IsWinlogonDesktopActive()
        {
            IntPtr hDesktop = OpenInputDesktop(0, false, DESKTOP_READOBJECTS);
            if (hDesktop != IntPtr.Zero)
            {
                try
                {
                    byte[] nameBuffer = new byte[256];
                    if (GetUserObjectInformation(hDesktop, UOI_NAME, nameBuffer, (uint)nameBuffer.Length, out uint needed))
                    {
                        string deskName = Encoding.Unicode.GetString(nameBuffer, 0, (int)needed).TrimEnd('\0');
                        if (string.Equals(deskName, "Winlogon", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
                catch { }
                finally
                {
                    CloseDesktop(hDesktop);
                }
            }
            return false;
        }

        /// <summary>
        /// Cancels the prompt by sending Escape to candidate window and simulating Escape keypress
        /// </summary>
        public static void CancelPrompt()
        {
            try
            {
                if (_currentPromptHwnd != IntPtr.Zero)
                {
                    PostMessage(_currentPromptHwnd, WM_KEYDOWN, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                    PostMessage(_currentPromptHwnd, WM_KEYUP, (IntPtr)VK_ESCAPE, IntPtr.Zero);
                }

                _inputSimulator.Keyboard.KeyPress(VirtualKeyCode.ESCAPE);
            }
            catch { }
        }
    }
}
