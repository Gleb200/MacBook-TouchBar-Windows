using System;
using Avalonia;
using Avalonia.Logging.Serilog;
using Avalonia.DfrFrameBuffer;
using Avalonia.Controls;
using Avalonia.Media;
using System.Linq;
using DFRContentHost.Internal;

namespace DFRContentHost
{
    class Program
    {
        private static System.Threading.Mutex _singleInstanceMutex;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        public static void Main(string[] args)
        {
            _singleInstanceMutex = new System.Threading.Mutex(true, "Local\\DFRContentHost_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                // Another instance is already running on this session
                return;
            }
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "crash.txt");
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] CRASH: {e.ExceptionObject}\n");
                }
                catch { }
            };

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try
                {
                    ViewModels.TelemetryViewModel.Shutdown();
                }
                catch { }
            };

            // Initialize configuration manager and Touch ID detector
            _ = Models.ConfigManager.Instance;
            Interop.TouchIdDetector.Start();

            try
            {
                var builder = AppBuilder.Configure<App>().UseReactiveUI();

                if (args.Contains("--self-host"))
                {
                    builder.UsePlatformDetect()
                        .UseDirect2D1()
                        .LogToDebug()
                        .Start<DebugHostedWindow>();
                }
                else
                {
                    builder.InitializeWithBridgeFramebuffer(tl =>
                    {
                        var grid = new Grid();
                        grid.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                        grid.Children.Add(new MainView());
                        tl.Content = grid;
                        tl.Width = DfrDeviceConfig.LogicalWidth;
                        tl.Height = DfrDeviceConfig.LogicalHeight;
                    });
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "crash.txt");
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now}] MAIN EXCEPTION: {ex}\n");
                }
                catch { }

                string err = ex.ToString();
                if (err.Contains("DFR Display Device not found"))
                {
                    try
                    {
                        MessageBox(IntPtr.Zero,
                            "Apple Touch Bar Display hardware device was not found!\n\n" +
                            "Please complete the initial setup on your MacBook Pro:\n" +
                            "1. Secure Boot must be turned OFF (macOS Recovery -> Startup Security Utility -> No Security)\n" +
                            "2. Windows Test Mode must be ON: open CMD as Administrator, run 'bcdedit -set TESTSIGNING ON', and REBOOT\n" +
                            "3. Run 'Install_Driver.bat' as Administrator to register the driver and certificate\n\n" +
                            "If the issue persists, run 'Restart_TouchBar.bat' to reset the USB controller.",
                            "Touch Bar for Windows — Driver Setup Required",
                            0x00000010 /* MB_ICONERROR */);
                    }
                    catch { }
                }

                throw;
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new Win32PlatformOptions
                {
                    AllowEglInitialization = true
                })
                .UseSkia()
                .UseReactiveUI()
                .LogToDebug();
    }
}
