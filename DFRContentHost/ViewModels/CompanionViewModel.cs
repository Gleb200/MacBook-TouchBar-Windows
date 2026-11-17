using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DFRContentHost.Interop;
using ReactiveUI;
using System;

namespace DFRContentHost.ViewModels
{
    public class CompanionViewModel : ReactiveObject
    {
        public static CompanionViewModel Instance { get; private set; }
        public static int CurrentFrameTick { get; set; } = 0;

        private bool _isPetVisible = true;
        public bool IsPetVisible
        {
            get => _isPetVisible;
            set => this.RaiseAndSetIfChanged(ref _isPetVisible, value);
        }

        private Bitmap _companionImage;
        public Bitmap CompanionImage
        {
            get => _companionImage;
            set => this.RaiseAndSetIfChanged(ref _companionImage, value);
        }

        private double _positionX = 350.0;
        public double PositionX
        {
            get => _positionX;
            set
            {
                this.RaiseAndSetIfChanged(ref _positionX, value);
                this.RaisePropertyChanged(nameof(EmoteX));
            }
        }

        private double _positionY = 1.0;
        public double PositionY
        {
            get => _positionY;
            set => this.RaiseAndSetIfChanged(ref _positionY, value);
        }

        public double CharacterWidth => 34.0;
        public double CharacterHeight => 28.0;

        public double EmoteX => PositionX + 24.0;

        private Bitmap _emoteImage;
        public Bitmap EmoteImage
        {
            get => _emoteImage;
            set => this.RaiseAndSetIfChanged(ref _emoteImage, value);
        }

        private bool _isEmoteVisible = false;
        public bool IsEmoteVisible
        {
            get => _isEmoteVisible;
            set => this.RaiseAndSetIfChanged(ref _isEmoteVisible, value);
        }

        public bool IsDragging => _isDragging;

        private readonly SystemMediaTransportControlViewModel _smtc;
        private readonly DispatcherTimer _loopTimer;
        private readonly Random _random = new Random();

        private DateTime _lastKeystrokeTime = DateTime.MinValue;
        private DateTime _lastActivityTime = DateTime.UtcNow;
        private static DateTime _userInteractionUntil = DateTime.MinValue;
        public static bool IsUserInteracting => (DateTime.UtcNow < _userInteractionUntil);

        // Walking / physics variables
        private double _walkSpeed = 1.3;  // Normal strolling pace (~40 px/sec)
        private int _direction = 1;       // +1: right, -1: left
        private bool _isWalking = true;
        private bool _isPetPinned = false; // When true, stays stationary; moved only by finger
        private int _stateTicks = 0;
        private int _targetStateTicks = 140;

        private int _walkFrameIndex = 0;
        private int _frameTickCounter = 0;
        private double _musicPhase = 0.0;
        private int _danceFrame = 0;

        // Dragging & Interaction variables
        private bool _isDragging = false;
        private int _dragFrame = 0;

        private const double MinX = 10.0;
        public double GetMaxAllowedX()
        {
            if (MainViewModel.Instance != null && MainViewModel.Instance.IsAnySliderActive)
            {
                return 440.0;
            }
            return 660.0;
        }

        public CompanionViewModel(SystemMediaTransportControlViewModel smtc)
        {
            Instance = this;
            _smtc = smtc;

            // Preload and decode all animation frames into RAM immediately
            CompanionSpriteHelper.PreloadAllSprites();

            CompanionImage = CompanionSpriteHelper.GetSprite("idle_0");
            EmoteImage = CompanionSpriteHelper.GetSprite("emote_heart");

            KeyboardHookHelper.OnKeyPressed += HandleKeyPress;
            KeyboardHookHelper.StartHook();

            // 30 FPS update loop
            _loopTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _loopTimer.Tick += OnLoopTick;
            _loopTimer.Start();

            ApplyConfig(Models.ConfigManager.Instance.Current);
            Models.ConfigManager.Instance.ConfigChanged += cfg => Dispatcher.UIThread.Post(() => ApplyConfig(cfg));
        }

