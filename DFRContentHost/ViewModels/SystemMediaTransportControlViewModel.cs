using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DFRContentHost.Interop;
using ReactiveUI;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;
using WindowsInput;
using WindowsInput.Native;

namespace DFRContentHost.ViewModels
{
    public class SystemMediaTransportControlViewModel : ReactiveObject
    {
        private GlobalSystemMediaTransportControlsSession _glbSmtcSession;
        private GlobalSystemMediaTransportControlsSessionManager _glbSmtcMgr;
        private DispatcherTimer _progressTimer;
        private bool _isUserSeeking = false;

        private string _mediaTitle = "";
        public string MediaTitle
        {
            get => _mediaTitle;
            set
            {
                this.RaiseAndSetIfChanged(ref _mediaTitle, value);
                ResetMarquee();
            }
        }

        private double _titleScrollOffset = 0.0;
        public double TitleScrollOffset
        {
            get => _titleScrollOffset;
            set
            {
                this.RaiseAndSetIfChanged(ref _titleScrollOffset, value);
                this.RaisePropertyChanged(nameof(TitleScrollOffsetCombined));
            }
        }

        private double _titleSecondScrollOffset = 0.0;
        public double TitleSecondScrollOffset
        {
            get => _titleSecondScrollOffset;
            set
            {
                this.RaiseAndSetIfChanged(ref _titleSecondScrollOffset, value);
                this.RaisePropertyChanged(nameof(TitleSecondScrollOffsetCombined));
            }
        }

        private double _trackOpacity = 1.0;
        public double TrackOpacity
        {
            get => _trackOpacity;
            set => this.RaiseAndSetIfChanged(ref _trackOpacity, value);
        }

        private double _trackTranslateX = 0.0;
        public double TrackTranslateX
        {
            get => _trackTranslateX;
            set
            {
                this.RaiseAndSetIfChanged(ref _trackTranslateX, value);
                this.RaisePropertyChanged(nameof(TitleScrollOffsetCombined));
                this.RaisePropertyChanged(nameof(TitleSecondScrollOffsetCombined));
            }
        }

        public double TitleScrollOffsetCombined => TitleScrollOffset + TrackTranslateX;
        public double TitleSecondScrollOffsetCombined => TitleSecondScrollOffset + TrackTranslateX;

        private bool _isMarqueeActive = false;
        public bool IsMarqueeActive
        {
            get => _isMarqueeActive;
            set => this.RaiseAndSetIfChanged(ref _isMarqueeActive, value);
        }

        private double _continuousScrollPos = 0.0;

        public void ResetMarquee()
        {
            _continuousScrollPos = 0.0;
            TitleScrollOffset = 0.0;
            TitleSecondScrollOffset = 0.0;
            IsMarqueeActive = false;
            this.RaisePropertyChanged(nameof(TitleScrollOffsetCombined));
            this.RaisePropertyChanged(nameof(TitleSecondScrollOffsetCombined));
        }

        private string _mediaArtist = "";
        public string MediaArtist
        {
            get => _mediaArtist;
            set => this.RaiseAndSetIfChanged(ref _mediaArtist, value);
        }

        private Bitmap _bitmap;
        public Bitmap MediaThumbnail
        {
            get => _bitmap;
            set => this.RaiseAndSetIfChanged(ref _bitmap, value);
        }

        private bool _thumbnailAvailable;
        public bool ThumbnailAvailable
        {
            get => _thumbnailAvailable;
            set => this.RaiseAndSetIfChanged(ref _thumbnailAvailable, value);
        }

        private bool _isSmtcActive;
        public bool IsSmtcActive
        {
            get => _isSmtcActive;
            set => this.RaiseAndSetIfChanged(ref _isSmtcActive, value);
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set => this.RaiseAndSetIfChanged(ref _isPlaying, value);
        }

        private string _playPauseGlyph = "\uE102"; // Play icon
        public string PlayPauseGlyph
        {
            get => _playPauseGlyph;
            set => this.RaiseAndSetIfChanged(ref _playPauseGlyph, value);
        }

        private TimeSpan _position = TimeSpan.Zero;
        public TimeSpan Position
        {
            get => _position;
            set => this.RaiseAndSetIfChanged(ref _position, value);
        }

        private TimeSpan _duration = TimeSpan.Zero;
        public TimeSpan Duration
        {
            get => _duration;
            set => this.RaiseAndSetIfChanged(ref _duration, value);
        }

