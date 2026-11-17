using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DFRContentHost.Interop;
using DFRContentHost.ViewModels;
using System;
using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Native;

namespace DFRContentHost
{
    public class MainView : UserControl
    {
        private InputSimulator inputSimulator;
        private MainViewModel ViewModel => DataContext as MainViewModel;

        public MainView()
        {
            this.InitializeComponent();
            inputSimulator = new InputSimulator();
            this.DataContext = new MainViewModel();

            Avalonia.DfrFrameBuffer.Digitizer.TouchInterceptFilter = (x) =>
            {
                return ViewModel?.Companion?.HitTest(x) ?? false;
            };

            Avalonia.DfrFrameBuffer.Digitizer.TouchRawEvent = (x, type) =>
            {
                ViewModel?.NotifyUserActive();
                if (ViewModel?.Companion != null)
                {
                    if (type == 0) ViewModel.Companion.StartDrag(x);
                    else if (type == 1) ViewModel.Companion.UpdateDrag(x);
                    else if (type == 2) ViewModel.Companion.EndDrag();
                }
            };

            Avalonia.DfrFrameBuffer.Digitizer.TouchNonCompanionEvent = () =>
            {
                ViewModel?.NotifyUserActive();
                ViewModel?.Companion?.NotifyUserInteracting();
            };

            var rightControls = this.FindControl<StackPanel>("RightControlsPanel");
            double lastNotifiedAvailable = 0.0;
            this.LayoutUpdated += (s, e) =>
            {
                if (ViewModel != null)
                {
                    double totalWidth = this.Bounds.Width > 0 ? this.Bounds.Width : Avalonia.DfrFrameBuffer.DfrDeviceConfig.LogicalWidth;
                    if (totalWidth <= 0) totalWidth = 1004.0;

                    double escWidth = ViewModel.ShowVirtualEsc ? 64.0 : 0.0;
                    double rightWidth = (rightControls != null && rightControls.Bounds.Width > 0)
                        ? rightControls.Bounds.Width
                        : (ViewModel.IsAnySliderActive ? 490.0 : 255.0);

                    double dockAudioReserved = (ViewModel.SmtcViewModel != null && ViewModel.SmtcViewModel.IsAudioActive) ? 34.0 : 0.0;

                    double available = Math.Max(120.0, totalWidth - escWidth - rightWidth - dockAudioReserved - 10.0);
                    if (Math.Abs(available - lastNotifiedAvailable) > 2.0)
                    {
                        lastNotifiedAvailable = available;
                        ViewModel.AvailableDockWidth = available;
                    }
                }
            };

            var dockViewport = this.FindControl<Grid>("DockScrollViewport");
            if (dockViewport != null)
            {
                dockViewport.AddHandler(InputElement.PointerPressedEvent, OnDockPointerPressed, RoutingStrategies.Tunnel);
                dockViewport.AddHandler(InputElement.PointerMovedEvent, OnDockPointerMoved, RoutingStrategies.Tunnel);
                dockViewport.AddHandler(InputElement.PointerReleasedEvent, OnDockPointerReleased, RoutingStrategies.Tunnel);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnEscClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.NotifyUserActive();
            ViewModel?.Companion?.NotifyUserInteracting();
            KeyboardInputHelper.SendEscapeKey();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        // Power Menu Handlers
        private void OnPowerClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.TogglePowerMenu();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnClosePowerMenuClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.ClosePowerMenu();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnPowerLockClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.TriggerLock();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnPowerRestartClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.TriggerRestart();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnPowerSleepClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.TriggerSleep();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnPowerShutdownClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.HandleShutdownClick();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnLockClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            NativeMethods.LockWorkStation();
        }

        private void OnBrightnessClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.ToggleBrightnessSlider();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnVolumeClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.ToggleVolumeSlider();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnCloseSliderClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.CloseSliders();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private Point _volumeTouchDownPt;
        private bool _isVolumeSliding = false;
        private bool _volumeTouchOnSpeaker = false;

        private void OnVolumeCapsulePointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (sender is Border border && ViewModel != null)
            {
                var pt = e.GetPosition(border);

                if (e.RoutedEvent == InputElement.PointerPressedEvent)
                {
                    e.Device.Capture(border);
                    _volumeTouchDownPt = pt;
                    _isVolumeSliding = false;
                    _volumeTouchOnSpeaker = (pt.X <= 38.0);

                    if (!_volumeTouchOnSpeaker)
                    {
                        ApplyVolumeFromPoint(border, pt);
                    }
                }
                else if (e.RoutedEvent == InputElement.PointerMovedEvent)
                {
                    if (!_isVolumeSliding)
                    {
                        double deltaX = Math.Abs(pt.X - _volumeTouchDownPt.X);
                        double deltaY = Math.Abs(pt.Y - _volumeTouchDownPt.Y);
                        if (deltaX >= 6.0 || deltaY >= 6.0 || pt.X > 38.0)
                        {
                            _isVolumeSliding = true;
                        }
                    }

                    if (_isVolumeSliding || !_volumeTouchOnSpeaker)
                    {
                        ApplyVolumeFromPoint(border, pt);
                    }
                }
            }
        }

