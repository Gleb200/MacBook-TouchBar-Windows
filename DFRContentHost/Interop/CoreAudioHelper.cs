using System;
using System.Runtime.InteropServices;
using WindowsInput;
using WindowsInput.Native;

namespace DFRContentHost.Interop
{
    public static class CoreAudioHelper
    {
        private static IAudioEndpointVolume _endpointVolume;
        private static IAudioMeterInformation _audioMeter;
        private static IMMDeviceEnumerator _deviceEnumerator;
        private static NotificationClient _notificationClient;
        private static string _activeDeviceId = null;
        private static readonly object _endpointLock = new object();
        private static bool _comFailed = false;
        private static DateTime _lastDeviceCheck = DateTime.MinValue;

        public static readonly Guid OurEventContext = Guid.NewGuid();
        public static event Action<float, bool> VolumeNotificationReceived;

        private static AudioEndpointVolumeCallback _callbackInstance;

        static CoreAudioHelper()
        {
            EnsureActiveDevice(force: true);
        }

        public static void EnsureActiveDevice(bool force = false)
        {
            if (!force && _endpointVolume != null && (DateTime.UtcNow - _lastDeviceCheck).TotalMilliseconds < 400)
            {
                return;
            }
            if (!force && _comFailed && (DateTime.UtcNow - _lastDeviceCheck).TotalSeconds < 2)
            {
                return;
            }
            SwitchEndpoint(null, force);
        }

        private static void SwitchEndpoint(string targetDeviceId = null, bool force = false)
        {
            lock (_endpointLock)
            {
                _lastDeviceCheck = DateTime.UtcNow;
                try
                {
                    if (_deviceEnumerator == null)
                    {
                        _deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                        if (_notificationClient == null)
                        {
                            _notificationClient = new NotificationClient();
                            try
                            {
                                _deviceEnumerator.RegisterEndpointNotificationCallback(_notificationClient);
                            }
                            catch { }
                        }
                    }

                    IMMDevice newDevice = null;
                    if (!string.IsNullOrEmpty(targetDeviceId))
                    {
                        if (_deviceEnumerator.GetDevice(targetDeviceId, out newDevice) != 0)
                        {
                            newDevice = null;
                        }
                    }

                    if (newDevice == null)
                    {
                        // 0 = eRender, 0 = eConsole (Default Device)
                        if (_deviceEnumerator.GetDefaultAudioEndpoint(0, 0, out newDevice) != 0 || newDevice == null)
                        {
                            // Fallback: 0 = eRender, 1 = eMultimedia
                            _deviceEnumerator.GetDefaultAudioEndpoint(0, 1, out newDevice);
                        }
                    }

                    if (newDevice == null) return;

                    string newId = null;
                    try
                    {
                        newDevice.GetId(out newId);
                    }
                    catch { }

                    if (!force && _endpointVolume != null && newId != null && newId == _activeDeviceId)
                    {
                        Marshal.ReleaseComObject(newDevice);
                        return;
                    }

                    // Device has changed or forced refresh: clean up old endpoint volume callbacks
                    if (_endpointVolume != null)
                    {
                        try
                        {
                            if (_callbackInstance != null)
                            {
                                _endpointVolume.UnregisterControlChangeNotify(_callbackInstance);
                            }
                        }
                        catch { }

                        try { Marshal.ReleaseComObject(_endpointVolume); } catch { }
                        _endpointVolume = null;
                    }

                    if (_audioMeter != null)
                    {
                        try { Marshal.ReleaseComObject(_audioMeter); } catch { }
                        _audioMeter = null;
                    }

                    // Activate new endpoint volume
                    var guidVolume = typeof(IAudioEndpointVolume).GUID;
                    int hrVol = newDevice.Activate(ref guidVolume, 23, IntPtr.Zero, out var endpointObj);
                    if (hrVol == 0 && endpointObj is IAudioEndpointVolume vol)
                    {
                        _endpointVolume = vol;
                        _activeDeviceId = newId;
                        _comFailed = false;

                        if (_callbackInstance == null)
                        {
                            _callbackInstance = new AudioEndpointVolumeCallback();
                        }

                        try
                        {
                            _endpointVolume.RegisterControlChangeNotify(_callbackInstance);
                        }
                        catch { }

                        if (_endpointVolume.GetMasterVolumeLevelScalar(out float lvl) == 0)
                        {
                            _cachedMasterVolume = lvl;
                            _lastAppliedVolume = lvl;
                        }
                        if (_endpointVolume.GetMute(out bool mute) == 0)
                        {
                            _cachedMute = mute;
                        }

                        VolumeNotificationReceived?.Invoke(_cachedMasterVolume, _cachedMute);
                    }

                    // Activate new audio meter
                    try
                    {
                        var guidMeter = typeof(IAudioMeterInformation).GUID;
                        int hrMeter = newDevice.Activate(ref guidMeter, 23, IntPtr.Zero, out var meterObj);
                        if (hrMeter == 0 && meterObj is IAudioMeterInformation meter)
                        {
                            _audioMeter = meter;
                        }
                    }
                    catch { }

                    Marshal.ReleaseComObject(newDevice);
                }
                catch
                {
                    _comFailed = true;
                }
            }
        }

