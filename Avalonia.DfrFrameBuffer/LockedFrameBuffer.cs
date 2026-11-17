using Avalonia.DfrFrameBuffer.Device;
using Avalonia.Platform;
using System;

namespace Avalonia.DfrFrameBuffer
{
    class LockedFrameBuffer : ILockedFramebuffer
    {
        public LockedFrameBuffer(Vector dpi)
        {
            Dpi = dpi;
            Address = DfrDisplayPipeline.SkiaRenderBufferAddress;
        }

        public IntPtr Address { get; private set; }

        public PixelSize Size => new PixelSize(DfrDeviceConfig.PhysicalWidth, 60);

        public int RowBytes => DfrDeviceConfig.PhysicalWidth * 4;

        public Vector Dpi { get; }

        public PixelFormat Format => PixelFormat.Rgba8888;

        public void Dispose()
        {
            // Instantly notify pipeline that Skia finished rendering
            DfrDisplayPipeline.NotifyFrameRendered();
            Address = IntPtr.Zero;
        }
    }
}