        private void ApplyVolumeFromPoint(Border border, Point pt)
        {
            double width = border.Bounds.Width;
            if (width > 0)
            {
                // 12px edge inset so finger comfortably reaches 100% and 0%
                const double Inset = 12.0;
                double effectiveWidth = Math.Max(1.0, width - (2.0 * Inset));
                double ratio = Math.Max(0.0, Math.Min(1.0, (pt.X - Inset) / effectiveWidth));
                if (!double.IsNaN(ratio))
                {
                    ViewModel.SetVolume((float)ratio);
                    Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
                }
            }
        }

        private void OnVolumeCapsulePointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (sender is Border border && ViewModel != null)
            {
                var pt = e.GetPosition(border);
                double deltaX = Math.Abs(pt.X - _volumeTouchDownPt.X);
                double deltaY = Math.Abs(pt.Y - _volumeTouchDownPt.Y);

                if (_volumeTouchOnSpeaker && !_isVolumeSliding && deltaX < 8.0 && deltaY < 8.0)
                {
                    ViewModel.ToggleMute();
                }
            }

            _isVolumeSliding = false;
            _volumeTouchOnSpeaker = false;
            e.Device.Capture(null);
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnBrightnessCapsulePointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (sender is Border border && ViewModel != null)
            {
                if (e.RoutedEvent == InputElement.PointerPressedEvent)
                {
                    e.Device.Capture(border);
                }

                double width = border.Bounds.Width;
                if (width > 0)
                {
                    var pt = e.GetPosition(border);
                    // 12px edge inset so finger comfortably reaches 100% and 0%
                    const double Inset = 12.0;
                    double effectiveWidth = Math.Max(1.0, width - (2.0 * Inset));
                    double ratio = Math.Max(0.0, Math.Min(1.0, (pt.X - Inset) / effectiveWidth));
                    if (!double.IsNaN(ratio))
                    {
                        ViewModel.SetBrightness((int)Math.Round(ratio * 100));
                        Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
                    }
                }
            }
        }

        private void OnBrightnessCapsulePointerReleased(object sender, PointerReleasedEventArgs e)
        {
            e.Device.Capture(null);
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnVolumeTrackPointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (sender is Border border && ViewModel != null)
            {
                if (e.RoutedEvent == InputElement.PointerPressedEvent)
                {
                    e.Device.Capture(border);
                }

                double width = border.Bounds.Width;
                if (width > 0)
                {
                    var pt = e.GetPosition(border);
                    const double Inset = 10.0;
                    double effectiveWidth = Math.Max(1.0, width - (2.0 * Inset));
                    double ratio = Math.Max(0.0, Math.Min(1.0, (pt.X - Inset) / effectiveWidth));
                    if (!double.IsNaN(ratio))
                    {
                        ViewModel.SetVolume((float)ratio);
                        Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
                    }
                }
            }
        }

