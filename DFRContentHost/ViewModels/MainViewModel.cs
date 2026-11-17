using Avalonia.Controls;
using Avalonia.DfrFrameBuffer;
using Avalonia.Media;
using Avalonia.Threading;
using DFRContentHost.Interop;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace DFRContentHost.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        public static MainViewModel Instance { get; private set; }

        private bool _showVirtualEsc = false;
        public bool ShowVirtualEsc
        {
            get => _showVirtualEsc;
            set => this.RaiseAndSetIfChanged(ref _showVirtualEsc, value);
        }

        public FunctionRowButtonCollectionViewModel FnKeyViewModel { get; }
        public SystemMediaTransportControlViewModel SmtcViewModel { get; }
        public CompanionViewModel Companion { get; }

        private DispatcherTimer _dockRefreshTimer;
        private DispatcherTimer _animTimer;

        private double _expandedSliderWidth = 0;
        public double ExpandedSliderWidth
        {
            get => _expandedSliderWidth;
            set
            {
                this.RaiseAndSetIfChanged(ref _expandedSliderWidth, value);
                this.RaisePropertyChanged(nameof(IsAnySliderActive));
                this.RaisePropertyChanged(nameof(IsTelemetryVisible));
            }
        }

        public bool IsTelemetryVisible => _expandedSliderWidth < 30.0;
        public bool IsAnySliderActive => ExpandedSliderWidth > 0.5 || IsVolumeSliderActive || IsBrightnessSliderActive || IsPowerMenuActive;

        private bool _isVolumeSliderActive = false;
        public bool IsVolumeSliderActive
        {
            get => _isVolumeSliderActive;
            set
            {
                this.RaiseAndSetIfChanged(ref _isVolumeSliderActive, value);
                this.RaisePropertyChanged(nameof(IsAnySliderActive));
                this.RaisePropertyChanged(nameof(VolumeButtonBackground));
                this.RaisePropertyChanged(nameof(VolumeButtonForeground));
            }
        }

        private bool _isBrightnessSliderActive = false;
        public bool IsBrightnessSliderActive
        {
            get => _isBrightnessSliderActive;
            set
            {
                this.RaiseAndSetIfChanged(ref _isBrightnessSliderActive, value);
                this.RaisePropertyChanged(nameof(IsAnySliderActive));
                this.RaisePropertyChanged(nameof(BrightnessButtonBackground));
                this.RaisePropertyChanged(nameof(BrightnessButtonForeground));
            }
        }

        private bool _isPowerMenuActive = false;
        public bool IsPowerMenuActive
        {
            get => _isPowerMenuActive;
            set
            {
                this.RaiseAndSetIfChanged(ref _isPowerMenuActive, value);
                this.RaisePropertyChanged(nameof(IsAnySliderActive));
                this.RaisePropertyChanged(nameof(PowerButtonBackground));
                this.RaisePropertyChanged(nameof(PowerButtonForeground));
            }
        }

        private static readonly IBrush BrushNormalBg = new SolidColorBrush(Color.Parse("#262629"));
        private static readonly IBrush BrushActiveBg = new SolidColorBrush(Color.Parse("#3A3A3E"));
        private static readonly IBrush BrushNormalFg = new SolidColorBrush(Color.Parse("#F2F2F7"));
        private static readonly IBrush BrushMutedFg = new SolidColorBrush(Color.Parse("#AEAEB2"));
        private static readonly IBrush BrushVolumeActiveFg = new SolidColorBrush(Color.Parse("#007AFF"));
        private static readonly IBrush BrushBrightnessActiveFg = new SolidColorBrush(Color.Parse("#FFD60A"));
        private static readonly IBrush BrushMuteActiveFg = new SolidColorBrush(Color.Parse("#FF453A"));
        private static readonly IBrush BrushPowerActiveFg = new SolidColorBrush(Color.Parse("#FF9F0A"));
        private static readonly IBrush BrushWhite = new SolidColorBrush(Color.Parse("#FFFFFF"));
        private static readonly IBrush BrushMutedFill = new SolidColorBrush(Color.Parse("#555558"));
        private static readonly IBrush BrushShutdownNormalBg = new SolidColorBrush(Color.Parse("#262629"));
        private static readonly IBrush BrushShutdownConfirmBg = new SolidColorBrush(Color.Parse("#FF453A"));

        private static readonly IBrush BrushIconDark = new SolidColorBrush(Color.Parse("#1C1C1E"));
        private static readonly IBrush BrushIconWhite = new SolidColorBrush(Color.Parse("#FFFFFF"));

        public IBrush VolumeButtonBackground => IsVolumeSliderActive ? BrushActiveBg : BrushNormalBg;
        public IBrush VolumeButtonForeground => IsVolumeSliderActive ? BrushVolumeActiveFg : (IsMuted ? BrushMuteActiveFg : BrushNormalFg);
        public string VolumeButtonGlyph => IsMuted ? "\uE74F" : "\uE767";

        public IBrush BrightnessButtonBackground => IsBrightnessSliderActive ? BrushActiveBg : BrushNormalBg;
        public IBrush BrightnessButtonForeground => IsBrightnessSliderActive ? BrushBrightnessActiveFg : BrushNormalFg;

        public IBrush MuteButtonBackground => IsMuted ? BrushActiveBg : BrushNormalBg;
        public IBrush MuteButtonForeground => IsMuted ? BrushMuteActiveFg : BrushMutedFg;

        public IBrush PowerButtonBackground => IsPowerMenuActive ? BrushActiveBg : BrushNormalBg;
        public IBrush PowerButtonForeground => IsPowerMenuActive ? BrushPowerActiveFg : BrushNormalFg;

        private bool _isShutdownConfirming = false;
        public bool IsShutdownConfirming
        {
            get => _isShutdownConfirming;
            set
            {
                this.RaiseAndSetIfChanged(ref _isShutdownConfirming, value);
                this.RaisePropertyChanged(nameof(ShutdownButtonText));
                this.RaisePropertyChanged(nameof(ShutdownButtonBackground));
                this.RaisePropertyChanged(nameof(ShutdownButtonForeground));
            }
        }

        private string _effectiveLanguage = "ru";
        public string EffectiveLanguage
        {
            get => _effectiveLanguage;
            set
            {
                this.RaiseAndSetIfChanged(ref _effectiveLanguage, value);
                this.RaisePropertyChanged(nameof(ShutdownButtonText));
                this.RaisePropertyChanged(nameof(TouchIdCancelText));
                this.RaisePropertyChanged(nameof(TouchIdPromptText));
                this.RaisePropertyChanged(nameof(PowerLockText));
                this.RaisePropertyChanged(nameof(PowerRestartText));
            }
        }

        public string TouchIdCancelText => EffectiveLanguage == "ru" ? "Отмена" : "Cancel";
        public string TouchIdPromptText => EffectiveLanguage == "ru" ? "Отсканируйте отпечаток пальца" : "Scan fingerprint";
        public string PowerLockText => EffectiveLanguage == "ru" ? "Блок" : "Lock";
        public string PowerRestartText => EffectiveLanguage == "ru" ? "Рестарт" : "Restart";

        public string ShutdownButtonText
        {
            get
            {
                if (EffectiveLanguage == "ru")
                {
                    return IsShutdownConfirming ? "Точно?" : "Выкл";
                }
                return IsShutdownConfirming ? "Confirm?" : "Shut Down";
            }
        }
        public IBrush ShutdownButtonBackground => IsShutdownConfirming ? BrushShutdownConfirmBg : BrushShutdownNormalBg;
        public IBrush ShutdownButtonForeground => IsShutdownConfirming ? BrushWhite : BrushMuteActiveFg;

        private bool _isDockActive = true;
        public bool IsDockActive
        {
            get => _isDockActive;
            set => this.RaiseAndSetIfChanged(ref _isDockActive, value);
        }

        private double _mediaStripOpacity = 0.0;
        public double MediaStripOpacity
        {
            get => _mediaStripOpacity;
            set
            {
                this.RaiseAndSetIfChanged(ref _mediaStripOpacity, value);
                this.RaisePropertyChanged(nameof(IsMediaStripVisible));
            }
        }
        public bool IsMediaStripVisible => IsMediaWidgetEnabled && _mediaStripOpacity > 0.01;

        private double _dockOpacity = 1.0;
        public double DockOpacity
        {
            get => _dockOpacity;
            set
            {
                this.RaiseAndSetIfChanged(ref _dockOpacity, value);
                this.RaisePropertyChanged(nameof(IsDockVisible));
            }
        }
        public bool IsDockVisible => IsDockWidgetEnabled && _dockOpacity > 0.01;

        private double _mediaStripTranslateX = 0.0;
        public double MediaStripTranslateX
        {
            get => _mediaStripTranslateX;
            set => this.RaiseAndSetIfChanged(ref _mediaStripTranslateX, value);
        }

        private bool _userCollapsedMedia = false;

        public void CollapseMediaStrip()
        {
            _userCollapsedMedia = true;
            TransitionToMode(false);
        }

        public void ExpandMediaStrip()
        {
            _userCollapsedMedia = false;
            TransitionToMode(true);
        }

        private DispatcherTimer _modeCrossfadeTimer;
        private DateTime _modeCrossfadeStart;
        private bool _targetMediaActive = false;

        private void TransitionToMode(bool mediaActive)
        {
            _targetMediaActive = mediaActive;
            _modeCrossfadeStart = DateTime.UtcNow;

            if (_modeCrossfadeTimer == null)
            {
                _modeCrossfadeTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
                _modeCrossfadeTimer.Tick += (s, e) =>
                {
                    double elapsed = (DateTime.UtcNow - _modeCrossfadeStart).TotalMilliseconds;
                    double t = Math.Min(1.0, elapsed / 160.0);
                    double ease = 1.0 - Math.Pow(1.0 - t, 3.0);

                    if (_targetMediaActive)
                    {
                        MediaStripOpacity = ease;
                        MediaStripTranslateX = -60.0 * (1.0 - ease);
                        DockOpacity = 1.0 - ease;
                    }
                    else
                    {
                        MediaStripOpacity = 1.0 - ease;
                        MediaStripTranslateX = -60.0 * ease;
                        DockOpacity = ease;
                    }

                    if (t >= 1.0)
                    {
                        _modeCrossfadeTimer.Stop();
                        if (_targetMediaActive)
                        {
                            MediaStripOpacity = 1.0;
                            MediaStripTranslateX = 0.0;
                            DockOpacity = 0.0;
                            IsDockActive = false;
                        }
                        else
                        {
                            MediaStripOpacity = 0.0;
                            MediaStripTranslateX = -60.0;
                            DockOpacity = 1.0;
                            IsDockActive = true;
                        }
                    }

                    try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                };
            }
            _modeCrossfadeTimer.Start();
        }

        private float _volumeValue = 0.5f;
        public float VolumeValue
        {
            get => _volumeValue;
            set
            {
                this.RaiseAndSetIfChanged(ref _volumeValue, value);
                this.RaisePropertyChanged(nameof(VolumePercentText));
                this.RaisePropertyChanged(nameof(VolumeFillWidth));
                this.RaisePropertyChanged(nameof(VolumeKnobX));
                this.RaisePropertyChanged(nameof(VolumePillFillWidth));
                this.RaisePropertyChanged(nameof(VolumeCapsuleIconBrush));
                this.RaisePropertyChanged(nameof(VolumeCapsuleTextBrush));
            }
        }

        public string VolumePercentText => IsMuted ? "Mute" : $"{(int)(VolumeValue * 100)}%";
        public IBrush VolumeSliderFillBrush => IsMuted ? BrushMutedFill : BrushWhite;
        public IBrush VolumeSliderKnobBorder => IsMuted ? BrushMutedFill : BrushVolumeActiveFg;
        public double VolumeFillWidth => Math.Max(0.0, Math.Min(130.0, VolumeValue * 130.0));
        public double VolumeKnobX => Math.Max(0.0, Math.Min(116.0, VolumeFillWidth - 7.0));

        public double VolumePillFillWidth => Math.Max(0.0, Math.Min(202.0, VolumeValue * 202.0));
        public IBrush VolumeCapsuleIconBrush
        {
            get
            {
                if (IsMuted) return BrushMuteActiveFg;
                return VolumeValue > 0.12f ? BrushIconDark : BrushIconWhite;
            }
        }

        public IBrush VolumeCapsuleTextBrush
        {
            get
            {
                if (IsMuted) return BrushMutedFg;
                return VolumeValue > 0.88f ? BrushIconDark : BrushNormalFg;
            }
        }

        private int _brightnessValue = 70;
        public int BrightnessValue
        {
            get => _brightnessValue;
            set
            {
                this.RaiseAndSetIfChanged(ref _brightnessValue, value);
                this.RaisePropertyChanged(nameof(BrightnessPercentText));
                this.RaisePropertyChanged(nameof(BrightnessFillWidth));
                this.RaisePropertyChanged(nameof(BrightnessKnobX));
                this.RaisePropertyChanged(nameof(BrightnessPillFillWidth));
                this.RaisePropertyChanged(nameof(BrightnessCapsuleIconBrush));
                this.RaisePropertyChanged(nameof(BrightnessCapsuleTextBrush));
            }
        }

        public string BrightnessPercentText => $"{BrightnessValue}%";
        public double BrightnessFillWidth => Math.Max(0.0, Math.Min(110.0, (BrightnessValue / 100.0) * 110.0));
        public double BrightnessKnobX => Math.Max(0.0, Math.Min(96.0, BrightnessFillWidth - 7.0));

        public double BrightnessPillFillWidth => Math.Max(0.0, Math.Min(158.0, (BrightnessValue / 100.0) * 158.0));
        public IBrush BrightnessCapsuleIconBrush => BrightnessValue > 14 ? BrushIconDark : BrushIconWhite;
        public IBrush BrightnessCapsuleTextBrush => BrightnessValue > 85 ? BrushIconDark : BrushNormalFg;

        private bool _isAutoBrightnessActive = false;
        public bool IsAutoBrightnessActive
        {
            get => _isAutoBrightnessActive;
            set
            {
                this.RaiseAndSetIfChanged(ref _isAutoBrightnessActive, value);
                this.RaisePropertyChanged(nameof(AutoBrightnessButtonBg));
                this.RaisePropertyChanged(nameof(AutoBrightnessButtonFg));
            }
        }

        private static readonly IBrush BrushAutoBrightActiveBg = new SolidColorBrush(Color.Parse("#FFD60A"));
        private static readonly IBrush BrushAutoBrightInactiveBg = new SolidColorBrush(Color.Parse("#262629"));
        private static readonly IBrush BrushAutoBrightActiveFg = new SolidColorBrush(Color.Parse("#000000"));
        private static readonly IBrush BrushAutoBrightInactiveFg = new SolidColorBrush(Color.Parse("#8E8E93"));

        public IBrush AutoBrightnessButtonBg => IsAutoBrightnessActive ? BrushAutoBrightActiveBg : BrushAutoBrightInactiveBg;
        public IBrush AutoBrightnessButtonFg => IsAutoBrightnessActive ? BrushAutoBrightActiveFg : BrushAutoBrightInactiveFg;

        public void ToggleAutoBrightness()
        {
            bool newState = !IsAutoBrightnessActive;
            IsAutoBrightnessActive = newState;
            BrightnessHelper.SetAutoBrightness(newState);
            if (newState)
            {
                Task.Delay(300).ContinueWith(_ =>
                {
                    int val = BrightnessHelper.GetBrightness();
                    if (val >= 0)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            BrightnessValue = val;
                            try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                        });
                    }
                });
            }
            try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
        }

        private bool _isMuted = false;
        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                this.RaiseAndSetIfChanged(ref _isMuted, value);
                this.RaisePropertyChanged(nameof(MuteButtonBackground));
                this.RaisePropertyChanged(nameof(MuteButtonForeground));
                this.RaisePropertyChanged(nameof(VolumeButtonForeground));
                this.RaisePropertyChanged(nameof(VolumeButtonGlyph));
                this.RaisePropertyChanged(nameof(VolumeSliderFillBrush));
                this.RaisePropertyChanged(nameof(VolumeSliderKnobBorder));
                this.RaisePropertyChanged(nameof(VolumePercentText));
                this.RaisePropertyChanged(nameof(VolumeCapsuleIconBrush));
                this.RaisePropertyChanged(nameof(VolumeCapsuleTextBrush));
            }
        }

        public ObservableCollection<AppWindowInfo> RunningApps { get; }
        public TelemetryViewModel Telemetry { get; }

        // Adaptive Dock Tabs & Horizontal Touch Scrolling
        private double _availableDockWidth = 500.0;
        public double AvailableDockWidth
        {
            get => _availableDockWidth;
            set
            {
                if (Math.Abs(_availableDockWidth - value) > 1.0)
                {
                    this.RaiseAndSetIfChanged(ref _availableDockWidth, value);
                    RecalculateDockLayout();
                }
            }
        }

        private double _totalDockWidth = 500.0;
        public double TotalDockWidth
        {
            get => _totalDockWidth;
            set => this.RaiseAndSetIfChanged(ref _totalDockWidth, value);
        }

        private double _currentTabWidth = 140.0;
        public double CurrentTabWidth
        {
            get => _currentTabWidth;
            set => this.RaiseAndSetIfChanged(ref _currentTabWidth, value);
        }

        private double _dockScrollOffset = 0.0;
        public double DockScrollOffset
        {
            get => _dockScrollOffset;
            set
            {
                this.RaiseAndSetIfChanged(ref _dockScrollOffset, value);
                this.RaisePropertyChanged(nameof(DockTranslateX));
                UpdateFadeOpacities();
                try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
            }
        }

        public double DockTranslateX => -_dockScrollOffset;

        private double _maxScrollOffset = 0.0;
        public double MaxScrollOffset
        {
            get => _maxScrollOffset;
            set => this.RaiseAndSetIfChanged(ref _maxScrollOffset, value);
        }

        private bool _isScrollModeActive = false;
        public bool IsScrollModeActive
        {
            get => _isScrollModeActive;
            set => this.RaiseAndSetIfChanged(ref _isScrollModeActive, value);
        }

        private double _leftFadeOpacity = 0.0;
        public double LeftFadeOpacity
        {
            get => _leftFadeOpacity;
            set => this.RaiseAndSetIfChanged(ref _leftFadeOpacity, value);
        }

        private double _rightFadeOpacity = 0.0;
        public double RightFadeOpacity
        {
            get => _rightFadeOpacity;
            set => this.RaiseAndSetIfChanged(ref _rightFadeOpacity, value);
        }

        private bool _isMediaWidgetEnabled = true;
        public bool IsMediaWidgetEnabled
        {
            get => _isMediaWidgetEnabled;
            set => this.RaiseAndSetIfChanged(ref _isMediaWidgetEnabled, value);
        }

        private bool _isDockWidgetEnabled = true;
        public bool IsDockWidgetEnabled
        {
            get => _isDockWidgetEnabled;
            set => this.RaiseAndSetIfChanged(ref _isDockWidgetEnabled, value);
        }

        private bool _isBrightnessWidgetEnabled = true;
        public bool IsBrightnessWidgetEnabled
        {
            get => _isBrightnessWidgetEnabled;
            set => this.RaiseAndSetIfChanged(ref _isBrightnessWidgetEnabled, value);
        }

        private bool _isVolumeWidgetEnabled = true;
        public bool IsVolumeWidgetEnabled
        {
            get => _isVolumeWidgetEnabled;
            set => this.RaiseAndSetIfChanged(ref _isVolumeWidgetEnabled, value);
        }

        private bool _isClockWidgetEnabled = true;
        public bool IsClockWidgetEnabled
        {
            get => _isClockWidgetEnabled;
            set => this.RaiseAndSetIfChanged(ref _isClockWidgetEnabled, value);
        }

        private bool _isBatteryWidgetEnabled = true;
        public bool IsBatteryWidgetEnabled
        {
            get => _isBatteryWidgetEnabled;
            set
            {
                this.RaiseAndSetIfChanged(ref _isBatteryWidgetEnabled, value);
                this.RaisePropertyChanged(nameof(ShowBatteryWidget));
            }
        }
        public bool ShowBatteryWidget => IsBatteryWidgetEnabled && (Telemetry?.IsBatteryPresent ?? false);

        private bool _isCpuTempWidgetEnabled = true;
        public bool IsCpuTempWidgetEnabled
        {
            get => _isCpuTempWidgetEnabled;
            set
            {
                this.RaiseAndSetIfChanged(ref _isCpuTempWidgetEnabled, value);
                this.RaisePropertyChanged(nameof(ShowCpuTempWidget));
            }
        }
        public bool ShowCpuTempWidget => IsCpuTempWidgetEnabled && (Telemetry?.IsCpuTempAvailable ?? false);

        private bool _isPowerWidgetEnabled = true;
        public bool IsPowerWidgetEnabled
        {
            get => _isPowerWidgetEnabled;
            set => this.RaiseAndSetIfChanged(ref _isPowerWidgetEnabled, value);
        }

        private bool _isTouchIdOverlayVisible = false;
        public bool IsTouchIdOverlayVisible
        {
            get => _isTouchIdOverlayVisible;
            set
            {
                this.RaiseAndSetIfChanged(ref _isTouchIdOverlayVisible, value);
                this.RaisePropertyChanged(nameof(IsNormalModeVisible));
                if (value)
                {
                    StartTouchIdArrowAnimation();
                }
                else
                {
                    StopTouchIdArrowAnimation();
                }
            }
        }

        public bool IsNormalModeVisible => (FnKeyViewModel == null || !FnKeyViewModel.FnVisible) && !IsTouchIdOverlayVisible;

        private double _touchIdArrowOffsetX = 0.0;
        public double TouchIdArrowOffsetX
        {
            get => _touchIdArrowOffsetX;
            set => this.RaiseAndSetIfChanged(ref _touchIdArrowOffsetX, value);
        }

        private DispatcherTimer _touchIdAnimTimer;
        private DateTime _touchIdAnimStart;

        private void StartTouchIdArrowAnimation()
        {
            _touchIdAnimStart = DateTime.UtcNow;
            if (_touchIdAnimTimer == null)
            {
                _touchIdAnimTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(25)
                };
                _touchIdAnimTimer.Tick += (s, e) =>
                {
                    double elapsed = (DateTime.UtcNow - _touchIdAnimStart).TotalSeconds;
                    // Smooth pulsating nudge: 0.0 -> 5.5 px pointing right towards physical sensor
                    double wave = 0.5 * (1.0 - Math.Cos(elapsed * 2.0 * Math.PI * 1.5));
                    TouchIdArrowOffsetX = wave * 5.5;
                    try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                };
            }
            _touchIdAnimTimer.Start();
        }

        private void StopTouchIdArrowAnimation()
        {
            _touchIdAnimTimer?.Stop();
            TouchIdArrowOffsetX = 0.0;
        }

        public void CancelTouchIdPrompt()
        {
            TouchIdDetector.CancelPrompt();
            IsTouchIdOverlayVisible = false;
            try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
        }

        private DateTime _lastUserActivityTime = DateTime.UtcNow;
        private DispatcherTimer _sleepCheckTimer;

        public void NotifyUserActive()
        {
            _lastUserActivityTime = DateTime.UtcNow;
            if (Avalonia.DfrFrameBuffer.DfrDisplayPipeline.IsScreenSleeping)
            {
                Avalonia.DfrFrameBuffer.DfrDisplayPipeline.IsScreenSleeping = false;
                try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
            }
        }

        public void ApplyConfig(Models.AppConfig config)
        {
            if (config == null) return;

            EffectiveLanguage = config.GetEffectiveLanguage();
            TrayIconHelper.CurrentLanguage = EffectiveLanguage;

            ShowVirtualEsc = MacModelHelper.ShouldShowEsc(config.ShowEscButtonMode);

            Avalonia.DfrFrameBuffer.DfrDisplayPipeline.TouchBarBrightness = config.TouchBarBrightness;
            Avalonia.DfrFrameBuffer.DfrDisplayPipeline.SyncWithMainDisplayBrightness = config.SyncWithMainDisplayBrightness;
            Avalonia.DfrFrameBuffer.DfrDisplayPipeline.MainDisplayBrightness = BrightnessValue;

            CpuTemperatureMonitor.CurrentMode = config.CpuTempSource;

            if (config.Widgets != null)
            {
                foreach (var w in config.Widgets)
                {
                    switch (w.Id)
                    {
                        case "Media": IsMediaWidgetEnabled = w.Enabled; break;
                        case "Dock": IsDockWidgetEnabled = w.Enabled; break;
                        case "Brightness": IsBrightnessWidgetEnabled = w.Enabled; break;
                        case "Volume": IsVolumeWidgetEnabled = w.Enabled; break;
                        case "Clock": IsClockWidgetEnabled = w.Enabled; break;
                        case "Battery": IsBatteryWidgetEnabled = w.Enabled; break;
                        case "CpuTemp": IsCpuTempWidgetEnabled = w.Enabled; break;
                        case "Power": IsPowerWidgetEnabled = w.Enabled; break;
                    }
                }
            }

            try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
        }

        private DateTime _lastUserVolumeAdjust = DateTime.MinValue;
        private DateTime _lastUserBrightnessAdjust = DateTime.MinValue;

        public MainViewModel()
        {
            Instance = this;
            FnKeyViewModel = new FunctionRowButtonCollectionViewModel();
            FnKeyViewModel.WhenAnyValue(x => x.FnVisible)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(IsNormalModeVisible)));
            SmtcViewModel = new SystemMediaTransportControlViewModel();
            Companion = new CompanionViewModel(SmtcViewModel);
            RunningApps = new ObservableCollection<AppWindowInfo>();
            Telemetry = new TelemetryViewModel();
            InitScrollAnimation();

            SmtcViewModel.WhenAnyValue(x => x.IsSmtcActive)
                .Subscribe(active =>
                {
                    if (active)
                    {
                        if (!_userCollapsedMedia)
                        {
                            TransitionToMode(true);
                        }
                    }
                    else
                    {
                        _userCollapsedMedia = false;
                        TransitionToMode(false);
                    }
                });

            CoreAudioHelper.VolumeNotificationReceived += (vol, muted) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Ignore incoming OS notifications if user touched volume slider within last 1.5s
                    if ((DateTime.UtcNow - _lastUserVolumeAdjust).TotalMilliseconds > 1500)
                    {
                        VolumeValue = vol;
                        IsMuted = muted;
                        try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                    }
                });
            };

            BrightnessHelper.BrightnessNotificationReceived += (bright) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Ignore incoming WMI notifications if user touched brightness slider within last 2.0s
                    // to completely prevent feedback loops and jitter
                    if ((DateTime.UtcNow - _lastUserBrightnessAdjust).TotalMilliseconds > 2000)
                    {
                        BrightnessValue = bright;
                        try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                    }
                });
            };

            // Dock window refresh timer (1.5s interval for snappy app appearance)
            _dockRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _dockRefreshTimer.Tick += (s, e) => RefreshRunningApps();
            _dockRefreshTimer.Start();

            BrightnessHelper.Initialize();
            BrightnessValue = BrightnessHelper.GetBrightness();
            IsAutoBrightnessActive = BrightnessHelper.IsAutoBrightnessEnabled;
            RefreshRunningApps();

            // Load and subscribe to configuration changes
            ApplyConfig(Models.ConfigManager.Instance.Current);
            Models.ConfigManager.Instance.ConfigChanged += cfg => Dispatcher.UIThread.Post(() => ApplyConfig(cfg));

            KeyboardHookHelper.OnKeyPressed += NotifyUserActive;

            // Touch Bar OLED auto-off / sleep timer (1s interval)
            _sleepCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sleepCheckTimer.Tick += (s, e) =>
            {
                int timeout = Models.ConfigManager.Instance.Current.SleepTimeoutSeconds;
                if (timeout > 0)
                {
                    bool shouldSleep = (DateTime.UtcNow - _lastUserActivityTime).TotalSeconds >= timeout;
                    if (Avalonia.DfrFrameBuffer.DfrDisplayPipeline.IsScreenSleeping != shouldSleep)
                    {
                        Avalonia.DfrFrameBuffer.DfrDisplayPipeline.IsScreenSleeping = shouldSleep;
                        try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                    }
                }
                else if (Avalonia.DfrFrameBuffer.DfrDisplayPipeline.IsScreenSleeping)
                {
                    Avalonia.DfrFrameBuffer.DfrDisplayPipeline.IsScreenSleeping = false;
                    try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                }
            };
            _sleepCheckTimer.Start();

            // Subscribe to Windows Hello / Touch ID biometric prompt detection
            TouchIdDetector.TouchIdPromptStateChanged += (active, hwnd) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (active)
                    {
                        NotifyUserActive();
                        IsTouchIdOverlayVisible = true;
                    }
                    else
                    {
                        IsTouchIdOverlayVisible = false;
                    }
                    try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                });
            };
        }

        public void RefreshRunningApps()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var windows = WindowSwitcherHelper.GetRunningWindows();
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (windows.Count == RunningApps.Count)
                        {
                            bool identical = true;
                            for (int i = 0; i < windows.Count; i++)
                            {
                                if (windows[i].Handle != RunningApps[i].Handle ||
                                    windows[i].AppName != RunningApps[i].AppName)
                                {
                                    identical = false;
                                    break;
                                }
                            }
                            if (identical)
                            {
                                int activeIdx = -1;
                                for (int i = 0; i < windows.Count; i++)
                                {
                                    bool wasActive = RunningApps[i].IsActiveApp;
                                    RunningApps[i].IsActiveApp = windows[i].IsActiveApp;
                                    if (!wasActive && windows[i].IsActiveApp)
                                    {
                                        activeIdx = i;
                                    }
                                }
                                if (activeIdx >= 0)
                                {
                                    EnsureTabVisible(activeIdx);
                                }
                                return;
                            }
                        }

                        RunningApps.Clear();
                        int initialActiveIdx = -1;
                        for (int i = 0; i < windows.Count; i++)
                        {
                            var w = windows[i];
                            w.TabWidth = CurrentTabWidth;
                            if (w.IsActiveApp && initialActiveIdx < 0) initialActiveIdx = i;
                            RunningApps.Add(w);
                        }
                        RecalculateDockLayout();
                        if (initialActiveIdx >= 0)
                        {
                            EnsureTabVisible(initialActiveIdx);
                        }
                        try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                    });
                }
                catch
                {
                }
            });
        }

        public void RemoveApp(IntPtr hWnd)
        {
            Dispatcher.UIThread.Post(() =>
            {
                for (int i = 0; i < RunningApps.Count; i++)
                {
                    if (RunningApps[i].Handle == hWnd)
                    {
                        RunningApps.RemoveAt(i);
                        RecalculateDockLayout();
                        try
                        {
                            BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
                        }
                        catch { }
                        break;
                    }
                }
            });
        }

        public void UpdateAvailableDockWidth(double totalColumnWidth)
        {
            double reserved = 0.0;
            if (SmtcViewModel != null && SmtcViewModel.IsAudioActive)
            {
                reserved += 34.0; // Mini equalizer button
            }

            double available = Math.Max(100.0, totalColumnWidth - reserved - 6.0);
            AvailableDockWidth = available;
        }

        public void RecalculateDockLayout()
        {
            int count = RunningApps.Count;
            if (count == 0)
            {
                CurrentTabWidth = 140.0;
                TotalDockWidth = AvailableDockWidth;
                MaxScrollOffset = 0.0;
                IsScrollModeActive = false;
                DockScrollOffset = 0.0;
                UpdateFadeOpacities();
                return;
            }

            const double MaxTabWidth = 150.0;
            const double MinTabWidth = 70.0;
            const double TabMargin = 4.0; // 2px margin on left and right

            // Calculate natural tab width:
            double rawTabWidth = (AvailableDockWidth / count) - TabMargin;

            if (rawTabWidth >= MinTabWidth)
            {
                // Fits without scrolling!
                CurrentTabWidth = Math.Min(MaxTabWidth, Math.Floor(rawTabWidth));
                IsScrollModeActive = false;
                MaxScrollOffset = 0.0;
                DockScrollOffset = 0.0;
                TotalDockWidth = Math.Max(AvailableDockWidth, count * (CurrentTabWidth + TabMargin));
            }
            else
            {
                // Overflow: lock tab width at MinTabWidth and enable scroll
                CurrentTabWidth = MinTabWidth;
                IsScrollModeActive = true;
                double totalWidth = count * (MinTabWidth + TabMargin);
                TotalDockWidth = totalWidth;
                MaxScrollOffset = Math.Max(0.0, totalWidth - AvailableDockWidth);
                if (DockScrollOffset > MaxScrollOffset)
                {
                    DockScrollOffset = MaxScrollOffset;
                }
            }

            foreach (var app in RunningApps)
            {
                if (Math.Abs(app.TabWidth - CurrentTabWidth) > 0.5)
                {
                    app.TabWidth = CurrentTabWidth;
                }
            }

            UpdateFadeOpacities();
            try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
        }

        public void UpdateFadeOpacities()
        {
            if (!IsScrollModeActive || MaxScrollOffset <= 0.0)
            {
                LeftFadeOpacity = 0.0;
                RightFadeOpacity = 0.0;
                return;
            }

            LeftFadeOpacity = Math.Max(0.0, Math.Min(1.0, DockScrollOffset / 16.0));
            double remaining = MaxScrollOffset - DockScrollOffset;
            RightFadeOpacity = Math.Max(0.0, Math.Min(1.0, remaining / 16.0));
        }

        // Horizontal Touch Scroll & Inertia Animation
        private DispatcherTimer _scrollAnimTimer;
        private double _targetScrollOffset = 0.0;
        private double _scrollVelocity = 0.0;
        private bool _isAnimatingToTarget = false;
        private bool _isInertiaActive = false;

        private void InitScrollAnimation()
        {
            _scrollAnimTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _scrollAnimTimer.Tick += OnScrollAnimTick;
        }

        private void OnScrollAnimTick(object sender, EventArgs e)
        {
            if (_isAnimatingToTarget)
            {
                double diff = _targetScrollOffset - DockScrollOffset;
                if (Math.Abs(diff) < 0.6)
                {
                    DockScrollOffset = _targetScrollOffset;
                    _isAnimatingToTarget = false;
                    _scrollAnimTimer.Stop();
                }
                else
                {
                    DockScrollOffset += diff * 0.28;
                }
                try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
            }
            else if (_isInertiaActive)
            {
                DockScrollOffset -= _scrollVelocity * 0.016;
                _scrollVelocity *= 0.90;

                if (DockScrollOffset < 0.0)
                {
                    DockScrollOffset = 0.0;
                    _isInertiaActive = false;
                    _scrollAnimTimer.Stop();
                }
                else if (DockScrollOffset > MaxScrollOffset)
                {
                    DockScrollOffset = MaxScrollOffset;
                    _isInertiaActive = false;
                    _scrollAnimTimer.Stop();
                }
                else if (Math.Abs(_scrollVelocity) < 15.0)
                {
                    _isInertiaActive = false;
                    _scrollAnimTimer.Stop();
                }
                try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
            }
            else
            {
                _scrollAnimTimer.Stop();
            }
        }

        public void SmoothScrollTo(double targetOffset)
        {
            targetOffset = Math.Max(0.0, Math.Min(MaxScrollOffset, targetOffset));
            if (Math.Abs(targetOffset - DockScrollOffset) < 1.0)
            {
                DockScrollOffset = targetOffset;
                return;
            }

            _targetScrollOffset = targetOffset;
            _isAnimatingToTarget = true;
            _isInertiaActive = false;
            if (_scrollAnimTimer != null && !_scrollAnimTimer.IsEnabled)
            {
                _scrollAnimTimer.Start();
            }
        }

        public void StartInertia(double velocity)
        {
            if (Math.Abs(velocity) < 60.0) return;
            _scrollVelocity = velocity;
            _isInertiaActive = true;
            _isAnimatingToTarget = false;
            if (_scrollAnimTimer != null && !_scrollAnimTimer.IsEnabled)
            {
                _scrollAnimTimer.Start();
            }
        }

        public void StopScrollAnimation()
        {
            _isAnimatingToTarget = false;
            _isInertiaActive = false;
            _scrollAnimTimer?.Stop();
        }

        public void EnsureTabVisible(int index)
        {
            if (!IsScrollModeActive || index < 0 || index >= RunningApps.Count) return;

            const double TabMargin = 4.0;
            double tabWidth = CurrentTabWidth + TabMargin;
            double tabLeft = index * tabWidth;
            double tabRight = tabLeft + CurrentTabWidth;

            double viewLeft = DockScrollOffset;
            double viewRight = DockScrollOffset + AvailableDockWidth;

            if (tabLeft < viewLeft + 16.0)
            {
                double target = Math.Max(0.0, tabLeft - 20.0);
                SmoothScrollTo(target);
            }
            else if (tabRight > viewRight - 16.0)
            {
                double target = Math.Min(MaxScrollOffset, tabRight - AvailableDockWidth + 20.0);
                SmoothScrollTo(target);
            }
        }

        private double _animStartWidth = 0;
        private double _targetSliderWidth = 0;
        private DateTime _animStartTime = DateTime.UtcNow;
        private const double AnimDurationMs = 140.0;

        private void AnimateSlider(double target)
        {
            _animStartWidth = ExpandedSliderWidth;
            _targetSliderWidth = target;
            _animStartTime = DateTime.UtcNow;
            try { System.IO.File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] AnimateSlider: start={_animStartWidth}, target={target}\n"); } catch { }
            Companion?.NotifyUserInteracting();

            if (_animTimer == null)
            {
                _animTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
                _animTimer.Tick += (s, e) =>
                {
                    double elapsed = (DateTime.UtcNow - _animStartTime).TotalMilliseconds;
                    double t = Math.Min(1.0, elapsed / AnimDurationMs);

                    // High-performance smooth quartic ease-out
                    double ease = 1.0 - Math.Pow(1.0 - t, 4.0);
                    ExpandedSliderWidth = _animStartWidth + (_targetSliderWidth - _animStartWidth) * ease;
                    RecalculateDockLayout();

                    if (t >= 1.0)
                    {
                        ExpandedSliderWidth = _targetSliderWidth;
                        RecalculateDockLayout();
                        _animTimer.Stop();
                        if (ExpandedSliderWidth <= 0.01)
                        {
                            IsVolumeSliderActive = false;
                            IsBrightnessSliderActive = false;
                            IsPowerMenuActive = false;
                        }
                    }

                    try
                    {
                        Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
                    }
                    catch { }
                };
            }
            _animTimer.Stop();
            _animTimer.Start();
        }

        private DateTime _lastVolumeToggleTime = DateTime.MinValue;
        public void ToggleVolumeSlider()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastVolumeToggleTime).TotalMilliseconds < 450) return;
            _lastVolumeToggleTime = now;

            if (IsVolumeSliderActive && _targetSliderWidth > 0)
            {
                CloseSliders();
            }
            else
            {
                OpenVolumeSlider();
            }
        }

        public void OpenVolumeSlider()
        {
            CoreAudioHelper.EnsureActiveDevice(force: true);
            VolumeValue = CoreAudioHelper.GetMasterVolume();
            IsMuted = CoreAudioHelper.GetMute();
            IsVolumeSliderActive = true;
            IsBrightnessSliderActive = false;
            IsPowerMenuActive = false;
            this.RaisePropertyChanged(nameof(IsAnySliderActive));
            AnimateSlider(240);
        }

        public void SetVolume(float scalar)
        {
            _lastUserVolumeAdjust = DateTime.UtcNow;
            scalar = Math.Max(0.0f, Math.Min(1.0f, scalar));
            VolumeValue = scalar;

            // Automatically unmute if user actively slides volume up
            if (IsMuted && scalar > 0.01f)
            {
                IsMuted = false;
                CoreAudioHelper.SetMute(false);
            }

            CoreAudioHelper.SetMasterVolume(scalar);
        }

        public void ToggleMute()
        {
            _lastUserVolumeAdjust = DateTime.UtcNow;
            CoreAudioHelper.ToggleMute();
            IsMuted = CoreAudioHelper.GetMute();
            try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
        }

        private DateTime _lastBrightnessToggleTime = DateTime.MinValue;
        public void ToggleBrightnessSlider()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastBrightnessToggleTime).TotalMilliseconds < 450) return;
            _lastBrightnessToggleTime = now;

            if (IsBrightnessSliderActive && _targetSliderWidth > 0)
            {
                CloseSliders();
            }
            else
            {
                OpenBrightnessSlider();
            }
        }

        public void OpenBrightnessSlider()
        {
            BrightnessValue = BrightnessHelper.GetBrightness();
            IsAutoBrightnessActive = BrightnessHelper.IsAutoBrightnessEnabled;
            IsBrightnessSliderActive = true;
            IsVolumeSliderActive = false;
            IsPowerMenuActive = false;
            this.RaisePropertyChanged(nameof(IsAnySliderActive));
            AnimateSlider(240);
        }

        public void SetBrightness(int percent)
        {
            _lastUserBrightnessAdjust = DateTime.UtcNow;
            if (IsAutoBrightnessActive)
            {
                IsAutoBrightnessActive = false;
                BrightnessHelper.SetAutoBrightness(false);
            }
            percent = Math.Max(0, Math.Min(100, percent));
            BrightnessValue = percent;
            BrightnessHelper.SetBrightness(percent);
        }

        private int _powerMenuSessionId = 0;
        private int _shutdownConfirmSessionId = 0;

        public void ResetPowerMenuTimer()
        {
            int sessionId = ++_powerMenuSessionId;
            System.Threading.Tasks.Task.Delay(6000).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (sessionId == _powerMenuSessionId && IsPowerMenuActive)
                    {
                        try { System.IO.File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] Power menu auto-timeout triggered\n"); } catch { }
                        ClosePowerMenu();
                    }
                });
            });
        }

        private DateTime _lastPowerMenuToggleTime = DateTime.MinValue;
        public void TogglePowerMenu()
        {
            var now = DateTime.UtcNow;
            double elapsed = (now - _lastPowerMenuToggleTime).TotalMilliseconds;
            try { System.IO.File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] TogglePowerMenu: elapsed={elapsed:F0}, IsActive={IsPowerMenuActive}, targetWidth={_targetSliderWidth}\n"); } catch { }

            if (elapsed < 450) return;
            _lastPowerMenuToggleTime = now;

            if (IsPowerMenuActive && _targetSliderWidth > 0)
            {
                ClosePowerMenu();
            }
            else
            {
                OpenPowerMenu();
            }
        }

        public void OpenPowerMenu()
        {
            try { System.IO.File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] OpenPowerMenu!\n"); } catch { }
            IsShutdownConfirming = false;
            IsPowerMenuActive = true;
            IsVolumeSliderActive = false;
            IsBrightnessSliderActive = false;
            this.RaisePropertyChanged(nameof(IsAnySliderActive));
            AnimateSlider(240);
            ResetPowerMenuTimer();
        }

        public void ClosePowerMenu()
        {
            try { System.IO.File.AppendAllText(@"C:\Users\Gleb\.gemini\antigravity\scratch\DFRDisplayKm\click_log.txt", $"[{DateTime.Now:HH:mm:ss.fff}] ClosePowerMenu! from {new System.Diagnostics.StackTrace(1, false)}\n"); } catch { }
            ++_powerMenuSessionId;
            ++_shutdownConfirmSessionId;
            IsShutdownConfirming = false;
            CloseSliders();
        }

        public void TriggerLock()
        {
            ClosePowerMenu();
            NativeMethods.LockWorkStation();
        }

        public void TriggerRestart()
        {
            ClosePowerMenu();
            NativeMethods.RestartComputer();
        }

        public void TriggerSleep()
        {
            ClosePowerMenu();
            NativeMethods.SleepComputer();
        }

        public void HandleShutdownClick()
        {
            ResetPowerMenuTimer();
            if (!IsShutdownConfirming)
            {
                IsShutdownConfirming = true;
                int confirmSessionId = ++_shutdownConfirmSessionId;
                try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }

                System.Threading.Tasks.Task.Delay(3500).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (confirmSessionId == _shutdownConfirmSessionId && IsShutdownConfirming)
                        {
                            IsShutdownConfirming = false;
                            try { BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
                        }
                    });
                });
            }
            else
            {
                ++_shutdownConfirmSessionId;
                IsShutdownConfirming = false;
                ClosePowerMenu();
                NativeMethods.ShutdownComputer();
            }
        }

        public void CloseSliders()
        {
            ++_powerMenuSessionId;
            ++_shutdownConfirmSessionId;
            IsShutdownConfirming = false;
            AnimateSlider(0);
        }
    }
}
