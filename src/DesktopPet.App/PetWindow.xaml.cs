using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopPet.App.Models;
using DesktopPet.App.Native;
using DesktopPet.App.Services;
using Forms = System.Windows.Forms;

namespace DesktopPet.App;

public partial class PetWindow : Window
{
    private static readonly bool UseHudReferencePreview = false;

    private enum PetAnimationState
    {
        Idle,
        OneShot,
        LoopingAction,
        DragStart,
        DragHold,
        Drop,
        Study,
        Feeding,
        Sleeping,
        Chatting
    }

    private readonly string _assetRoot;
    private AnimationCatalog _catalog;
    private HitTestService _hitTest;
    private readonly DialogueSelector _dialogue;
    private readonly JsonStore<PetState> _stateStore = new("pet-state.json");
    private readonly JsonStore<UserSettings> _settingsStore = new("user-settings.json");
    private readonly JsonStore<StudyRecord> _studyRecordStore = new("study-record.json");
    private readonly DefaultPetInteractionStateReducer _reducer = new();
    private readonly DefaultBehaviorScheduler _behavior = new();
    private readonly StudySessionService _study = new();
    private readonly PetActivityStateMachine _activity = new();

    private PetState _state;
    private UserSettings _settings;
    private StudyRecord _studyRecord;

    private readonly DispatcherTimer _frameTimer = new();
    private readonly DispatcherTimer _idleTimer = new();
    private readonly DispatcherTimer _hideBubbleTimer = new();
    private readonly DispatcherTimer _hideHudTimer = new();
    private readonly DispatcherTimer _studyTimer = new();
    private readonly DispatcherTimer _dragHoldSwitchTimer = new();
    private readonly DispatcherTimer _hudEffectTimer = new();

    private AnimationSpec? _activeAnimation;
    private int _frameIndex;
    private DateTimeOffset _animationStartedAt;
    private bool _returnToIdleAfterOneShot;
    private BitmapSource? _currentBitmap;
    private string _currentAnimationId = "idle_m8";
    private string _currentFeedingAnimationId = "feed_snack";
    private PetAnimationState _animationState = PetAnimationState.Idle;

    private CancellationTokenSource? _crossfadeCts;
    private const double CrossfadeDurationMs = 150;
    private const double CrossfadeStepMs = 15;
    private const int DragStartToHoldDelayMs = 160;
    private PropLayerService? _propLayer;
    private MotionSequenceService? _motionSeq;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private Forms.NotifyIcon? _notifyIcon;

    private bool _pressed;
    private bool _dragging;
    private Point _pressScreenPoint;
    private Point _lastDragScreenPoint;
    private PetHitRegion _pressedRegion = PetHitRegion.None;

    private PetHitRegion _lastTapRegion = PetHitRegion.None;
    private DateTimeOffset _lastTapAt = DateTimeOffset.MinValue;
    private int _repeatCount;
    private DateTimeOffset _lastIdleBubbleAt = DateTimeOffset.MinValue;
    private readonly Queue<string> _recentIdleAnimations = new();
    private readonly Random _random = new();
    private readonly List<HudSnowflake> _hudSnowflakes = new();
    private readonly List<Shape> _hudMouseLinks = new();
    private readonly DateTimeOffset _hudReferencePreviewStartedAt = DateTimeOffset.Now;
    private Point? _lastHudMousePosition;
    private int _hudEffectFrame;
    private string _activeHudPage = "Home";
    private LearningKind _selectedLearningKind = LearningKind.Reading;

    private sealed class HudSnowflake
    {
        public required TextBlock Visual { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Speed { get; init; }
        public double Drift { get; init; }
        public double Phase { get; init; }
        public double LinkRadius { get; init; }
    }

    public PetWindow()
    {
        InitializeComponent();
        _assetRoot = System.IO.Path.Combine(AppContext.BaseDirectory, "assets");
        _catalog = AnimationCatalog.Load(_assetRoot);
        _catalog.Preload("idle_m8", "hover_m8", "drag_start", "drag_hold", "drop", "pat_head_m8", "face_reaction_m8", "clasp_idle_m8", "tap_annoyed", "hand_invite_m8", "study_guard_m8", "sleepy_m8", "plush_hug_m8", "talking", "feed_snack", "feed_meal", "rest_tea", "idle_cheer_m8", "photo_m8", "draw_m8", "tongue_m8");
        _hitTest = new HitTestService(_catalog.HitRegions);
        _dialogue = DialogueSelector.Load(System.IO.Path.Combine(_assetRoot, "dialogue", "blue-girl.zh-CN.json"));
        _propLayer = new PropLayerService(PropLayer, _assetRoot);
        _propLayer.LoadManifest();
        _motionSeq = new MotionSequenceService(_assetRoot);
        _motionSeq.LoadManifest();

        _state = _stateStore.Load();
        _settings = _settingsStore.Load();
        _studyRecord = _studyRecordStore.Load();

        _frameTimer.Tick += FrameTimer_Tick;
        _idleTimer.Tick += IdleTimer_Tick;
        _hideBubbleTimer.Tick += (_, _) => HideBubbleIfNotPinned();
        _hideHudTimer.Tick += (_, _) => HudPanel.Visibility = Visibility.Collapsed;
        _studyTimer.Tick += StudyTimer_Tick;
        _dragHoldSwitchTimer.Tick += DragHoldSwitchTimer_Tick;
        _hudEffectTimer.Tick += HudEffectTimer_Tick;

        _idleTimer.Interval = TimeSpan.FromSeconds(1);
        _hideBubbleTimer.Interval = TimeSpan.FromSeconds(4);
        _hideHudTimer.Interval = TimeSpan.FromMilliseconds(650);
        _studyTimer.Interval = TimeSpan.FromSeconds(1);
        _dragHoldSwitchTimer.Interval = TimeSpan.FromMilliseconds(DragStartToHoldDelayMs);
        _hudEffectTimer.Interval = TimeSpan.FromMilliseconds(33);
    }

    private void PetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? Forms.Screen.AllScreens[0].WorkingArea;
        Left = area.Left + Math.Max(16, (area.Width - Width) / 2);
        Top = area.Top + 36;
        ApplySettingsToWindow();
        LoadChatSettingsIntoUi();
        CreateTrayIcon();
        PlayAnimation("idle_m8", returnToIdle: false);
        LoadHudAvatar();
        ShowBubble(_dialogue.Pick("startup"), seconds: 4);
        SetHudPage("Home");
        UpdateLearningKindButtons();
        HudPanel.Visibility = Visibility.Visible;
        PositionBubbleAndHud();
        InitializeHudEffects();
        _idleTimer.Start();
        _studyTimer.Start();
        _hudEffectTimer.Start();
        UpdateStatusText();
    }