        private void ApplyConfig(Models.AppConfig cfg)
        {
            if (cfg == null) return;
            IsPetVisible = cfg.PetEnabled;
            _isPetPinned = cfg.PetPinned;
            _walkSpeed = 1.3 * Math.Max(0.2, cfg.PetSpeed);

            if (_isPetPinned)
            {
                _isWalking = false;
                if (cfg.PetPinnedX >= MinX && cfg.PetPinnedX <= GetMaxAllowedX())
                {
                    PositionX = cfg.PetPinnedX;
                }
            }

            if (!string.Equals(CompanionSpriteHelper.CurrentSkin, cfg.SelectedPetSkin, StringComparison.OrdinalIgnoreCase))
            {
                CompanionSpriteHelper.SetSkin(cfg.SelectedPetSkin);
                CompanionImage = CompanionSpriteHelper.GetSprite("idle_0");
            }
            try { Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
        }

        private void HandleKeyPress()
        {
            _lastKeystrokeTime = DateTime.UtcNow;
            _lastActivityTime = DateTime.UtcNow;
        }

        // Called when user touches controls (sliders, buttons, dock) on the Touch Bar
        public void NotifyUserInteracting()
        {
            _userInteractionUntil = DateTime.UtcNow.AddSeconds(3.0);
            _lastActivityTime = DateTime.UtcNow;
        }

        public bool HitTest(double touchX)
        {
            if (!IsPetVisible || !Models.ConfigManager.Instance.Current.PetReactToTouch)
            {
                return false;
            }

            // CRITICAL: NEVER intercept touches in the Control Strip, sliding drawers, or telemetry area!
            double stripThreshold = (MainViewModel.Instance != null && MainViewModel.Instance.IsAnySliderActive) ? 460.0 : 680.0;
            if (touchX >= stripThreshold)
            {
                return false;
            }

            // If user is currently touching or interacting with Touch Bar controls, do not steal touch
            if (IsUserInteracting)
            {
                return false;
            }

            return (touchX >= PositionX - 10.0 && touchX <= PositionX + CharacterWidth + 10.0);
        }

        public void StartDrag(double touchX)
        {
            double maxX = GetMaxAllowedX();
            _isDragging = true;
            _lastActivityTime = DateTime.UtcNow;
            PositionX = Math.Max(MinX, Math.Min(maxX, touchX - CharacterWidth / 2.0));
            PositionY = -2.0; // Lift off ground into hand
            _dragFrame = 0;

            string dragName = (_direction > 0) ? "drag_0" : "drag_0_flip";
            CompanionImage = CompanionSpriteHelper.GetSprite(dragName);
            EmoteImage = CompanionSpriteHelper.GetSprite("emote_heart");
            IsEmoteVisible = true;

            // Trigger immediate redraw
            try { Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
        }

        public void UpdateDrag(double touchX)
        {
            if (_isDragging)
            {
                double maxX = GetMaxAllowedX();
                _lastActivityTime = DateTime.UtcNow;
                PositionX = Math.Max(MinX, Math.Min(maxX, touchX - CharacterWidth / 2.0));

                _frameTickCounter++;
                if (_frameTickCounter % 2 == 0)
                {
                    _dragFrame = (_dragFrame + 1) % 3;
                    string dragName = (_direction > 0) ? $"drag_{_dragFrame}" : $"drag_{_dragFrame}_flip";
                    CompanionImage = CompanionSpriteHelper.GetSprite(dragName);
                }

                // High-FPS direct invalidate for silky smooth 60-120 FPS finger tracking
                try { Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
            }
        }

        public void EndDrag()
        {
            if (_isDragging)
            {
                _isDragging = false;
                PositionY = 1.0;
                IsEmoteVisible = false;

                if (_isPetPinned)
                {
                    _isWalking = false;
                    // Persist the user's dragged position to config
                    var cfg = Models.ConfigManager.Instance.Current;
                    if (cfg != null)
                    {
                        cfg.PetPinnedX = Math.Round(PositionX, 1);
                        Models.ConfigManager.Instance.Save(cfg);
                    }
                }
                else
                {
                    _isWalking = true;
                    _stateTicks = 0;
                    _targetStateTicks = _random.Next(90, 200);
                }

                try { Avalonia.DfrFrameBuffer.BridgeFrameBufferPlatform.TopLevel?.Invalidate(default); } catch { }
            }
        }

        private void OnLoopTick(object sender, EventArgs e)
        {
            var now = DateTime.UtcNow;

            // 0. OPTIMIZATION: If user is interacting with Touch Bar controls, freeze mascot to free 100% CPU
            if (now < _userInteractionUntil)
            {
                if (_isWalking)
                {
                    _isWalking = false;
                    _stateTicks = 0;
                    CompanionImage = CompanionSpriteHelper.GetSprite((_direction > 0) ? "idle_0" : "idle_0_flip");
                    IsEmoteVisible = false;
                }
                return;
            }

            bool isMusicActive = (_smtc != null && (_smtc.IsPlaying || _smtc.IsAudioActive));
            bool isTypingActive = (now - _lastKeystrokeTime).TotalMilliseconds < 900;

            _frameTickCounter++;
            CurrentFrameTick = _frameTickCounter;
            _stateTicks++;

            // 1. DRAGGING WITH FINGER (Held up by hand, flailing cute legs and flowing twin tails!)
            if (_isDragging)
            {
                if (_frameTickCounter % 4 == 0)
                {
                    _dragFrame = (_dragFrame + 1) % 3;
                }
                string dragName = (_direction > 0) ? $"drag_{_dragFrame}" : $"drag_{_dragFrame}_flip";
                CompanionImage = CompanionSpriteHelper.GetSprite(dragName);

                PositionY = -2.0;
                EmoteImage = CompanionSpriteHelper.GetSprite("emote_heart");
                IsEmoteVisible = true;
                return;
            }

            double currentMaxX = GetMaxAllowedX();
            if (PositionX > currentMaxX)
            {
                PositionX = currentMaxX;
                _direction = -1;
            }

            // 2. STATIC PINNED MODE: Stay stationary at pinned position, moved ONLY by finger
            if (_isPetPinned)
            {
                _isWalking = false;
                PositionX = Math.Max(MinX, Math.Min(currentMaxX, PositionX));
                PositionY = 1.0;
                int idleFrame = (_frameTickCounter / 4) % 20;
                string idleName = (_direction > 0) ? $"idle_{idleFrame}" : $"idle_{idleFrame}_flip";
                CompanionImage = CompanionSpriteHelper.GetSprite(idleName);
                return;
            }

            // 3. TYPING (Excited fast sprint / dash across Touch Bar with sparks!)
            if (isTypingActive)
            {
                double runSpeed = _walkSpeed * 2.3;
                PositionX += _direction * runSpeed;

                if (_frameTickCounter % 3 == 0)
                {
                    _walkFrameIndex = (_walkFrameIndex + 1) % 3;
                }

                string runFrame = (_direction > 0) ? $"walk_right_{_walkFrameIndex}" : $"walk_left_{_walkFrameIndex}";
                CompanionImage = CompanionSpriteHelper.GetSprite(runFrame);

                PositionY = (_frameTickCounter % 2 == 0) ? 0.0 : 1.0;
                EmoteImage = CompanionSpriteHelper.GetSprite("emote_spark");
                IsEmoteVisible = true;

                if (PositionX >= currentMaxX) { PositionX = currentMaxX; _direction = -1; }
                else if (PositionX <= MinX) { PositionX = MinX; _direction = 1; }
                return;
            }

            // 3. MUSIC ACTIVE: Dancing to the beat with floating musical notes!
            if (isMusicActive)
            {
                _lastActivityTime = now;
                _musicPhase += 0.22;
                if (_musicPhase > 10000.0) _musicPhase = 0.0;
                bool beat = Math.Sin(_musicPhase) > 0;

                EmoteImage = CompanionSpriteHelper.GetSprite("emote_note");
                IsEmoteVisible = true;

                // Dance cycle
                if (_frameTickCounter % 5 == 0)
                {
                    _danceFrame = (_danceFrame + 1) % 4;
                }
                string danceFrame = (_direction > 0) ? $"dance_{_danceFrame}" : $"dance_{_danceFrame}_flip";
                CompanionImage = CompanionSpriteHelper.GetSprite(danceFrame);

                // Rhythm bounce
                PositionY = beat ? 0.0 : 1.0;
                return;
            }
            else
            {
                IsEmoteVisible = false;
                PositionY = 1.0;
            }

            // 4. SLEEPING (AFK for > 45 seconds)
            if ((now - _lastActivityTime).TotalSeconds > 45.0)
            {
                int sleepFrame = ((_frameTickCounter / 15) % 2 == 0) ? 0 : 1;
                CompanionImage = CompanionSpriteHelper.GetSprite($"sleep_{sleepFrame}");
                EmoteImage = CompanionSpriteHelper.GetSprite("emote_sleep");
                IsEmoteVisible = true;
                return;
            }

            // 5. FREE ROAMING (Walking across the Touch Bar)
            if (_isWalking)
            {
                PositionX += _direction * _walkSpeed;

                if (_frameTickCounter % 4 == 0)
                {
                    _walkFrameIndex = (_walkFrameIndex + 1) % 4;
                }

                string walkFrame = (_direction > 0) ? $"walk_right_{_walkFrameIndex}" : $"walk_left_{_walkFrameIndex}";
                CompanionImage = CompanionSpriteHelper.GetSprite(walkFrame);

                // Walking step bounce for lively, natural motion across all characters
                if (string.Equals(CompanionSpriteHelper.CurrentSkin, "Mew", StringComparison.OrdinalIgnoreCase))
                {
                    PositionY = Math.Sin(_frameTickCounter * 0.22) * 2.0; // Mew floating wave
                }
                else
                {
                    PositionY = (_walkFrameIndex % 2 == 0) ? 0.0 : 1.5; // Step bounce
                }

                if (PositionX >= currentMaxX)
                {
                    PositionX = currentMaxX;
                    _direction = -1;
                    SwitchToRest();
                }
                else if (PositionX <= MinX)
                {
                    PositionX = MinX;
                    _direction = 1;
                    SwitchToRest();
                }
                else if (_stateTicks >= _targetStateTicks)
                {
                    SwitchToRest();
                }
            }
            else
            {
                // 6. STANDING IDLE (Gentle breathing, blinking, twin tails swaying, looking around!)
                int idleFrame = (_frameTickCounter / 4) % 20;
                string idleName = (_direction > 0) ? $"idle_{idleFrame}" : $"idle_{idleFrame}_flip";
                CompanionImage = CompanionSpriteHelper.GetSprite(idleName);

                if (_stateTicks >= _targetStateTicks)
                {
                    _isWalking = true;
                    _stateTicks = 0;
                    _targetStateTicks = _random.Next(90, 220); // 3 to 7 seconds

                    if (PositionX >= currentMaxX - 50) _direction = -1;
                    else if (PositionX <= MinX + 50) _direction = 1;
                    else if (_random.Next(100) < 40) _direction = -_direction;
                }
            }
        }

        private void SwitchToRest()
        {
            _isWalking = false;
            _stateTicks = 0;
            _targetStateTicks = _random.Next(70, 160); // 2 to 5 seconds
        }
    }
}
