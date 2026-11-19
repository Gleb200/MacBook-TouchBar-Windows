using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls.Platform.Surfaces;
using Avalonia.DfrFrameBuffer.Device;
using Avalonia.DfrFrameBuffer.Interop;
using Avalonia.Platform;

namespace Avalonia.DfrFrameBuffer
{
    public class BridgeFrameBuffer : IFramebufferPlatformSurface, IDisposable
    {
        private readonly Vector _dpi;
        private IntPtr _fd;

        public BridgeFrameBuffer(Vector? dpi = null)
        {
            _dpi = dpi ?? new Vector(192, 192);

            string instancePath = null;
            for (int retry = 0; retry < 15; retry++)
            {
                instancePath = Locator.FindDfrDevice();
                if (instancePath != null) break;
                System.Threading.Thread.Sleep(500);
            }

            if (instancePath == null)
            {
                // Attempt self-healing: start the DFRDisplayKm service and re-query
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("sc.exe", "start DFRDisplayKm")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    p?.WaitForExit(1000);
                }
                catch { }

                for (int retry = 0; retry < 6; retry++)
                {
                    System.Threading.Thread.Sleep(500);
                    instancePath = Locator.FindDfrDevice();
                    if (instancePath != null) break;
                }
            }

            if (instancePath == null)
            {
                throw new Exception("DFR Display Device not found");
            }

            for (int retry = 0; retry < 10; retry++)
            {
                _fd = NativeMethods.CreateFile(
                    instancePath, FileAccess.Write, FileShare.ReadWrite,
                    IntPtr.Zero, FileMode.Open, FileOptions.None,
                    IntPtr.Zero
                );
                if (_fd != IntPtr.Zero && _fd.ToInt64() != -1) break;

                // Also attempt with FileShare.None
                _fd = NativeMethods.CreateFile(
                    instancePath, FileAccess.Write, FileShare.None,
                    IntPtr.Zero, FileMode.Open, FileOptions.None,
                    IntPtr.Zero
                );
                if (_fd != IntPtr.Zero && _fd.ToInt64() != -1) break;

                System.Threading.Thread.Sleep(500);
            }

            if (_fd == IntPtr.Zero || _fd.ToInt64() == -1)
            {
                throw new Exception("Error: " + Marshal.GetLastWin32Error());
            }

            DfrDisplayPipeline.Initialize(_fd);
        }

        public Vector Dpi => _dpi;

        public ILockedFramebuffer Lock() => new LockedFrameBuffer(_dpi);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                DfrDisplayPipeline.Shutdown();
                NativeMethods.CloseHandle(_fd);

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~BridgeFrameBuffer()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
