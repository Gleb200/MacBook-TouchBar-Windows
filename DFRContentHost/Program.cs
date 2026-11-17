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
                    Interop.TrayIconHelper.Stop();
                }
                catch { }
            };

            // Initialize configuration manager, system tray icon and Touch ID detector
            _ = Models.ConfigManager.Instance;
            Interop.TrayIconHelper.Start();
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
