using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DFRContentHost.Interop
{
    public static class MacModelHelper
    {
        public static string GetSystemProductName()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (key != null)
                    {
                        string prod = key.GetValue("SystemProductName") as string;
                        if (!string.IsNullOrWhiteSpace(prod)) return prod.Trim();
                    }
                }
            }
            catch { }
            return "";
        }

        public static bool IsLegacyTouchBarWithoutPhysicalEsc()
        {
            string prodName = GetSystemProductName();
            if (string.IsNullOrEmpty(prodName)) return false;

            // Touch Bar models without physical Esc (Late 2016 - Mid 2019):
            // 13-inch: MacBookPro13,2, MacBookPro14,2, MacBookPro15,2, MacBookPro15,4
            // 15-inch: MacBookPro13,3, MacBookPro14,3, MacBookPro15,1, MacBookPro15,3
            string[] noEscModels = new[]
            {
                "MacBookPro13,2", "MacBookPro13,3",
                "MacBookPro14,2", "MacBookPro14,3",
                "MacBookPro15,1", "MacBookPro15,2", "MacBookPro15,3", "MacBookPro15,4"
            };

            foreach (var m in noEscModels)
            {
                if (string.Equals(prodName, m, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // General prefix check for any 13.*, 14.*, 15.* MacBookPro
            if (prodName.StartsWith("MacBookPro13,", StringComparison.OrdinalIgnoreCase) ||
                prodName.StartsWith("MacBookPro14,", StringComparison.OrdinalIgnoreCase) ||
                prodName.StartsWith("MacBookPro15,", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool ShouldShowEsc(string mode)
        {
            if (string.Equals(mode, "AlwaysOn", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(mode, "AlwaysOff", StringComparison.OrdinalIgnoreCase))
                return false;

            // Default: "Auto"
            return IsLegacyTouchBarWithoutPhysicalEsc();
        }
    }

    public static class KeyboardInputHelper
    {
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        private const ushort VK_ESCAPE = 0x1B;
        private const ushort SCANCODE_ESCAPE = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public static void SendEscapeKey()
        {
            try
            {
                ushort scanCode = (ushort)MapVirtualKey(VK_ESCAPE, 0 /* MAPVK_VK_TO_VSC */);
                if (scanCode == 0) scanCode = SCANCODE_ESCAPE;

                INPUT[] inputs = new INPUT[2];

                // 1. Key Down (with ScanCode and Virtual Key)
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = VK_ESCAPE;
                inputs[0].u.ki.wScan = scanCode;
                inputs[0].u.ki.dwFlags = KEYEVENTF_SCANCODE;
                inputs[0].u.ki.time = 0;
                inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

                // 2. Key Up
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = VK_ESCAPE;
                inputs[1].u.ki.wScan = scanCode;
                inputs[1].u.ki.dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;
                inputs[1].u.ki.time = 0;
                inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            }
            catch { }
        }
    }
}