        private string _positionText = "00:00";
        public string PositionText
        {
            get => _positionText;
            set => this.RaiseAndSetIfChanged(ref _positionText, value);
        }

        private string _durationText = "00:00";
        public string DurationText
        {
            get => _durationText;
            set => this.RaiseAndSetIfChanged(ref _durationText, value);
        }

        private double _progressRatio = 0.0;
        public double ProgressRatio
        {
            get => _progressRatio;
            set
            {
                this.RaiseAndSetIfChanged(ref _progressRatio, value);
                this.RaisePropertyChanged(nameof(ScrubberFillWidth));
                this.RaisePropertyChanged(nameof(ScrubberKnobX));
            }
        }

        public double ScrubberFillWidth => Math.Max(0, Math.Min(120, 120 * ProgressRatio));
        public double ScrubberKnobX => Math.Max(0.0, Math.Min(108.0, ScrubberFillWidth - 6.0));

        private double _bar1Height = 3.0;
        public double Bar1Height
        {
            get => _bar1Height;
            set => this.RaiseAndSetIfChanged(ref _bar1Height, value);
        }

        private double _bar2Height = 3.0;
        public double Bar2Height
        {
            get => _bar2Height;
            set => this.RaiseAndSetIfChanged(ref _bar2Height, value);
        }

        private double _bar3Height = 3.0;
        public double Bar3Height
        {
            get => _bar3Height;
            set => this.RaiseAndSetIfChanged(ref _bar3Height, value);
        }

        private double _bar4Height = 3.0;
        public double Bar4Height
        {
            get => _bar4Height;
            set => this.RaiseAndSetIfChanged(ref _bar4Height, value);
        }

        private bool _isAudioActive = false;
        public bool IsAudioActive
        {
            get => _isAudioActive;
            set => this.RaiseAndSetIfChanged(ref _isAudioActive, value);
        }

        private DispatcherTimer _equalizerTimer;
        private double _eqPhase = 0.0;

        public SystemMediaTransportControlViewModel()
        {
            ResetSmtc();
            InitializeSmtcAsync();

            _progressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _progressTimer.Tick += OnProgressTimerTick;
            _progressTimer.Start();

            _equalizerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _equalizerTimer.Tick += OnEqualizerTick;
            _equalizerTimer.Start();
        }

        private void OnEqualizerTick(object sender, EventArgs e)
        {
            UpdateTitleMarquee();

            if (CompanionViewModel.IsUserInteracting) return;

            float peak = CoreAudioHelper.GetAudioPeak();
            bool active = (IsPlaying && IsSmtcActive) || (peak > 0.01f);

            IsAudioActive = active;

            if (!active)
            {
                if (Bar1Height > 3.01 || Bar2Height > 3.01 || Bar3Height > 3.01 || Bar4Height > 3.01)
                {
                    Bar1Height = Math.Max(3.0, Bar1Height * 0.82);
                    Bar2Height = Math.Max(3.0, Bar2Height * 0.82);
                    Bar3Height = Math.Max(3.0, Bar3Height * 0.82);
                    Bar4Height = Math.Max(3.0, Bar4Height * 0.82);
                }
                return;
            }

            _eqPhase += 0.28;
            if (_eqPhase > 10000.0) _eqPhase = 0.0;

            // Sensitivity curve: if SMTC is playing, ensure baseline energy even for quiet tracks
            double effectivePeak = Math.Max((double)peak, (IsPlaying && IsSmtcActive) ? 0.12 : 0.0);
            double amp = Math.Min(1.0, Math.Sqrt(effectivePeak) * 1.3);

            // Multiband harmonics for 4 vertical bars
            double wave1 = 0.45 + 0.45 * Math.Sin(_eqPhase * 1.7);
            double wave2 = 0.45 + 0.45 * Math.Sin(_eqPhase * 2.5 + 1.3);
            double wave3 = 0.45 + 0.45 * Math.Sin(_eqPhase * 3.3 + 2.6);
            double wave4 = 0.45 + 0.45 * Math.Cos(_eqPhase * 4.2 + 0.8);

            if (peak > 0.2f)
            {
                wave1 = Math.Min(1.0, wave1 + 0.25);
                wave3 = Math.Min(1.0, wave3 + 0.18);
            }

            double targetH1 = 3.0 + 8.5 * wave1 * amp;
            double targetH2 = 3.0 + 9.5 * wave2 * amp;
            double targetH3 = 3.0 + 9.0 * wave3 * amp;
            double targetH4 = 3.0 + 7.5 * wave4 * amp;

            Bar1Height = StepHeight(Bar1Height, targetH1);
            Bar2Height = StepHeight(Bar2Height, targetH2);
            Bar3Height = StepHeight(Bar3Height, targetH3);
            Bar4Height = StepHeight(Bar4Height, targetH4);
        }