    private void PetWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        NativeWindowInterop.SetToolWindow(_hwnd);
        ApplySettingsToWindow();
    }

    private void PetWindow_Closing(object? sender, CancelEventArgs e)
    {
        _state.Memory.LastOpenedAt = DateTimeOffset.Now;
        SaveAll();
        _crossfadeCts?.Cancel();
        _motionSeq?.Cancel();
        _dragHoldSwitchTimer.Stop();
        _hudEffectTimer.Stop();
        _propLayer?.HideAllProps();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _source?.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeWindowInterop.WM_NCHITTEST) return IntPtr.Zero;

        var screenPoint = NativeWindowInterop.ScreenPointFromLParam(lParam);
        if (IsScreenPointInside(HudPanel, screenPoint) || IsScreenPointInside(BubbleBorder, screenPoint))
        {
            handled = true;
            return new IntPtr(NativeWindowInterop.HTCLIENT);
        }

        if (_settings.ClickThrough)
        {
            handled = true;
            return new IntPtr(NativeWindowInterop.HTTRANSPARENT);
        }

        if (IsScreenPointOnOpaquePet(screenPoint))
        {
            handled = true;
            return new IntPtr(NativeWindowInterop.HTCLIENT);
        }

        handled = true;
        return new IntPtr(NativeWindowInterop.HTTRANSPARENT);
    }

    private void PlayAnimation(string id, bool returnToIdle = true)
    {
        if (!_catalog.Has(id)) id = _catalog.DefaultAnimation;
        var nextState = ResolveAnimationState(id, returnToIdle);
        if (!CanSwitchTo(nextState))
            return;

        if (id == _currentAnimationId && _activeAnimation?.Loop == true)
            return;

        _frameTimer.Stop();
        _crossfadeCts?.Cancel();
        PetSprite.Opacity = 1;
        PetSpriteOverlay.Visibility = Visibility.Collapsed;

        _activeAnimation = _catalog.Get(id);
        _currentAnimationId = id;
        _frameIndex = 0;
        _animationStartedAt = DateTimeOffset.Now;
        _returnToIdleAfterOneShot = returnToIdle && id != "idle_m8";
        _animationState = nextState;

        var firstFrame = _catalog.LoadFrame(_activeAnimation, 0);
        _currentBitmap = firstFrame;

        if (UsesInstantAnimationSwitch(id))
        {
            PetSprite.Source = firstFrame;
            StartFrameTimer();
            return;
        }

        _crossfadeCts = new CancellationTokenSource();
        var token = _crossfadeCts.Token;
        _ = CrossfadeAndStartAsync(firstFrame, token);
    }

    private static bool UsesInstantAnimationSwitch(string id) =>
        id is "drag_start" or "drag_hold" or "drop";

    private PetAnimationState ResolveAnimationState(string id, bool returnToIdle)
    {
        if (id == "idle_m8") return PetAnimationState.Idle;
        if (id == "drag_start") return PetAnimationState.DragStart;
        if (id == "drag_hold") return PetAnimationState.DragHold;
        if (id == "drop") return PetAnimationState.Drop;
        if (id == "study_guard_m8" && _study.IsActive) return PetAnimationState.Study;
        if (id is "feed_snack" or "feed_meal" or "rest_tea") return PetAnimationState.Feeding;
        if (id == "sleepy_m8") return PetAnimationState.Sleeping;
        if (id == "talking") return PetAnimationState.Chatting;

        var spec = _catalog.Get(id);
        if (spec.Loop || !returnToIdle) return PetAnimationState.LoopingAction;
        return PetAnimationState.OneShot;
    }

    private bool CanSwitchTo(PetAnimationState nextState)
    {
        if (_activity.IsDragging || _dragging)
            return nextState is PetAnimationState.DragStart or PetAnimationState.DragHold or PetAnimationState.Drop;

        if (_animationState is PetAnimationState.DragStart or PetAnimationState.DragHold)
            return nextState is PetAnimationState.Drop or PetAnimationState.Idle;

        if (_activity.Mode is PetActivityMode.Learning or PetActivityMode.Feeding or PetActivityMode.Sleeping or PetActivityMode.Chatting
            && nextState == PetAnimationState.Idle)
            return false;

        if (nextState == PetAnimationState.Idle && IsTemporaryAnimationActive())
            return false;

        return true;
    }

    private bool IsTemporaryAnimationActive()
    {
        if (_activeAnimation is null || _activeAnimation.Loop) return false;
        if (_animationState is not (PetAnimationState.OneShot or PetAnimationState.Drop)) return false;

        var duration = Math.Max(350, _activeAnimation.DurationMs);
        return (DateTimeOffset.Now - _animationStartedAt).TotalMilliseconds < duration;
    }

    private void StartFrameTimer()
    {
        if (_activeAnimation is null) return;
        var fps = Math.Max(1, _activeAnimation.Fps);
        _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
        _frameTimer.Start();
    }

    private async Task CrossfadeAndStartAsync(BitmapSource newFrame, CancellationToken token)
    {
        PetSpriteOverlay.Source = newFrame;
        PetSpriteOverlay.Opacity = 0;
        PetSpriteOverlay.Visibility = Visibility.Visible;

        var steps = (int)(CrossfadeDurationMs / CrossfadeStepMs);
        for (var i = 1; i <= steps; i++)
        {
            if (token.IsCancellationRequested) return;
            var t = (double)i / steps;
            PetSprite.Opacity = 1 - t;
            PetSpriteOverlay.Opacity = t;
            await Task.Delay((int)CrossfadeStepMs, token);
        }

        PetSprite.Source = newFrame;
        PetSprite.Opacity = 1;
        PetSpriteOverlay.Visibility = Visibility.Collapsed;

        StartFrameTimer();
    }

    private void FrameTimer_Tick(object? sender, EventArgs e)
    {
        if (_activeAnimation is null) return;

        if (ShouldLoopCurrentAnimation())
        {
            if (_returnToIdleAfterOneShot && HasAnimationDurationElapsed())
            {
                PlayCurrentModeAnimation();
                return;
            }

            _frameIndex = (_frameIndex + 1) % _catalog.GetFrameCount(_activeAnimation);
            SetFrame(_frameIndex);
            return;
        }

        if (_frameIndex < _catalog.GetFrameCount(_activeAnimation) - 1)
        {
            _frameIndex++;
            SetFrame(_frameIndex);
            return;
        }

        if (_dragging && _currentAnimationId == "drag_start" && HasAnimationDurationElapsed())
        {
            PlayAnimation("drag_hold", returnToIdle: false);
            return;
        }

        if (!HasAnimationDurationElapsed())
        {
            _frameIndex = 0;
            SetFrame(_frameIndex);
            return;
        }

        if (_returnToIdleAfterOneShot && HasAnimationDurationElapsed())
        {
            PlayCurrentModeAnimation();
        }
    }

    private void PlayCurrentModeAnimation()
    {
        if (_activity.IsDragging) return;

        var animationId = _activity.Mode switch
        {
            PetActivityMode.Learning => _activity.LearningKind == LearningKind.Painting && _catalog.Has("draw_m8")
                ? "draw_m8"
                : "study_guard_m8",
            PetActivityMode.Feeding => _currentFeedingAnimationId,
            PetActivityMode.Sleeping => "sleepy_m8",
            PetActivityMode.Chatting => "talking",
            _ => "idle_m8"
        };

        var returnToIdle = animationId is not ("idle_m8" or "study_guard_m8" or "sleepy_m8" or "draw_m8" or "feed_snack" or "feed_meal" or "rest_tea");
        PlayAnimation(animationId, returnToIdle);
    }

    private bool ShouldLoopCurrentAnimation() =>
        _activeAnimation?.Loop == true ||
        _animationState is PetAnimationState.LoopingAction
            or PetAnimationState.Study
            or PetAnimationState.Feeding
            or PetAnimationState.Sleeping
            or PetAnimationState.Chatting;

    private bool HasAnimationDurationElapsed()
    {
        if (_activeAnimation is null) return true;

        var minDuration = _activeAnimation.Loop ? 1200 : 350;
        if (_currentAnimationId == "drag_start") minDuration = 1;
        var duration = Math.Max(minDuration, _activeAnimation.DurationMs);
        return (DateTimeOffset.Now - _animationStartedAt).TotalMilliseconds >= duration;
    }

    private void SetFrame(int index)
    {
        if (_activeAnimation is null) return;
        index = Math.Clamp(index, 0, _catalog.GetFrameCount(_activeAnimation) - 1);
        var bitmap = _catalog.LoadFrame(_activeAnimation, index);
        _currentBitmap = bitmap;
        PetSprite.Source = bitmap;
    }

    private void PetSprite_MouseEnter(object sender, MouseEventArgs e)
    {
    }

    private void PetSprite_MouseLeave(object sender, MouseEventArgs e)
    {
    }

    private void PetSprite_MouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed)
        {
            var currentScreen = PointToScreen(e.GetPosition(this));
            var moved = currentScreen - _pressScreenPoint;
            if (!_dragging && (Math.Abs(moved.X) > 4 || Math.Abs(moved.Y) > 4))
            {
                _dragging = true;
                _lastDragScreenPoint = currentScreen;
                if (_activity.Mode == PetActivityMode.Sleeping)
                {
                    _activity.Wake();
                }
                _activity.BeginDrag();
                PlayAnimation("drag_start", returnToIdle: false);
                StartDragHoldSwitchTimer();
                ShowBubble(_dialogue.Pick("drag.start"), seconds: 2);
                UpdateStatusText();
            }

            if (_dragging)
            {
                var delta = currentScreen - _lastDragScreenPoint;
                Left += delta.X;
                Top += delta.Y;
                _lastDragScreenPoint = currentScreen;
                KeepInsideWorkArea();
            }
            return;
        }

        // Hover no longer drives animation state; click/tap and explicit buttons do.
    }

    private void PetSprite_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedRegion = HitTestPet(e);
        if (_pressedRegion == PetHitRegion.None) return;
        _pressed = true;
        _dragging = false;
        _pressScreenPoint = PointToScreen(e.GetPosition(this));
        _lastDragScreenPoint = _pressScreenPoint;
        PetSprite.CaptureMouse();
        e.Handled = true;
    }

    private void PetSprite_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed) return;
        PetSprite.ReleaseMouseCapture();
        _pressed = false;

        if (_dragging)
        {
            _dragging = false;
            _activity.EndDrag();
            _dragHoldSwitchTimer.Stop();
            PlayAnimation("drop");
            ShowBubble(_dialogue.Pick("drag.drop"), seconds: 3);
            _state.Energy = Math.Max(0, _state.Energy - 1);
            SaveAll();
            UpdateStatusText();
            return;
        }

        ShowHud();
        HandleTap(_pressedRegion);
        e.Handled = true;
    }

    private void StartDragHoldSwitchTimer()
    {
        _dragHoldSwitchTimer.Stop();
        _dragHoldSwitchTimer.Interval = TimeSpan.FromMilliseconds(DragStartToHoldDelayMs);
        _dragHoldSwitchTimer.Start();
    }

    private void DragHoldSwitchTimer_Tick(object? sender, EventArgs e)
    {
        _dragHoldSwitchTimer.Stop();
        if (_pressed && _dragging && _currentAnimationId == "drag_start")
        {
            PlayAnimation("drag_hold", returnToIdle: false);
        }
    }

    private PetHitRegion HitTestPet(MouseEventArgs e)
    {
        var p = e.GetPosition(PetSprite);
        return _hitTest.HitTest(p, new Size(PetSprite.ActualWidth, PetSprite.ActualHeight), _currentBitmap);
    }

    private void HandleTap(PetHitRegion region)
    {
        if (_activity.Mode == PetActivityMode.Sleeping)
        {
            _activity.Wake();
            ShowHud();
            ShowBubble("唔...醒啦。", seconds: 3);
            PlayCurrentModeAnimation();
            UpdateStatusText();
            return;
        }

        _activity.RegisterInteraction();

        if (_activity.Mode is PetActivityMode.Learning or PetActivityMode.Feeding)
        {
            ShowBubble(_activity.Mode == PetActivityMode.Learning ? "先陪我把这段学完吧。" : "正在吃东西，等一下下。", seconds: 3);
            UpdateStatusText();
            return;
        }

        var now = DateTimeOffset.Now;
        if (region == _lastTapRegion && (now - _lastTapAt).TotalMilliseconds <= 1800)
        {
            _repeatCount++;
        }
        else
        {
            _repeatCount = 1;
        }
        _lastTapRegion = region;
        _lastTapAt = now;

        var action = _behavior.HandleTap(region, _repeatCount, _state);
        _reducer.ApplyTap(_state, region, _repeatCount);
        RunAction(action);
        SaveAll();
        UpdateStatusText();
    }

    private void RunAction(PetAction action, bool showBubble = true)
    {
        PlayAnimation(action.AnimationId, action.ReturnToIdle);
        if (showBubble)
        {
            var text = _dialogue.Pick(action.DialogueCategory);
            ShowBubble(text, action.Pinned, action.BubbleSeconds);
        }
    }

    private void ShowBubble(string text, bool pinned = false, int seconds = 4)
    {
        if (UseHudReferencePreview)
        {
            BubbleBorder.Visibility = Visibility.Collapsed;
            _hideBubbleTimer.Stop();
            return;
        }

        BubbleText.Text = text;
        BubbleBorder.Tag = pinned ? "pinned" : "normal";
        BubbleBorder.Visibility = Visibility.Visible;
        PositionBubbleAndHud();
        _hideBubbleTimer.Stop();
        if (!pinned)
        {
            _hideBubbleTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, seconds));
            _hideBubbleTimer.Start();
        }
    }

    private void HideBubbleIfNotPinned()
    {
        if ((BubbleBorder.Tag as string) == "pinned") return;
        BubbleBorder.Visibility = Visibility.Collapsed;
        _hideBubbleTimer.Stop();
    }

    private void ShowHud()
    {
        if (_settings.ClickThrough) return;
        _hideHudTimer.Stop();
        HudPanel.Visibility = Visibility.Visible;
        PositionBubbleAndHud();
    }

    private void ScheduleHudHide()
    {
        _hideHudTimer.Stop();
        _hideHudTimer.Interval = TimeSpan.FromMilliseconds(650);
        _hideHudTimer.Start();
    }

    private void HudOrBubble_MouseEnter(object sender, MouseEventArgs e) => _hideHudTimer.Stop();

    private void HudOrBubble_MouseLeave(object sender, MouseEventArgs e)
    {
        if (ReferenceEquals(sender, HudPanel))
        {
            _lastHudMousePosition = null;
            ClearHudMouseLinks();
        }

        ScheduleHudHide();
    }

    private void HudPanel_MouseMove(object sender, MouseEventArgs e)
    {
        _lastHudMousePosition = e.GetPosition(HudLinkLayer);
    }

    private void InitializeHudEffects()
    {
        if (_hudSnowflakes.Count > 0) return;

        var width = GetHudEffectWidth();
        var height = GetHudEffectHeight();
        for (var i = 0; i < 34; i++)
        {
            var size = _random.Next(5, 14);
            var isStar = i % 6 == 0;
            var visual = new TextBlock
            {
                Text = isStar ? "✦" : "❄",
                FontSize = size,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(
                    (byte)_random.Next(92, 168),
                    (byte)_random.Next(205, 235),
                    (byte)_random.Next(218, 245),
                    255)),
                Opacity = _random.NextDouble() * 0.34 + 0.34,
                IsHitTestVisible = false
            };

            var flake = new HudSnowflake
            {
                Visual = visual,
                X = _random.NextDouble() * width,
                Y = _random.NextDouble() * height,
                Speed = _random.NextDouble() * 0.32 + 0.12,
                Drift = _random.NextDouble() * 8 + 4,
                Phase = _random.NextDouble() * Math.PI * 2,
                LinkRadius = _random.NextDouble() * 58 + 118
            };

            _hudSnowflakes.Add(flake);
            HudSnowLayer.Children.Add(visual);
        }
    }

    private void HudEffectTimer_Tick(object? sender, EventArgs e)
    {
        if (HudPanel.Visibility != Visibility.Visible) return;
        if (_hudSnowflakes.Count == 0) InitializeHudEffects();

        _hudEffectFrame++;
        var width = GetHudEffectWidth();
        var height = GetHudEffectHeight();
        foreach (var flake in _hudSnowflakes)
        {
            flake.Y += flake.Speed;
            if (flake.Y > height + 18)
            {
                flake.Y = -18;
                flake.X = _random.NextDouble() * width;
            }

            var driftedX = flake.X + Math.Sin((_hudEffectFrame * 0.018) + flake.Phase) * flake.Drift;
            Canvas.SetLeft(flake.Visual, driftedX);
            Canvas.SetTop(flake.Visual, flake.Y);
        }

        if (HudPanel.IsMouseOver)
        {
            _lastHudMousePosition = Mouse.GetPosition(HudLinkLayer);
        }
        else
        {
            _lastHudMousePosition = null;
        }

        UpdateHudMouseLinks();
    }

    private void UpdateHudMouseLinks()
    {
        ClearHudMouseLinks();
        if (_lastHudMousePosition is not { } mouse) return;

        var cursorGlow = new Ellipse
        {
            Width = 76,
            Height = 76,
            Fill = new RadialGradientBrush(
                Color.FromArgb(72, 190, 211, 255),
                Color.FromArgb(0, 190, 211, 255)),
            Stroke = new SolidColorBrush(Color.FromArgb(92, 255, 255, 255)),
            StrokeThickness = 1,
            Opacity = 0.82,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(cursorGlow, mouse.X - 38);
        Canvas.SetTop(cursorGlow, mouse.Y - 38);
        _hudMouseLinks.Add(cursorGlow);
        HudLinkLayer.Children.Add(cursorGlow);

        foreach (var flake in _hudSnowflakes
                     .Select(flake => new
                     {
                         Flake = flake,
                         X = Canvas.GetLeft(flake.Visual) + flake.Visual.FontSize * 0.5,
                         Y = Canvas.GetTop(flake.Visual) + flake.Visual.FontSize * 0.5
                     })
                     .Select(item => new
                     {
                         item.Flake,
                         item.X,
                         item.Y,
                         Distance = Math.Sqrt(Math.Pow(item.X - mouse.X, 2) + Math.Pow(item.Y - mouse.Y, 2))
                     })
                     .Where(item => item.Distance < item.Flake.LinkRadius)
                     .OrderBy(item => item.Distance)
                     .Take(7))
        {
            var opacity = Math.Max(0.18, 1 - flake.Distance / flake.Flake.LinkRadius) * 0.72;
            var line = new Line
            {
                X1 = mouse.X,
                Y1 = mouse.Y,
                X2 = flake.X,
                Y2 = flake.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(205, 176, 195, 255)),
                StrokeThickness = 1.15,
                Opacity = opacity,
                IsHitTestVisible = false
            };

            _hudMouseLinks.Add(line);
            HudLinkLayer.Children.Add(line);
        }
    }

    private void ClearHudMouseLinks()
    {
        foreach (var line in _hudMouseLinks)
        {
            HudLinkLayer.Children.Remove(line);
        }
        _hudMouseLinks.Clear();
    }

    private double GetHudEffectWidth() =>
        HudSnowLayer.ActualWidth > 1 ? HudSnowLayer.ActualWidth : Math.Max(1, HudPanel.Width - HudPanel.Padding.Left - HudPanel.Padding.Right);

    private double GetHudEffectHeight() =>
        HudSnowLayer.ActualHeight > 1 ? HudSnowLayer.ActualHeight : Math.Max(1, HudPanel.Height - HudPanel.Padding.Top - HudPanel.Padding.Bottom);

    private void PositionBubbleAndHud()
    {
        var petLeft = Canvas.GetLeft(PetSprite);
        var petTop = Canvas.GetTop(PetSprite);
        Canvas.SetLeft(BubbleBorder, petLeft + 130);
        Canvas.SetTop(BubbleBorder, Math.Max(18, petTop - 72));
        Canvas.SetLeft(HudPanel, Math.Max(430, Width - HudPanel.Width - 36));
        Canvas.SetTop(HudPanel, 42);
    }

    private void IdleTimer_Tick(object? sender, EventArgs e)
    {
        var stateChanged = _activity.TickTimedStates();
        if (_activity.ShouldAutoSleep())
        {
            _activity.TryEnterSleeping();
            PlayAnimation("sleepy_m8", returnToIdle: false);
            ShowBubble("有点困了，先眯一会儿。", seconds: 3);
            stateChanged = true;
        }

        if (stateChanged)
        {
            PlayCurrentModeAnimation();
        }

        if (_activity.IsIdleActionDue() && _animationState is PetAnimationState.Idle or PetAnimationState.LoopingAction)
        {
            var idleAnimation = PickIdleAnimation();
            if (idleAnimation is not null)
            {
                PlayAnimation(idleAnimation, returnToIdle: false);
                _activity.MarkIdleActionPlayed();
            }
        }

        if (_activity.Mode == PetActivityMode.Idle &&
            _random.NextDouble() < 0.03 * _settings.IdleFrequency &&
            (DateTimeOffset.Now - _lastIdleBubbleAt).TotalSeconds > 28)
        {
            _lastIdleBubbleAt = DateTimeOffset.Now;
            ShowBubble(_dialogue.Pick("idle.proactive"), seconds: 3);
        }

        UpdateStatusText();
    }

    private string? PickIdleAnimation()
    {
        var candidates = new[]
        {
            "plush_hug_m8",
            "tongue_m8",
            "photo_m8",
            "clasp_idle_m8",
            "hand_invite_m8",
            "idle_cheer_m8"
        }.Where(id => _catalog.Has(id)).ToList();

        if (candidates.Count == 0) return null;

        var available = candidates.Where(id => !_recentIdleAnimations.Contains(id)).ToList();
        if (available.Count == 0) available = candidates;

        var selected = available[_random.Next(available.Count)];
        _recentIdleAnimations.Enqueue(selected);
        while (_recentIdleAnimations.Count > 3) _recentIdleAnimations.Dequeue();
        return selected;
    }

    private void StudyTimer_Tick(object? sender, EventArgs e)
    {
        if (!_study.IsActive) return;

        if (_study.HasCompleted())
        {
            var completedMinutes = (int)Math.Round(_study.TotalDuration.TotalMinutes);
            _study.Cancel();
            _activity.CompleteLearning();
            _studyRecord.AddMinutes(completedMinutes);
            _reducer.ApplyStudyComplete(_state);
            _studyRecordStore.Save(_studyRecord);
            SaveAll();
            UpdateStatusText();
            RunAction(new PetAction("idle_cheer_m8", "study.complete"));
            return;
        }

        var prefix = _study.IsPaused ? "暂停中" : "专注中";
        var remaining = _study.Remaining;
        StudyStatusText.Text = $"{FormatLearningKind(_activity.LearningKind)} · {prefix}：{FormatDuration(remaining)}";
        BubbleText.Text = $"{prefix} {FormatDuration(remaining)}";
        BubbleBorder.Tag = "pinned";
        BubbleBorder.Visibility = Visibility.Visible;
        PositionBubbleAndHud();
        UpdateStatusText();
    }

    private static string FormatLearningKind(LearningKind kind) =>
        kind == LearningKind.Painting ? "画画" : "读书";

    private static string FormatLearningPreset(LearningDurationPreset preset) =>
        preset switch
        {
            LearningDurationPreset.Standard => "标准模式",
            LearningDurationPreset.Deep => "深度模式",
            _ => "专注模式"
        };


    private void HudCloseButton_Click(object sender, RoutedEventArgs e)
    {
        HudPanel.Visibility = Visibility.Collapsed;
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page })
        {
            SetHudPage(page);
        }
    }

    private void SetHudPage(string page)
    {
        _activeHudPage = page;
        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        CarePage.Visibility = page == "Care" ? Visibility.Visible : Visibility.Collapsed;
        StudyPage.Visibility = page == "Study" ? Visibility.Visible : Visibility.Collapsed;
        ChatPage.Visibility = page == "Chat" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        StyleNavButton(HomeNavButton, page == "Home");
        StyleNavButton(StudyNavButton, page == "Study");
        StyleNavButton(ChatNavButton, page == "Care");
        StyleNavButton(SettingsNavButton, page == "Settings");
    }

    private static void StyleNavButton(Button button, bool active)
    {
        button.Background = active
            ? new SolidColorBrush(Color.FromRgb(111, 142, 232))
            : new SolidColorBrush(Color.FromRgb(244, 247, 255));
        button.Foreground = active
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(65, 82, 124));
        button.BorderBrush = active
            ? new SolidColorBrush(Color.FromRgb(103, 129, 224))
            : new SolidColorBrush(Color.FromRgb(214, 225, 249));
    }

    private void ReadingKindButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedLearningKind = LearningKind.Reading;
        UpdateLearningKindButtons();
    }

    private void PaintingKindButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedLearningKind = LearningKind.Painting;
        UpdateLearningKindButtons();
    }

    private void UpdateLearningKindButtons()
    {
        StyleChoiceButton(ReadingKindButton, _selectedLearningKind == LearningKind.Reading);
        StyleChoiceButton(PaintingKindButton, _selectedLearningKind == LearningKind.Painting);
    }

    private static void StyleChoiceButton(Button button, bool active)
    {
        button.Background = active
            ? new SolidColorBrush(Color.FromRgb(111, 142, 232))
            : new SolidColorBrush(Color.FromRgb(248, 251, 255));
        button.Foreground = active
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(66, 84, 123));
        button.BorderBrush = active
            ? new SolidColorBrush(Color.FromRgb(103, 129, 224))
            : new SolidColorBrush(Color.FromRgb(207, 222, 249));
    }

    private void PreviewIdle_Click(object sender, RoutedEventArgs e) => PlayAnimation("idle_m8", returnToIdle: false);
    private void PreviewDrag_Click(object sender, RoutedEventArgs e) => PlayAnimation("drag_hold", returnToIdle: false);
    private void PreviewThink_Click(object sender, RoutedEventArgs e) => PlayAnimation("study_guard_m8", returnToIdle: false);
    private void PreviewWave_Click(object sender, RoutedEventArgs e) => PlayAnimation("hand_invite_m8");

    private void ActionTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string animationId }) return;
        PlayAnimation(animationId);
    }

    private void PatButton_Click(object sender, RoutedEventArgs e) => HandleTap(PetHitRegion.Head);
    private void SnackButton_Click(object sender, RoutedEventArgs e) => Feed("snack");
    private void MealButton_Click(object sender, RoutedEventArgs e) => Feed("meal");
    private void TeaButton_Click(object sender, RoutedEventArgs e) => Feed("tea");

    private void Feed(string foodKind)
    {
        if (_activity.Mode == PetActivityMode.Sleeping)
        {
            _activity.Wake();
        }

        if (_state.FoodInventory.TryGetValue(foodKind, out var count) && count <= 0)
        {
            ShowBubble("今天这份已经没有了，不过没关系。", seconds: 3);
            UpdateStatusText();
            return;
        }

        if (!_activity.TryStartFeeding())
        {
            ShowBubble("现在不太适合吃东西。", seconds: 3);
            UpdateStatusText();
            return;
        }
        _reducer.ApplyFeed(_state, foodKind);
        var action = _behavior.HandleFeed(foodKind);

        _currentFeedingAnimationId = action.AnimationId;
        PlayAnimation(_currentFeedingAnimationId, returnToIdle: false);
        ShowBubble(_dialogue.Pick(action.DialogueCategory), action.Pinned, action.BubbleSeconds);

        SaveAll();
        UpdateStatusText();
    }

    private Task ShowSequencePropAsync(string propId, string? motion)
    {
        var point = motion switch
        {
            "pop" => new Point(300, 260),
            "flyToHands" => new Point(250, 330),
            "flyToMouthOrHand" => new Point(250, 315),
            _ => new Point(260, 320)
        };

        _propLayer?.ShowProp(propId, point.X, point.Y);
        return Task.CompletedTask;
    }

    private void ChatSendButton_Click(object sender, RoutedEventArgs e)
    {
        var message = ChatBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(message)) ChatBox.Clear();
        if (!_activity.TryStartChat())
        {
            ShowBubble("等我放下来再说。", seconds: 2);
            return;
        }
        PlayAnimation("talking");
        ShowBubble(_dialogue.Pick("chat.reply"), seconds: 4);
        UpdateStatusText();
    }

    private void Study25Button_Click(object sender, RoutedEventArgs e) => StartStudy(25);
    private void Study60Button_Click(object sender, RoutedEventArgs e) => StartStudy(60);
    private void Study120Button_Click(object sender, RoutedEventArgs e) => StartStudy(120);

    private void StartStudy(int minutes)
    {
        var kind = _selectedLearningKind;
        var preset = minutes switch
        {
            60 => LearningDurationPreset.Standard,
            120 => LearningDurationPreset.Deep,
            _ => LearningDurationPreset.Focus
        };

        if (!_activity.TryStartLearning(kind, preset))
        {
            ShowBubble("现在还不能开始学习。", seconds: 3);
            UpdateStatusText();
            return;
        }

        _study.Start(TimeSpan.FromMinutes(minutes));
        StudyStatusText.Text = $"{FormatLearningKind(kind)} · {FormatLearningPreset(preset)}：{minutes}:00";
        PlayCurrentModeAnimation();
        ShowBubble(_dialogue.Pick("study.start"), pinned: true);
        _state.Outfit = "focus";
        SaveAll();
        UpdateStatusText();
    }

    private void StudyPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_activity.CanPauseLearning(_study.IsActive, _study.IsPaused)) return;
        _study.Pause();
        ShowBubble(_dialogue.Pick("study.pause"), pinned: true);
        UpdateStatusText();
    }

    private void StudyResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_activity.CanResumeLearning(_study.IsActive, _study.IsPaused)) return;
        _study.Resume();
        PlayCurrentModeAnimation();
        ShowBubble(_dialogue.Pick("study.resume"), pinned: true);
        UpdateStatusText();
    }

    private void StudyCancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_activity.CanStopLearning(_study.IsActive)) return;
        _study.Cancel();
        _activity.StopLearning();
        StudyStatusText.Text = "未开始";
        BubbleBorder.Tag = "normal";
        ShowBubble(_dialogue.Pick("study.cancel"), seconds: 3);
        PlayAnimation("idle_m8", returnToIdle: false);
        UpdateStatusText();
    }

    private void RandomIdleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activity.Mode != PetActivityMode.Idle || _activity.IsDragging) return;
        _activity.RegisterInteraction();
        var animationId = PickIdleAnimation();
        if (animationId is not null)
        {
            PlayAnimation(animationId, returnToIdle: false);
        }
        UpdateStatusText();
    }

    private void SleepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activity.Mode != PetActivityMode.Idle || _activity.IsDragging) return;
        _activity.RegisterInteraction();
        _activity.TryEnterSleeping();
        PlayAnimation("sleepy_m8", returnToIdle: false);
        ShowBubble("晚安，叫我也可以。", seconds: 3);
        UpdateStatusText();
    }

    private void SaveChatSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.ChatProvider = ProviderBox.Text.Trim();
        _settings.ChatBaseUrl = BaseUrlBox.Text.Trim();
        _settings.ChatModel = ModelBox.Text.Trim();
        _settings.ChatApiKey = ApiKeyBox.Text.Trim();
        _settings.ChatSystemPrompt = SystemPromptBox.Text.Trim();
        if (int.TryParse(ContextTurnsBox.Text.Trim(), out var turns))
        {
            _settings.MaxContextTurns = Math.Max(0, turns);
        }

        SaveAll();
        ShowBubble("聊天配置已保存。", seconds: 2);
    }

    private void OutfitDaily_Click(object sender, RoutedEventArgs e) => ChangeOutfit("daily");
    private void OutfitFocus_Click(object sender, RoutedEventArgs e) => ChangeOutfit("focus");
    private void OutfitRest_Click(object sender, RoutedEventArgs e) => ChangeOutfit("rest");
    private void OutfitNight_Click(object sender, RoutedEventArgs e) => ChangeOutfit("night");
    private void OutfitFestival_Click(object sender, RoutedEventArgs e) => ChangeOutfit("festival");

    private void ChangeOutfit(string outfit)
    {
        _reducer.ApplyOutfit(_state, outfit);
        RunAction(_behavior.HandleOutfit(outfit));
        SaveAll();
        UpdateStatusText();
    }

    private void TopmostButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Topmost = !_settings.Topmost;
        ApplySettingsToWindow();
        SaveAll();
    }

    private void ClickThroughButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClickThrough = !_settings.ClickThrough;
        ApplySettingsToWindow();
        SaveAll();
    }

    private void SoundButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.SoundEnabled = !_settings.SoundEnabled;
        ApplySettingsToWindow();
        SaveAll();
    }

    private void ReloadAssetsButton_Click(object sender, RoutedEventArgs e)
    {
        _catalog = AnimationCatalog.Load(_assetRoot);
        _hitTest = new HitTestService(_catalog.HitRegions);
        _motionSeq?.Cancel();
        _motionSeq = new MotionSequenceService(_assetRoot);
        _motionSeq.LoadManifest();
        _propLayer?.HideAllProps();
        _propLayer = new PropLayerService(PropLayer, _assetRoot);
        _propLayer.LoadManifest();
        _catalog.Preload("idle_m8", "drag_start", "drag_hold", "drop", "study_guard_m8", "sleepy_m8", "talking", "feed_snack", "feed_meal", "rest_tea", "plush_hug_m8", "photo_m8", "draw_m8", "tongue_m8");
        PlayAnimation("idle_m8", returnToIdle: false);
        ShowBubble("资源已重载。", seconds: 2);
    }

    private void ApplySettingsToWindow()
    {
        Topmost = _settings.Topmost;
        if (_hwnd != IntPtr.Zero)
        {
            NativeWindowInterop.SetClickThrough(_hwnd, _settings.ClickThrough);
        }
        SetSettingButtonContent(TopmostButton, "TOP", _settings.Topmost ? "置顶：开" : "置顶：关", _settings.Topmost);
        SetSettingButtonContent(ClickThroughButton, "PASS", _settings.ClickThrough ? "点击穿透：开" : "点击穿透：关", _settings.ClickThrough);
        SetSettingButtonContent(SoundButton, "SND", _settings.SoundEnabled ? "声音：开" : "声音：关", _settings.SoundEnabled);
    }

    private void LoadChatSettingsIntoUi()
    {
        ProviderBox.Text = _settings.ChatProvider;
        BaseUrlBox.Text = _settings.ChatBaseUrl;
        ModelBox.Text = _settings.ChatModel;
        ApiKeyBox.Text = _settings.ChatApiKey;
        SystemPromptBox.Text = _settings.ChatSystemPrompt;
        ContextTurnsBox.Text = _settings.MaxContextTurns.ToString();
    }

    private void LoadHudAvatar()
    {
        var avatarId = _catalog.Has("idle_m8") ? "idle_m8" : "reference_pose";
        if (!_catalog.Has(avatarId)) return;
        HudAvatar.Source = _catalog.LoadFrame(_catalog.Get(avatarId), 0);
    }


    private static void SetSettingButtonContent(Button button, string icon, string label, bool enabled)
    {
        var dock = new DockPanel { LastChildFill = true };
        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 10,
            FontWeight = FontWeights.Black,
            Width = 36,
            VerticalAlignment = VerticalAlignment.Center
        };
        dock.Children.Add(iconText);

        var toggleText = new TextBlock
        {
            Text = enabled ? "●" : "●",
            Foreground = enabled ? new SolidColorBrush(Color.FromRgb(140, 184, 255)) : new SolidColorBrush(Color.FromRgb(220, 230, 242)),
            FontSize = 28,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(toggleText, Dock.Right);
        dock.Children.Add(toggleText);

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        dock.Children.Add(labelText);

        button.Content = dock;
    }

    private void UpdateStatusText()
    {
        if (UseHudReferencePreview)
        {
            UpdateReferencePreviewStatusText();
            UpdateCommandAvailability();
            return;
        }

        ModeText.Text = GetModeTitle();
        ModeDetailText.Text = GetModeDetail();
        NextIdleText.Text = _activity.Mode == PetActivityMode.Idle ? $"{FormatDuration(_activity.UntilNextIdleAction)}后" : "暂停";
        TodayInteractionsText.Text = $"{_activity.TodayInteractions}次";
        RightTodayInteractionsText.Text = $"{_activity.TodayInteractions} 次";
        CompanionText.Text = FormatDuration(_activity.CompanionshipDuration);
        CompanionDayText.Text = $"陪伴你的第 {Math.Max(1, (DateTimeOffset.Now.Date - _activity.CompanionshipStartedAt.Date).Days + 1)} 天";

        MoodValueText.Text = $"{_state.Mood}/100";
        MoodBar.Value = _state.Mood;
        var satiety = Math.Clamp(100 - _state.Hunger, 0, 100);
        HungerValueText.Text = $"{satiety}/100";
        HungerBar.Value = satiety;
        EnergyValueText.Text = $"{_state.Energy}/100";
        EnergyBar.Value = _state.Energy;
        IntimacyValueText.Text = $"{_state.Intimacy}/100";
        IntimacyBar.Value = _state.Intimacy;

        StateStatusText.Text = string.Join(Environment.NewLine,
            $"当前状态：{GetModeTitle()}",
            $"亲密：{_state.Intimacy}/100",
            $"心情：{_state.Mood}/100",
            $"体力：{_state.Energy}/100",
            $"饥饿：{_state.Hunger}/100",
            $"衣装：{_state.Outfit}",
            $"今日学习：{_studyRecord.TodayMinutes} 分钟",
            $"点心/正餐/热茶：{GetFood("snack")}/{GetFood("meal")}/{GetFood("tea")}");

        UpdateCommandAvailability();
    }

    private void UpdateReferencePreviewStatusText()
    {
        var previewCycle = TimeSpan.FromMinutes(42) + TimeSpan.FromSeconds(15);
        var elapsedSeconds = (DateTimeOffset.Now - _hudReferencePreviewStartedAt).TotalSeconds;
        var remaining = previewCycle - TimeSpan.FromSeconds(elapsedSeconds % previewCycle.TotalSeconds);
        var nextIdle = TimeSpan.FromSeconds(Math.Max(0, 24 - (elapsedSeconds % 25)));
        var companion = TimeSpan.FromMinutes(65) + TimeSpan.FromSeconds(elapsedSeconds);

        ModeText.Text = "学习中：读书 Reading";
        ModeDetailText.Text = $"◷ 剩余时间 {FormatClockDuration(remaining)}";
        NextIdleText.Text = $"{FormatDuration(nextIdle)}后";
        TodayInteractionsText.Text = "15次";
        RightTodayInteractionsText.Text = "15 次";
        CompanionText.Text = FormatHourMinute(companion);
        CompanionDayText.Text = "陪伴你的第 28 天";

        MoodValueText.Text = "78/100";
        MoodBar.Value = 78;
        HungerValueText.Text = "64/100";
        HungerBar.Value = 64;
        EnergyValueText.Text = "82/100";
        EnergyBar.Value = 82;
        IntimacyValueText.Text = "1280/2000";
        IntimacyBar.Value = 64;

        StateStatusText.Text = "当前状态：学习中";
    }

    private static string FormatClockDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    }

    private static string FormatHourMinute(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return $"{(int)value.TotalHours:00}:{value.Minutes:00}";
    }

    private string GetModeTitle()
    {
        if (_activity.IsDragging) return "拖拽中";
        return _activity.Mode switch
        {
            PetActivityMode.Feeding => "正在进食",
            PetActivityMode.Learning => $"正在{FormatLearningKind(_activity.LearningKind)}",
            PetActivityMode.Sleeping => "睡觉中",
            PetActivityMode.Chatting => "聊天中",
            _ => "空闲"
        };
    }

    private string GetModeDetail()
    {
        if (_activity.IsDragging) return "松开后会回到原来的节奏。";
        if (_activity.Mode == PetActivityMode.Feeding && _activity.FeedingEndsAt is { } feedingEndsAt)
        {
            return $"剩余 {FormatDuration(feedingEndsAt - DateTimeOffset.Now)}，学习/睡觉/随机动作暂不可用。";
        }
        if (_activity.Mode == PetActivityMode.Learning || (_activity.Mode == PetActivityMode.Chatting && _activity.IsLearningUnderChat))
        {
            var prefix = _study.IsPaused ? "暂停中" : "进行中";
            return $"{FormatLearningKind(_activity.LearningKind)} · {FormatLearningPreset(_activity.LearningPreset)} · {prefix} · 剩余 {FormatDuration(_study.Remaining)}";
        }
        if (_activity.Mode == PetActivityMode.Sleeping) return "点击、聊天、拖拽或喂食都可以叫醒。";
        if (_activity.Mode == PetActivityMode.Chatting) return "正在回应你，稍后会回到空闲。";
        return $"随机待机 {FormatDuration(_activity.UntilNextIdleAction)} 后轮换";
    }

    private void UpdateCommandAvailability()
    {
        var canFeed = _activity.CanStartFeeding();
        SnackButton.IsEnabled = canFeed;
        MealButton.IsEnabled = canFeed;
        TeaButton.IsEnabled = canFeed;

        var canStartLearning = _activity.CanStartLearning();
        ReadingKindButton.IsEnabled = canStartLearning;
        PaintingKindButton.IsEnabled = canStartLearning;
        UpdateLearningKindButtons();
        Study25Button.IsEnabled = canStartLearning;
        Study60Button.IsEnabled = canStartLearning;
        Study120Button.IsEnabled = canStartLearning;
        StudyPauseButton.IsEnabled = _activity.CanPauseLearning(_study.IsActive, _study.IsPaused);
        StudyResumeButton.IsEnabled = _activity.CanResumeLearning(_study.IsActive, _study.IsPaused);
        StudyResumeButton.Visibility = _study.IsPaused ? Visibility.Visible : Visibility.Collapsed;
        StudyCancelButton.IsEnabled = _activity.CanStopLearning(_study.IsActive);

        RandomIdleButton.IsEnabled = _activity.Mode == PetActivityMode.Idle && !_activity.IsDragging;
        SleepButton.IsEnabled = _activity.Mode == PetActivityMode.Idle && !_activity.IsDragging;
        ChatSendButton.IsEnabled = !_activity.IsDragging;
    }

    private static string FormatDuration(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        if (value.TotalHours >= 1)
        {
            return $"{(int)value.TotalHours}小时{value.Minutes:00}分";
        }
        return $"{value.Minutes:00}:{value.Seconds:00}";
    }

    private int GetFood(string key) => _state.FoodInventory.TryGetValue(key, out var value) ? value : 0;

    private void SaveAll()
    {
        _settingsStore.Save(_settings);
        _stateStore.Save(_state);
        _studyRecordStore.Save(_studyRecord);
    }

    private bool IsScreenPointOnOpaquePet(Point screenPoint)
    {
        var local = PetSprite.PointFromScreen(screenPoint);
        return _hitTest.IsOpaque(_currentBitmap, local, new Size(PetSprite.ActualWidth, PetSprite.ActualHeight));
    }

    private static bool IsScreenPointInside(FrameworkElement element, Point screenPoint)
    {
        if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return false;
        var local = element.PointFromScreen(screenPoint);
        return local.X >= 0 && local.Y >= 0 && local.X <= element.ActualWidth && local.Y <= element.ActualHeight;
    }

    private void KeepInsideWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left - Width + 80, Math.Min(area.Right - 80, Left));
        Top = Math.Max(area.Top - Height + 80, Math.Min(area.Bottom - 80, Top));
    }

    private void CreateTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "DesktopPet M1"
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 HUD", null, (_, _) => Dispatcher.Invoke(ShowHud));
        menu.Items.Add("切换点击穿透", null, (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.ClickThrough = !_settings.ClickThrough;
            ApplySettingsToWindow();
            SaveAll();
        }));
        menu.Items.Add("切换置顶", null, (_, _) => Dispatcher.Invoke(() =>
        {
            _settings.Topmost = !_settings.Topmost;
            ApplySettingsToWindow();
            SaveAll();
        }));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Close));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowHud);
    }
}
