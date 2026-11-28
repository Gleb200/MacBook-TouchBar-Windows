using Avalonia.DfrFrameBuffer.Device;
using Avalonia.DfrFrameBuffer.Interop;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Avalonia.DfrFrameBuffer
{
    public class FnKeyNotifier
    {
        private IntPtr _fd;
        private Dispatcher _dispatcher;

        public event Action<RawInputEventArgs> Event;

        public FnKeyNotifier(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            try
            {
                var instancePath = Locator.FindDfrDevice();
                if (instancePath != null)
                {
                    _fd = NativeMethods.CreateFile(
                        instancePath, FileAccess.ReadWrite, FileShare.ReadWrite,
                        IntPtr.Zero, FileMode.Open, FileOptions.None,
                        IntPtr.Zero
                    );

                    if (_fd != IntPtr.Zero && _fd.ToInt64() != -1)
                    {
                        ThreadPool.UnsafeQueueUserWorkItem(_ => PollNextFnStatus(), null);
                    }
                }
            }
            catch { }
        }

        public void PollNextFnStatus()
        {
            bool lastPressed = false;
            while (true)
            {
                try
                {
                    var ioCtlResult = DfrHostIo.GetNextFnKeyStatus(_fd, out bool pressed);
                    if (ioCtlResult)
                    {
                        if (pressed != lastPressed)
                        {
                            lastPressed = pressed;
                            _dispatcher?.InvokeAsync(() => ProcessEvent(pressed), DispatcherPriority.Input);
                        }
                    }
                    else
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                }
                catch { }

                Thread.Sleep(10);
            }
        }

        private void ProcessEvent(bool pressed)
        {
            // We use F23 as the Fn indicator
            Event?.Invoke(new RawKeyEventArgs(
                BridgeFrameBufferPlatform.KbdDevice,
                BridgeFrameBufferPlatform.Timestamp,
                pressed ? RawKeyEventType.KeyDown : RawKeyEventType.KeyUp,
                Key.F23,
                InputModifiers.None));
        }
    }
}