        public static double MeasureTextWidth(string text, double fontSize = 11.0)
        {
            if (string.IsNullOrEmpty(text)) return 0.0;
            double scale = fontSize / 11.0;
            double width = 0.0;
            foreach (char c in text)
            {
                if (c == ' ' || c == '\t') width += 3.2;
                else if ("il1!.,:;'|`/\\[]()".IndexOf(c) >= 0) width += 3.2;
                else if ("fjrt-".IndexOf(c) >= 0) width += 4.5;
                else if ("mwMWЖШЩЮжшщю@#%&".IndexOf(c) >= 0) width += 8.6;
                else if (char.IsUpper(c)) width += 7.2;
                else if (char.IsDigit(c)) width += 6.2;
                else width += 5.8;
            }
            return width * scale;
        }

        private void UpdateTitleMarquee()
        {
            if (string.IsNullOrEmpty(MediaTitle))
            {
                if (TitleScrollOffset != 0.0 || TitleSecondScrollOffset != 0.0 || IsMarqueeActive)
                {
                    ResetMarquee();
                }
                return;
            }

            // Accurate character measurement to avoid false scrolling when text completely fits
            double titleWidth = MeasureTextWidth(MediaTitle, 11.0);
            double artistWidth = MeasureTextWidth(MediaArtist, 9.0);
            double maxTextWidth = Math.Max(titleWidth, artistWidth);
            const double containerWidth = 105.0;

            if (maxTextWidth <= containerWidth - 3.0)
            {
                if (IsMarqueeActive || TitleScrollOffset != 0.0)
                {
                    ResetMarquee();
                }
                return;
            }

            IsMarqueeActive = true;
            if (!IsPlaying) return;

            // Infinite continuous ticker tape without stopping
            const double Gap = 45.0;
            double loopLength = maxTextWidth + Gap;

            _continuousScrollPos += 0.8;
            if (_continuousScrollPos >= loopLength)
            {
                _continuousScrollPos -= loopLength;
            }

            TitleScrollOffset = -_continuousScrollPos;
            TitleSecondScrollOffset = -_continuousScrollPos + loopLength;
        }

        private static double StepHeight(double current, double target)
        {
            if (target > current)
            {
                return current + (target - current) * 0.65;
            }
            return current + (target - current) * 0.28;
        }

