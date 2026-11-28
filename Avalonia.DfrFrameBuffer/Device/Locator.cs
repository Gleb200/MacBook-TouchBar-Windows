using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Avalonia.DfrFrameBuffer.Interop;

namespace Avalonia.DfrFrameBuffer.Device
{
    public class Locator
    {
        static Guid DfrDisplayInterfaceGuid = Guid.Parse("2003cacd-9e7c-477c-ab06-a5a8bbb1a63e");

        public static string FindDfrDevice()
        {
            // 1. Поиск только реально активного (подключенного) устройства через SetupAPI с флагом DIGCF_PRESENT
            string path = EnumerateDeviceInterface(SetupAPI.DIGCF_PRESENT | SetupAPI.DIGCF_DEVICEINTERFACE);
            if (!string.IsNullOrEmpty(path)) return path;

            // Если устройство не найдено как активное, возвращаем null (чтобы вызывающая сторона знала о необходимости подождать или запустить службу)
            return null;
        }

        private static string EnumerateDeviceInterface(uint flags)
        {
            var h = SetupAPI.SetupDiGetClassDevs(
                ref DfrDisplayInterfaceGuid,
                IntPtr.Zero,
                IntPtr.Zero,
                flags
            );

            if (h == IntPtr.Zero || h.ToInt64() == -1) return null;

            try
            {
                uint i = 0;
                while (true)
                {
                    SP_DEVICE_INTERFACE_DATA dia = new SP_DEVICE_INTERFACE_DATA();
                    dia.cbSize = (uint)Marshal.SizeOf(dia);

                    bool bResult = SetupAPI.SetupDiEnumDeviceInterfaces(h, IntPtr.Zero, ref DfrDisplayInterfaceGuid, i, dia);
                    if (!bResult) break;

                    if (!SetupAPI.SetupDiGetDeviceInterfaceDetail(h, dia, IntPtr.Zero, 0, out uint nRequiredSize, null))
                    {
                        if (Marshal.GetLastWin32Error() != 122)
                        {
                            break;
                        }

                        var deviceInterfaceDetailData = Marshal.AllocHGlobal((int)nRequiredSize);
                        try
                        {
                            Marshal.WriteInt32(deviceInterfaceDetailData, (IntPtr.Size == 4) ? (4 + Marshal.SystemDefaultCharSize) : 8);
                            if (SetupAPI.SetupDiGetDeviceInterfaceDetail(h, dia, deviceInterfaceDetailData, nRequiredSize, out uint _, null))
                            {
                                IntPtr pDevicePathName = new IntPtr(deviceInterfaceDetailData.ToInt64() + 4);
                                string foundPath = Marshal.PtrToStringAuto(pDevicePathName);
                                if (!string.IsNullOrEmpty(foundPath))
                                {
                                    return foundPath;
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(deviceInterfaceDetailData);
                        }
                    }

                    i++;
                }
            }
            finally
            {
                // SetupDiDestroyDeviceInfoList
            }

            return null;
        }
    }
}