        private class NotificationClient : IMMNotificationClient
        {
            public int OnDeviceStateChanged(string pwstrDeviceId, int dwNewState)
            {
                if (pwstrDeviceId == _activeDeviceId)
                {
                    System.Threading.Tasks.Task.Run(() => SwitchEndpoint(null, force: true));
                }
                return 0;
            }

            public int OnDeviceAdded(string pwstrDeviceId) => 0;

            public int OnDeviceRemoved(string pwstrDeviceId)
            {
                if (pwstrDeviceId == _activeDeviceId)
                {
                    System.Threading.Tasks.Task.Run(() => SwitchEndpoint(null, force: true));
                }
                return 0;
            }

            public int OnDefaultDeviceChanged(int flow, int role, string pwstrDefaultDeviceId)
            {
                if (flow == 0 /* eRender */ && (role == 0 /* eConsole */ || role == 1 /* eMultimedia */))
                {
                    System.Threading.Tasks.Task.Run(() => SwitchEndpoint(pwstrDefaultDeviceId, force: true));
                }
                return 0;
            }

            public int OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) => 0;
        }

        private class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
        {
            public int OnNotify(IntPtr pNotifyData)
            {
                try
                {
                    if (pNotifyData != IntPtr.Zero)
                    {
                        var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotifyData);
                        bool isExternal = (data.guidEventContext != OurEventContext);
                        _cachedMasterVolume = data.fMasterVolume;
                        _cachedMute = data.bMuted;
                        if (isExternal)
                        {
                            _lastAppliedVolume = data.fMasterVolume;
                        }
                        VolumeNotificationReceived?.Invoke(data.fMasterVolume, data.bMuted);
                    }
                }
                catch { }
                return 0;
            }
        }

        private static float _cachedPeak = 0f;
        private static long _lastPeakTicks = 0;

        public static float GetAudioPeak()
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - _lastPeakTicks < TimeSpan.FromMilliseconds(35).Ticks)
            {
                return _cachedPeak;
            }

            EnsureActiveDevice();
            if (_audioMeter != null)
            {
                try
                {
                    if (_audioMeter.GetPeakValue(out float peak) == 0)
                    {
                        _cachedPeak = peak;
                        _lastPeakTicks = now;
                        return peak;
                    }
                }
                catch
                {
                    _audioMeter = null;
                }
            }
            return 0f;
        }

        private static float _cachedMasterVolume = 0.5f;

        public static float GetMasterVolume()
        {
            EnsureActiveDevice();
            if (_endpointVolume != null)
            {
                try
                {
                    if (_endpointVolume.GetMasterVolumeLevelScalar(out float level) == 0)
                    {
                        _cachedMasterVolume = level;
                        return level;
                    }
                }
                catch
                {
                    EnsureActiveDevice(force: true);
                }
            }
            return _cachedMasterVolume;
        }

        private static volatile float _targetVolume = -1f;
        private static readonly object _volumeLock = new object();
        private static bool _volumeWorkerRunning = false;
        private static float _lastAppliedVolume = -1f;

        public static void SetMasterVolume(float level)
        {
            level = Math.Max(0.0f, Math.Min(1.0f, level));
            _cachedMasterVolume = level;
            _targetVolume = level;

            lock (_volumeLock)
            {
                if (!_volumeWorkerRunning)
                {
                    _volumeWorkerRunning = true;
                    System.Threading.Tasks.Task.Run(VolumeWorkerLoop);
                }
            }
        }

        private static void VolumeWorkerLoop()
        {
            while (true)
            {
                float target;
                lock (_volumeLock)
                {
                    target = _targetVolume;
                }

                if (Math.Abs(target - _lastAppliedVolume) > 0.001f)
                {
                    EnsureActiveDevice();
                    if (_endpointVolume != null)
                    {
                        try
                        {
                            var guid = OurEventContext;
                            _endpointVolume.SetMasterVolumeLevelScalar(target, ref guid);
                            _lastAppliedVolume = target;
                        }
                        catch
                        {
                            EnsureActiveDevice(force: true);
                            if (_endpointVolume != null)
                            {
                                try
                                {
                                    var guid = OurEventContext;
                                    _endpointVolume.SetMasterVolumeLevelScalar(target, ref guid);
                                    _lastAppliedVolume = target;
                                }
                                catch { }
                            }
                        }
                    }
                }

                System.Threading.Thread.Sleep(20);

                lock (_volumeLock)
                {
                    if (Math.Abs(_targetVolume - _lastAppliedVolume) <= 0.001f)
                    {
                        _volumeWorkerRunning = false;
                        break;
                    }
                }
            }
        }

        private static bool _cachedMute = false;

        public static bool GetMute()
        {
            EnsureActiveDevice();
            if (_endpointVolume != null)
            {
                try
                {
                    if (_endpointVolume.GetMute(out bool mute) == 0)
                    {
                        _cachedMute = mute;
                        return mute;
                    }
                }
                catch
                {
                    EnsureActiveDevice(force: true);
                }
            }
            return _cachedMute;
        }

        public static void SetMute(bool mute)
        {
            _cachedMute = mute;
            EnsureActiveDevice();
            if (_endpointVolume != null)
            {
                try
                {
                    var guid = OurEventContext;
                    _endpointVolume.SetMute(mute, ref guid);
                    return;
                }
                catch
                {
                    EnsureActiveDevice(force: true);
                    if (_endpointVolume != null)
                    {
                        try
                        {
                            var guid = OurEventContext;
                            _endpointVolume.SetMute(mute, ref guid);
                            return;
                        }
                        catch { }
                    }
                }
            }

            try
            {
                new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.VOLUME_MUTE);
            }
            catch { }
        }

        public static void ToggleMute()
        {
            SetMute(!GetMute());
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
            [PreserveSig]
            int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
        }

        [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMNotificationClient
        {
            [PreserveSig]
            int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, int dwNewState);
            [PreserveSig]
            int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
            [PreserveSig]
            int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
            [PreserveSig]
            int OnDefaultDeviceChanged(int dataFlow, int role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
            [PreserveSig]
            int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PropertyKey key);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public int pid;
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            [PreserveSig]
            int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            [PreserveSig]
            int GetState(out int pdwState);
        }

        [Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolumeCallback
        {
            [PreserveSig]
            int OnNotify(IntPtr pNotifyData);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AUDIO_VOLUME_NOTIFICATION_DATA
        {
            public Guid guidEventContext;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bMuted;
            public float fMasterVolume;
            public uint nChannels;
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig]
            int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            [PreserveSig]
            int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
            [PreserveSig]
            int GetChannelCount(out uint pnChannelCount);
            [PreserveSig]
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig]
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            [PreserveSig]
            int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig]
            int GetMasterVolumeLevelScalar(out float pfLevel);
            [PreserveSig]
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig]
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            [PreserveSig]
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            [PreserveSig]
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
            [PreserveSig]
            int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            [PreserveSig]
            int VolumeStepUp(ref Guid pguidEventContext);
            [PreserveSig]
            int VolumeStepDown(ref Guid pguidEventContext);
            [PreserveSig]
            int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            [PreserveSig]
            int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }

        [Guid("C0221673-03B4-414D-B318-23D604C6FF3C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            [PreserveSig]
            int GetPeakValue(out float pfPeak);
            [PreserveSig]
            int GetMeteringChannelCount(out uint pnChannelCount);
            [PreserveSig]
            int GetChannelsPeakValues(uint u32ChannelCount, [In] IntPtr afPeakValues);
            [PreserveSig]
            int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        }
    }
}