        private void OnProgressTimerTick(object sender, EventArgs e)
        {
            // If currently not playing or no session, periodically poll if any media began playing
            if (_glbSmtcMgr != null && (_glbSmtcSession == null || !IsPlaying))
            {
                try
                {
                    var sessions = _glbSmtcMgr.GetSessions();
                    if (sessions != null)
                    {
                        foreach (var s in sessions)
                        {
                            var info = s.GetPlaybackInfo();
                            if (info != null && info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                            {
                                DetachSession();
                                _glbSmtcSession = s;
                                _glbSmtcSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                                _glbSmtcSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                                _glbSmtcSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                                _ = LoadMediaPropertiesAsync();
                                UpdatePlaybackInfo();
                                UpdateTimelineInfo();
                                IsSmtcActive = true;
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            if (!IsSmtcActive || !IsPlaying || _isUserSeeking) return;

            if (Duration.TotalSeconds > 0)
            {
                var newPos = Position.Add(TimeSpan.FromMilliseconds(500));
                if (newPos > Duration) newPos = Duration;
                Position = newPos;
                PositionText = FormatTime(newPos);
                ProgressRatio = Math.Max(0.0, Math.Min(1.0, newPos.TotalSeconds / Duration.TotalSeconds));
            }
        }

        private void ResetSmtc()
        {
            ThumbnailAvailable = false;
            IsSmtcActive = false;
            IsPlaying = false;
            PlayPauseGlyph = "\uE102";
            MediaTitle = string.Empty;
            MediaArtist = string.Empty;
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            PositionText = "00:00";
            DurationText = "00:00";
            ProgressRatio = 0.0;
            Bar1Height = 3.0;
            Bar2Height = 3.0;
            Bar3Height = 3.0;
            Bar4Height = 3.0;
            IsAudioActive = false;
            TrackOpacity = 1.0;
            TrackTranslateX = 0.0;
            _trackChangePhase = 0;
            _trackChangeTimer?.Stop();
        }

        private async void InitializeSmtcAsync()
        {
            try
            {
                _glbSmtcMgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                AttachCurrentSession();
                _glbSmtcMgr.CurrentSessionChanged += OnSmtcSessionChanged;
                _glbSmtcMgr.SessionsChanged += OnSessionsChanged;
            }
            catch
            {
            }
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_glbSmtcSession == null || !IsPlaying)
                {
                    DetachSession();
                    AttachCurrentSession();
                }
            });
        }

        private void AttachCurrentSession()
        {
            if (_glbSmtcMgr == null) return;
            _glbSmtcSession = _glbSmtcMgr.GetCurrentSession();

            // If current session is null, find any playing session among all sessions
            if (_glbSmtcSession == null)
            {
                try
                {
                    var sessions = _glbSmtcMgr.GetSessions();
                    if (sessions != null)
                    {
                        foreach (var s in sessions)
                        {
                            var info = s.GetPlaybackInfo();
                            if (info != null && info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                            {
                                _glbSmtcSession = s;
                                break;
                            }
                        }
                        if (_glbSmtcSession == null && sessions.Count > 0)
                        {
                            _glbSmtcSession = sessions[0];
                        }
                    }
                }
                catch { }
            }

            if (_glbSmtcSession != null)
            {
                _glbSmtcSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _glbSmtcSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _glbSmtcSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;

                _ = LoadMediaPropertiesAsync();
                UpdatePlaybackInfo();
                UpdateTimelineInfo();
                IsSmtcActive = true;
            }
            else
            {
                ResetSmtc();
            }
        }

        private void DetachSession()
        {
            if (_glbSmtcSession != null)
            {
                try
                {
                    _glbSmtcSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    _glbSmtcSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    _glbSmtcSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
                }
                catch { }
                _glbSmtcSession = null;
            }
        }

        private void OnSmtcSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            Dispatcher.UIThread.Post(() =>
            {
                DetachSession();
                AttachCurrentSession();
            });
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            Dispatcher.UIThread.Post(UpdatePlaybackInfo);
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            Dispatcher.UIThread.Post(UpdateTimelineInfo);
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            Dispatcher.UIThread.Post(async () => await LoadMediaPropertiesAsync());
        }

        private void UpdatePlaybackInfo()
        {
            if (_glbSmtcSession == null) return;
            try
            {
                var info = _glbSmtcSession.GetPlaybackInfo();
                IsPlaying = (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
                PlayPauseGlyph = IsPlaying ? "\uE103" : "\uE102"; // \uE103 = Pause, \uE102 = Play
                IsSmtcActive = (info.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed &&
                                info.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped);
            }
            catch
            {
            }
        }

        private void UpdateTimelineInfo()
        {
            if (_glbSmtcSession == null || _isUserSeeking) return;
            try
            {
                var timeline = _glbSmtcSession.GetTimelineProperties();
                Position = timeline.Position;
                Duration = timeline.EndTime;
                PositionText = FormatTime(Position);
                DurationText = FormatTime(Duration);

                if (Duration.TotalSeconds > 0)
                {
                    ProgressRatio = Math.Max(0.0, Math.Min(1.0, Position.TotalSeconds / Duration.TotalSeconds));
                }
            }
            catch
            {
            }
        }

        private async Task LoadMediaPropertiesAsync()
        {
            if (_glbSmtcSession == null) return;
            try
            {
                var prop = await _glbSmtcSession.TryGetMediaPropertiesAsync();
                string newTitle = prop.Title ?? "";
                string newArtist = prop.Artist ?? "";
                Bitmap newThumb = null;
                bool newThumbAvailable = false;

                if (prop.Thumbnail != null)
                {
                    try
                    {
                        using var stream = await prop.Thumbnail.OpenReadAsync();
                        using var readStream = stream.AsStreamForRead();
                        newThumb = new Bitmap(readStream);
                        newThumbAvailable = true;
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(newTitle))
                {
                    IsSmtcActive = true;
                }

                if (!string.IsNullOrEmpty(MediaTitle) && (MediaTitle != newTitle || MediaArtist != newArtist))
                {
                    AnimateTrackChange(newTitle, newArtist, newThumb, newThumbAvailable);
                }
                else
                {
                    MediaTitle = newTitle;
                    MediaArtist = newArtist;
                    MediaThumbnail = newThumb;
                    ThumbnailAvailable = newThumbAvailable;
                    TrackOpacity = 1.0;
                    TrackTranslateX = 0.0;
                }
            }
            catch
            {
            }
        }

        private DispatcherTimer _trackChangeTimer;
        private DateTime _trackChangeStart;
        private int _trackChangePhase = 0; // 1 = fade out, 2 = slide/fade in
        private string _nextTitle;
        private string _nextArtist;
        private Bitmap _nextThumb;
        private bool _nextThumbAvailable;

        private void AnimateTrackChange(string title, string artist, Bitmap thumb, bool thumbAvail)
        {
            _nextTitle = title;
            _nextArtist = artist;
            _nextThumb = thumb;
            _nextThumbAvailable = thumbAvail;

            _trackChangePhase = 1;
            _trackChangeStart = DateTime.UtcNow;

            if (_trackChangeTimer == null)
            {
                _trackChangeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _trackChangeTimer.Tick += OnTrackChangeTick;
            }
            _trackChangeTimer.Start();
        }

        private void OnTrackChangeTick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.UtcNow - _trackChangeStart).TotalMilliseconds;
            if (_trackChangePhase == 1)
            {
                // Smooth fade & slide out (80 ms)
                double t = Math.Min(1.0, elapsed / 80.0);
                TrackOpacity = 1.0 - t;
                TrackTranslateX = -8.0 * t;

                if (t >= 1.0)
                {
                    MediaTitle = _nextTitle;
                    MediaArtist = _nextArtist;
                    MediaThumbnail = _nextThumb;
                    ThumbnailAvailable = _nextThumbAvailable;

                    _trackChangePhase = 2;
                    _trackChangeStart = DateTime.UtcNow;
                    TrackTranslateX = 10.0;
                }
            }
            else if (_trackChangePhase == 2)
            {
                // Smooth slide & fade in (130 ms with cubic ease-out)
                double t = Math.Min(1.0, elapsed / 130.0);
                double ease = 1.0 - Math.Pow(1.0 - t, 3.0);
                TrackOpacity = ease;
                TrackTranslateX = 10.0 * (1.0 - ease);

                if (t >= 1.0)
                {
                    TrackOpacity = 1.0;
                    TrackTranslateX = 0.0;
                    _trackChangeTimer.Stop();
                }
            }

            try
            {
                Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default);
            }
            catch { }
        }

        public async void TogglePlayPause()
        {
            if (_glbSmtcSession != null)
            {
                try
                {
                    await _glbSmtcSession.TryTogglePlayPauseAsync();
                    return;
                }
                catch { }
            }

            try
            {
                new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.MEDIA_PLAY_PAUSE);
            }
            catch { }
        }

        public async void NextTrack()
        {
            if (_glbSmtcSession != null)
            {
                try
                {
                    await _glbSmtcSession.TrySkipNextAsync();
                    return;
                }
                catch { }
            }

            try
            {
                new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.MEDIA_NEXT_TRACK);
            }
            catch { }
        }

