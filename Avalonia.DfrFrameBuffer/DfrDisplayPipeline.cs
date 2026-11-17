using Avalonia.DfrFrameBuffer.Device;
using Avalonia.DfrFrameBuffer.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Avalonia.DfrFrameBuffer
{
    public static unsafe class DfrDisplayPipeline
    {
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        public static volatile int TouchBarBrightness = 100;
        public static volatile bool SyncWithMainDisplayBrightness = false;
        public static volatile int MainDisplayBrightness = 100;
        public static volatile bool IsScreenSleeping = false;

        private static IntPtr _deviceHandle = IntPtr.Zero;
        private static Thread _renderThread;
        private static volatile bool _running = false;

        private static int _physicalWidth;
        private static int _bufferSize;
        private static int _requestSize;

        // Double Buffering:
        // 1. Skia renders into _skiaRenderBuffer on UI thread
        // 2. On Dispose(), _skiaRenderBuffer is copied to _frontBuffer (0.05 ms) and _hasNewFrame is set
        // 3. Dedicated AboveNormal worker thread copies _frontBuffer to _processingBuffer
        // 4. Compares _processingBuffer with _lastSentBuffer (fast 64-bit memcmp)
        // 5. If different, transposes into _ioctlRequestBuffer and sends to DFRDisplayKm!
        private static IntPtr _skiaRenderBuffer = IntPtr.Zero;
        private static IntPtr _frontBuffer = IntPtr.Zero;
        private static IntPtr _processingBuffer = IntPtr.Zero;
        private static IntPtr _lastSentBuffer = IntPtr.Zero;
        private static IntPtr _ioctlRequestBuffer = IntPtr.Zero;

        private static readonly object _frameLock = new object();
        private static volatile bool _hasNewFrame = false;
        private static bool _hasSentAnyFrame = false;

        public static IntPtr SkiaRenderBufferAddress => _skiaRenderBuffer;

        public static void Initialize(IntPtr deviceHandle)
        {
            if (_running) return;

            _deviceHandle = deviceHandle;
            _physicalWidth = DfrDeviceConfig.PhysicalWidth;
            _bufferSize = _physicalWidth * 60 * 4;

            int headerSize = sizeof(DFR_HOSTIO_UPDATE_FRAMEBUFFER_HEADER);
            _requestSize = _physicalWidth * 60 * 3 + headerSize;

            _skiaRenderBuffer = Marshal.AllocHGlobal(_bufferSize);
            _frontBuffer = Marshal.AllocHGlobal(_bufferSize);
            _processingBuffer = Marshal.AllocHGlobal(_bufferSize);
            _lastSentBuffer = Marshal.AllocHGlobal(_bufferSize);
            _ioctlRequestBuffer = Marshal.AllocHGlobal(_requestSize);

            // Zero out buffers
            new Span<byte>((void*)_skiaRenderBuffer, _bufferSize).Clear();
            new Span<byte>((void*)_frontBuffer, _bufferSize).Clear();
            new Span<byte>((void*)_processingBuffer, _bufferSize).Clear();
            new Span<byte>((void*)_lastSentBuffer, _bufferSize).Clear();
            new Span<byte>((void*)_ioctlRequestBuffer, _requestSize).Clear();

            _hasNewFrame = false;
            _hasSentAnyFrame = false;
            _running = true;

            _renderThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "DFRDisplayKm-Pipeline"
            };
            _renderThread.Start();
        }

        public static void NotifyFrameRendered()
        {
            if (!_running || _skiaRenderBuffer == IntPtr.Zero || _frontBuffer == IntPtr.Zero) return;

            lock (_frameLock)
            {
                Buffer.MemoryCopy((void*)_skiaRenderBuffer, (void*)_frontBuffer, _bufferSize, _bufferSize);
                _hasNewFrame = true;
            }
        }

        public static void Shutdown()
        {
            _running = false;
            if (_renderThread != null && _renderThread.IsAlive)
            {
                _renderThread.Join(500);
            }

            if (_skiaRenderBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_skiaRenderBuffer); _skiaRenderBuffer = IntPtr.Zero; }
            if (_frontBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_frontBuffer); _frontBuffer = IntPtr.Zero; }
            if (_processingBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_processingBuffer); _processingBuffer = IntPtr.Zero; }
            if (_lastSentBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_lastSentBuffer); _lastSentBuffer = IntPtr.Zero; }
            if (_ioctlRequestBuffer != IntPtr.Zero) { Marshal.FreeHGlobal(_ioctlRequestBuffer); _ioctlRequestBuffer = IntPtr.Zero; }
        }

        private static void WorkerLoop()
        {
            TimeBeginPeriod(1);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                // Locked 30 FPS timing (33.333 ms per frame)
                long targetTicksPerFrame = Stopwatch.Frequency / 30;
                long nextTargetTicks = sw.ElapsedTicks;

                while (_running)
                {
                    long nowTicks = sw.ElapsedTicks;
                    long remainingTicks = nextTargetTicks - nowTicks;
                    if (remainingTicks > 0)
                    {
                        long remainingMs = (remainingTicks * 1000) / Stopwatch.Frequency;
                        if (remainingMs > 2)
                        {
                            Thread.Sleep((int)(remainingMs - 1));
                        }
                        while (sw.ElapsedTicks < nextTargetTicks)
                        {
                            Thread.SpinWait(8);
                        }
                    }

                    nextTargetTicks += targetTicksPerFrame;
                    if (sw.ElapsedTicks > nextTargetTicks + targetTicksPerFrame)
                    {
                        nextTargetTicks = sw.ElapsedTicks + targetTicksPerFrame;
                    }

                    ProcessAndTransmit();
                }
            }
            finally
            {
                TimeEndPeriod(1);
            }
        }

        private static void ProcessAndTransmit()
        {
            if (_deviceHandle == IntPtr.Zero) return;

            bool hasNew;
            lock (_frameLock)
            {
                hasNew = _hasNewFrame;
                if (hasNew)
                {
                    Buffer.MemoryCopy((void*)_frontBuffer, (void*)_processingBuffer, _bufferSize, _bufferSize);
                    _hasNewFrame = false;
                }
            }

            // 1. If no frame was rendered by UI, skip transmission completely
            if (!hasNew) return;

            // 2. Dirty Check: Compare with last sent frame (fast 64-bit memcmp)
            if (_hasSentAnyFrame && IsBufferIdentical((byte*)_processingBuffer, (byte*)_lastSentBuffer, _bufferSize))
            {
                // Screen content has not visually changed. Skip DeviceIoControl to save USB bandwidth on T2 chip!
                return;
            }

            // 3. Transpose into IOCTL buffer
            int wCount = _physicalWidth;
            int headerSize = sizeof(DFR_HOSTIO_UPDATE_FRAMEBUFFER_HEADER);

            DFR_HOSTIO_UPDATE_FRAMEBUFFER_HEADER* pHeader = (DFR_HOSTIO_UPDATE_FRAMEBUFFER_HEADER*)_ioctlRequestBuffer.ToPointer();
            pHeader->BeginX = 0;
            pHeader->BeginY = 0;
            pHeader->Width = (ushort)wCount;
            pHeader->Height = 60;
            pHeader->FrameBufferPixelFormat = DfrHostIo.DFR_FRAMEBUFFER_FORMAT;
            pHeader->RequireVertFlip = 0;

            byte* dst = (byte*)_ioctlRequestBuffer.ToPointer() + headerSize;
            byte* pFbContent = (byte*)_processingBuffer.ToPointer();

            float scale = 1.0f;
            if (IsScreenSleeping)
            {
                scale = 0.0f;
            }
            else if (SyncWithMainDisplayBrightness)
            {
                scale = Math.Max(0.1f, Math.Min(1.0f, MainDisplayBrightness / 100.0f));
            }
            else
            {
                scale = Math.Max(0.1f, Math.Min(1.0f, TouchBarBrightness / 100.0f));
            }

            int stride = -(wCount * 4);
            if (scale < 0.999f)
            {
                for (int w = 0; w < wCount; w++)
                {
                    byte* p = pFbContent + (59 * wCount + w) * 4;
                    for (int h = 0; h < 60; h++)
                    {
                        *dst++ = (byte)(p[0] * scale);
                        *dst++ = (byte)(p[1] * scale);
                        *dst++ = (byte)(p[2] * scale);
                        p += stride;
                    }
                }
            }
            else
            {
                for (int w = 0; w < wCount; w++)
                {
                    byte* p = pFbContent + (59 * wCount + w) * 4;
                    for (int h = 0; h < 60; h++)
                    {
                        *dst++ = p[0];
                        *dst++ = p[1];
                        *dst++ = p[2];
                        p += stride;
                    }
                }
            }

            // 4. Send to DFRDisplayKm driver on this AboveNormal priority thread
            bool res = NativeMethods.DeviceIoControl(
                _deviceHandle,
                DfrHostIo.IOCTL_DFR_UPDATE_FRAMEBUFFER,
                _ioctlRequestBuffer,
                _requestSize,
                IntPtr.Zero,
                0,
                IntPtr.Zero,
                IntPtr.Zero
            );

            if (res)
            {
                Buffer.MemoryCopy((void*)_processingBuffer, (void*)_lastSentBuffer, _bufferSize, _bufferSize);
                _hasSentAnyFrame = true;
            }
        }

        private static bool IsBufferIdentical(byte* buf1, byte* buf2, int length)
        {
            ulong* p1 = (ulong*)buf1;
            ulong* p2 = (ulong*)buf2;
            int ulongCount = length / 8;
            for (int i = 0; i < ulongCount; i++)
            {
                if (p1[i] != p2[i]) return false;
            }
            int remainder = length % 8;
            if (remainder > 0)
            {
                byte* b1 = buf1 + (ulongCount * 8);
                byte* b2 = buf2 + (ulongCount * 8);
                for (int i = 0; i < remainder; i++)
                {
                    if (b1[i] != b2[i]) return false;
                }
            }
            return true;
        }
    }
}
