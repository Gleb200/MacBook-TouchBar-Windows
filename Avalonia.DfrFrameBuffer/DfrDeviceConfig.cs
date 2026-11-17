using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Avalonia.DfrFrameBuffer
{
    public static class DfrDeviceConfig
    {
        public static int PhysicalWidth { get; }
        public static int PhysicalHeight => 60;
        public static double LogicalWidth => PhysicalWidth / 2.0;
        public static double LogicalHeight => 30.0;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, uint ulOptions, uint samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, int lpReserved, out uint lpType, StringBuilder lpData, ref uint lpcbData);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
        private const uint KEY_READ = 0x20019;

        static DfrDeviceConfig()
        {
            int width = 2008; // Default for MacBookPro16,1, 16,2, 16,3 (MacBooks with physical Esc key)
            try
            {
                if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, @"HARDWARE\DESCRIPTION\System\BIOS", 0, KEY_READ, out IntPtr hKey) == 0)
                {
                    uint size = 512;
                    StringBuilder sb = new StringBuilder(512);
                    if (RegQueryValueEx(hKey, "SystemProductName", 0, out _, sb, ref size) == 0)
                    {
                        string prod = sb.ToString();
                        // Older models (13,x, 14,x, 15,x) have 2170 width
                        if (prod.Contains("13,") || prod.Contains("14,") || prod.Contains("15,"))
                        {
                            width = 2170;
                        }
                        else if (prod.Contains("16,"))
                        {
                            width = 2008;
                        }
                    }
                    RegCloseKey(hKey);
                }
            }
            catch
            {
                width = 2008;
            }

            PhysicalWidth = width;
        }
    }
}