        public async void PrevTrack()
        {
            if (_glbSmtcSession != null)
            {
                try
                {
                    await _glbSmtcSession.TrySkipPreviousAsync();
                    return;
                }
                catch { }
            }

            try
            {
                new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.MEDIA_PREV_TRACK);
            }
            catch { }
        }

        public async void StopTrack()
        {
            if (_glbSmtcSession != null)
            {
                try
                {
                    await _glbSmtcSession.TryStopAsync();
                }
                catch { }
            }

            try
            {
                new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.MEDIA_STOP);
            }
            catch { }

            IsPlaying = false;
            IsSmtcActive = false;
        }

        public void SeekToRatio(double ratio)
        {
            if (Duration.TotalSeconds <= 0) return;

            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            var target = TimeSpan.FromSeconds(Duration.TotalSeconds * ratio);

            Position = target;
            PositionText = FormatTime(target);
            ProgressRatio = ratio;

            if (_glbSmtcSession != null)
            {
                _isUserSeeking = true;
                Task.Run(async () =>
                {
                    try
                    {
                        await _glbSmtcSession.TryChangePlaybackPositionAsync(target.Ticks);
                    }
                    catch { }
                    finally
                    {
                        await Task.Delay(400);
                        _isUserSeeking = false;
                    }
                });
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
            }
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }
    }
}