        private void OnVolumeMaxPointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (ViewModel != null)
            {
                ViewModel.SetVolume(1.0f);
                Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
            }
        }

        private void OnVolumeMinPointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (ViewModel != null)
            {
                ViewModel.SetVolume(0.0f);
                Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
            }
        }

        private void OnBrightnessTrackPointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (sender is Border border && ViewModel != null)
            {
                if (e.RoutedEvent == InputElement.PointerPressedEvent)
                {
                    e.Device.Capture(border);
                }

                double width = border.Bounds.Width;
                if (width > 0)
                {
                    var pt = e.GetPosition(border);
                    const double Inset = 10.0;
                    double effectiveWidth = Math.Max(1.0, width - (2.0 * Inset));
                    double ratio = Math.Max(0.0, Math.Min(1.0, (pt.X - Inset) / effectiveWidth));
                    if (!double.IsNaN(ratio))
                    {
                        ViewModel.SetBrightness((int)Math.Round(ratio * 100));
                        Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
                    }
                }
            }
        }

        private void OnBrightnessMaxPointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (ViewModel != null)
            {
                ViewModel.SetBrightness(100);
                Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
            }
        }

        private void OnBrightnessMinPointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (ViewModel != null)
            {
                ViewModel.SetBrightness(10);
                Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
            }
        }

        private void OnScrubberTrackPointer(object sender, PointerEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            if (sender is Border border && ViewModel != null)
            {
                if (border.Bounds.Width > 0)
                {
                    var pt = e.GetPosition(border);
                    double ratio = Math.Max(0.0, Math.Min(1.0, pt.X / border.Bounds.Width));
                    if (!double.IsNaN(ratio))
                    {
                        ViewModel.SmtcViewModel.SeekToRatio(ratio);
                    }
                }
            }
        }

        private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.SmtcViewModel.TogglePlayPause();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnNextClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.SmtcViewModel.NextTrack();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnPrevClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.SmtcViewModel.PrevTrack();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnCollapseMediaClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.CollapseMediaStrip();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnExpandMediaClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.ExpandMediaStrip();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnStopTrackClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.SmtcViewModel?.StopTrack();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnAutoBrightnessClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.ToggleAutoBrightness();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnTouchIdCancelClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            ViewModel?.CancelTouchIdPrompt();
            Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
        }

        private void OnTaskViewClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.Companion?.NotifyUserInteracting();
            try
            {
                inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LWIN, VirtualKeyCode.TAB);
            }
            catch (Exception)
            {
            }
        }

        // Horizontal Touch Scroll & Drag/Tap Gesture Handling for Dock Tabs
        private bool _isDockPointerDown = false;
        private bool _isDockPanning = false;
        private Avalonia.Point _dockTouchStartPt;
        private Avalonia.Point _dockTouchLastPt;
        private DateTime _dockTouchStartTime;
        private DateTime _dockTouchLastTime;
        private double _dockTouchStartOffset;
        private double _dockScrollVelocity;

        private void OnDockPointerPressed(object sender, PointerPressedEventArgs e)
        {
            ViewModel?.NotifyUserActive();
            if (ViewModel == null || !ViewModel.IsDockVisible) return;

            var pt = e.GetPosition(this);

            _isDockPointerDown = true;
            _isDockPanning = false;
            _dockTouchStartPt = pt;
            _dockTouchLastPt = pt;
            _dockTouchStartTime = DateTime.UtcNow;
            _dockTouchLastTime = _dockTouchStartTime;
            _dockTouchStartOffset = ViewModel.DockScrollOffset;
            _dockScrollVelocity = 0.0;

            ViewModel.StopScrollAnimation();
        }

        private void OnDockPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_isDockPointerDown || ViewModel == null) return;

            var pt = e.GetPosition(this);
            double deltaX = pt.X - _dockTouchStartPt.X;

            if (!_isDockPanning && Math.Abs(deltaX) >= 8.0)
            {
                _isDockPanning = true;
                ViewModel.Companion?.NotifyUserInteracting();
                var viewport = this.FindControl<Grid>("DockScrollViewport");
                if (viewport != null)
                {
                    e.Device.Capture(viewport);
                }
            }

            if (_isDockPanning && ViewModel.IsScrollModeActive)
            {
                // Moving finger to left (negative deltaX) advances scroll offset (positive delta)
                double newOffset = _dockTouchStartOffset - deltaX;

                // Rubber-band resistance at boundaries
                if (newOffset < 0.0)
                {
                    newOffset = newOffset * 0.3;
                }
                else if (newOffset > ViewModel.MaxScrollOffset)
                {
                    double over = newOffset - ViewModel.MaxScrollOffset;
                    newOffset = ViewModel.MaxScrollOffset + (over * 0.3);
                }

                ViewModel.DockScrollOffset = newOffset;

                var now = DateTime.UtcNow;
                double dt = (now - _dockTouchLastTime).TotalSeconds;
                if (dt > 0.005)
                {
                    _dockScrollVelocity = (pt.X - _dockTouchLastPt.X) / dt;
                    _dockTouchLastTime = now;
                    _dockTouchLastPt = pt;
                }

                e.Handled = true;
                Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
            }
        }

        private void OnDockPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!_isDockPointerDown || ViewModel == null) return;
            _isDockPointerDown = false;

            if (_isDockPanning)
            {
                e.Device.Capture(null);
                if (ViewModel.IsScrollModeActive)
                {
                    e.Handled = true;

                    if (ViewModel.DockScrollOffset < 0.0)
                    {
                        ViewModel.SmoothScrollTo(0.0);
                    }
                    else if (ViewModel.DockScrollOffset > ViewModel.MaxScrollOffset)
                    {
                        ViewModel.SmoothScrollTo(ViewModel.MaxScrollOffset);
                    }
                    else
                    {
                        // Start smooth inertia deceleration
                        ViewModel.StartInertia(_dockScrollVelocity);
                    }
                }
            }
            _isDockPanning = false;
        }
    }
}
