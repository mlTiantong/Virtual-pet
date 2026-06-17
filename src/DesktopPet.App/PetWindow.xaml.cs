using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.App.Models;
using DesktopPet.App.Native;
using DesktopPet.App.Services;
using Forms = System.Windows.Forms;

namespace DesktopPet.App;

public partial class PetWindow : Window
{
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

    private PetState _state;
    private UserSettings _settings;
    private StudyRecord _studyRecord;

    private readonly DispatcherTimer _frameTimer = new();
    private readonly DispatcherTimer _idleTimer = new();
    private readonly DispatcherTimer _hideBubbleTimer = new();
    private readonly DispatcherTimer _hideHudTimer = new();
    private readonly DispatcherTimer _studyTimer = new();

    private AnimationSpec? _activeAnimation;
    private int _frameIndex;
    private DateTimeOffset _animationStartedAt;
    private bool _returnToIdleAfterOneShot;
    private BitmapSource? _currentBitmap;
    private string _currentAnimationId = "idle_m8";

    private CancellationTokenSource? _crossfadeCts;
    private const double CrossfadeDurationMs = 150;
    private const double CrossfadeStepMs = 15;
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
    private DateTimeOffset _lastHoverEffectAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastIdleBubbleAt = DateTimeOffset.MinValue;
    private readonly Random _random = new();

    public PetWindow()
    {
        InitializeComponent();
        _assetRoot = System.IO.Path.Combine(AppContext.BaseDirectory, "assets");
        _catalog = AnimationCatalog.Load(_assetRoot);
        _catalog.Preload("idle_m8", "hover_m8", "drag_start", "drag_hold", "drop", "pat_head_m8", "face_reaction_m8", "tap_annoyed", "hand_invite_m8", "study_guard_m8", "talking");
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

        _idleTimer.Interval = TimeSpan.FromSeconds(8);
        _hideBubbleTimer.Interval = TimeSpan.FromSeconds(4);
        _hideHudTimer.Interval = TimeSpan.FromMilliseconds(650);
        _studyTimer.Interval = TimeSpan.FromSeconds(1);
    }

    private void PetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Right - Width - 36;
        Top = SystemParameters.WorkArea.Bottom - Height - 20;
        ApplySettingsToWindow();
        CreateTrayIcon();
        PlayAnimation("idle_m8", returnToIdle: false);
        ShowBubble(_dialogue.Pick("startup"), seconds: 4);
        _idleTimer.Start();
        _studyTimer.Start();
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
        _returnToIdleAfterOneShot = returnToIdle && !_activeAnimation.Loop;

        var firstFrame = _catalog.LoadFrame(_activeAnimation, 0);
        _currentBitmap = firstFrame;

        _crossfadeCts = new CancellationTokenSource();
        var token = _crossfadeCts.Token;
        _ = CrossfadeAndStartAsync(firstFrame, token);
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

        if (_activeAnimation is not null)
        {
            var fps = Math.Max(1, _activeAnimation.Fps);
            _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            _frameTimer.Start();
        }
    }

    private void FrameTimer_Tick(object? sender, EventArgs e)
    {
        if (_activeAnimation is null) return;

        if (_activeAnimation.Loop)
        {
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

        var duration = Math.Max(350, _activeAnimation.DurationMs);
        if (_dragging && _currentAnimationId == "drag_start" && (DateTimeOffset.Now - _animationStartedAt).TotalMilliseconds >= duration)
        {
            PlayAnimation("drag_hold", returnToIdle: false);
            return;
        }

        if (_returnToIdleAfterOneShot && (DateTimeOffset.Now - _animationStartedAt).TotalMilliseconds >= duration)
        {
            PlayAnimation("idle_m8", returnToIdle: false);
        }
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
        ShowHud();
    }

    private void PetSprite_MouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleHudHide();
        if (!_dragging && !_study.IsActive) PlayAnimation("idle_m8", returnToIdle: false);
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
                PlayAnimation("drag_start", returnToIdle: false);
                ShowBubble(_dialogue.Pick("drag.start"), seconds: 2);
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

        var now = DateTimeOffset.Now;
        if ((now - _lastHoverEffectAt).TotalMilliseconds < 1200) return;

        var region = HitTestPet(e);
        if (region == PetHitRegion.None) return;
        var action = _behavior.HandleHover(region, _state);
        _lastHoverEffectAt = now;
        RunAction(action, showBubble: _random.NextDouble() < 0.35);
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
            PlayAnimation("drop");
            ShowBubble(_dialogue.Pick("drag.drop"), seconds: 3);
            _state.Energy = Math.Max(0, _state.Energy - 1);
            SaveAll();
            UpdateStatusText();
            return;
        }

        HandleTap(_pressedRegion);
        e.Handled = true;
    }

    private PetHitRegion HitTestPet(MouseEventArgs e)
    {
        var p = e.GetPosition(PetSprite);
        return _hitTest.HitTest(p, new Size(PetSprite.ActualWidth, PetSprite.ActualHeight), _currentBitmap);
    }

    private void HandleTap(PetHitRegion region)
    {
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
    private void HudOrBubble_MouseLeave(object sender, MouseEventArgs e) => ScheduleHudHide();

    private void PositionBubbleAndHud()
    {
        Canvas.SetLeft(BubbleBorder, 22);
        Canvas.SetTop(BubbleBorder, 22);
        Canvas.SetLeft(HudPanel, Math.Max(390, Width - HudPanel.Width - 22));
        Canvas.SetTop(HudPanel, 46);
    }

    private void IdleTimer_Tick(object? sender, EventArgs e)
    {
        if (_study.IsActive || _dragging) return;
        if ((DateTimeOffset.Now - _lastTapAt).TotalSeconds < 5) return;

        var roll = _random.NextDouble();
        if (_state.Energy < 30 && roll < 0.45)
        {
            RunAction(new PetAction("study_guard_m8", "idle.lowEnergy"));
            return;
        }

        if (roll < 0.18 * _settings.IdleFrequency && (DateTimeOffset.Now - _lastIdleBubbleAt).TotalSeconds > 28)
        {
            _lastIdleBubbleAt = DateTimeOffset.Now;
            ShowBubble(_dialogue.Pick("idle.proactive"), seconds: 3);
        }
    }

    private void StudyTimer_Tick(object? sender, EventArgs e)
    {
        if (!_study.IsActive) return;

        if (_study.HasCompleted())
        {
            var completedMinutes = (int)Math.Round(_study.TotalDuration.TotalMinutes);
            _study.Cancel();
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
        StudyStatusText.Text = $"{prefix}：{remaining:mm\\:ss}";
        BubbleText.Text = $"{prefix} {remaining:mm\\:ss}";
        BubbleBorder.Tag = "pinned";
        BubbleBorder.Visibility = Visibility.Visible;
    }


    private void HudCloseButton_Click(object sender, RoutedEventArgs e)
    {
        HudPanel.Visibility = Visibility.Collapsed;
    }

    private void PreviewIdle_Click(object sender, RoutedEventArgs e) => PlayAnimation("idle_m8", returnToIdle: false);
    private void PreviewDrag_Click(object sender, RoutedEventArgs e) => PlayAnimation("drag_hold", returnToIdle: false);
    private void PreviewThink_Click(object sender, RoutedEventArgs e) => PlayAnimation("study_guard_m8", returnToIdle: false);
    private void PreviewWave_Click(object sender, RoutedEventArgs e) => PlayAnimation("hand_invite_m8");

    private void PatButton_Click(object sender, RoutedEventArgs e) => HandleTap(PetHitRegion.Head);
    private void SnackButton_Click(object sender, RoutedEventArgs e) => Feed("snack");
    private void MealButton_Click(object sender, RoutedEventArgs e) => Feed("meal");
    private void TeaButton_Click(object sender, RoutedEventArgs e) => Feed("tea");

    private async void Feed(string foodKind)
    {
        if (_state.FoodInventory.TryGetValue(foodKind, out var count) && count <= 0)
        {
            ShowBubble("今天这份已经没有了，不过没关系。", seconds: 3);
            return;
        }
        _reducer.ApplyFeed(_state, foodKind);
        var action = _behavior.HandleFeed(foodKind);

        var seqId = foodKind switch
        {
            "snack" => "feed_snack",
            "meal" => "feed_meal",
            "tea" => "feed_tea",
            _ => null
        };

        if (seqId != null && _motionSeq?.HasSequence(seqId) == true)
        {
            try
            {
                await _motionSeq.PlaySequenceAsync(
                    seqId,
                    PlayAnimation,
                    ShowSequencePropAsync,
                    propId => _propLayer?.HideProp(propId));
            }
            finally
            {
                _propLayer?.HideAllProps();
            }
        }
        else
        {
            RunAction(action);
        }

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
        PlayAnimation("talking");
        ShowBubble(_dialogue.Pick("chat.reply"), seconds: 4);
    }

    private void Study25Button_Click(object sender, RoutedEventArgs e) => StartStudy(25);
    private void Study45Button_Click(object sender, RoutedEventArgs e) => StartStudy(45);
    private void Study60Button_Click(object sender, RoutedEventArgs e) => StartStudy(60);

    private void StartStudy(int minutes)
    {
        _study.Start(TimeSpan.FromMinutes(minutes));
        StudyStatusText.Text = $"专注中：{minutes}:00";
        PlayAnimation("study_guard_m8", returnToIdle: false);
        ShowBubble(_dialogue.Pick("study.start"), pinned: true);
        _state.Outfit = "focus";
        SaveAll();
        UpdateStatusText();
    }

    private void StudyPauseButton_Click(object sender, RoutedEventArgs e)
    {
        _study.Pause();
        ShowBubble(_dialogue.Pick("study.pause"), pinned: true);
    }

    private void StudyResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _study.Resume();
        PlayAnimation("study_guard_m8", returnToIdle: false);
        ShowBubble(_dialogue.Pick("study.resume"), pinned: true);
    }

    private void StudyCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _study.Cancel();
        StudyStatusText.Text = "未开始";
        BubbleBorder.Tag = "normal";
        ShowBubble(_dialogue.Pick("study.cancel"), seconds: 3);
        PlayAnimation("idle_m8", returnToIdle: false);
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
        SetSettingButtonContent(TopmostButton, "📌", _settings.Topmost ? "置顶：开" : "置顶：关", _settings.Topmost);
        SetSettingButtonContent(ClickThroughButton, "➤", _settings.ClickThrough ? "点击穿透：开" : "点击穿透：关", _settings.ClickThrough);
        SetSettingButtonContent(SoundButton, "🔊", _settings.SoundEnabled ? "声音：开" : "声音：关", _settings.SoundEnabled);
    }


    private static void SetSettingButtonContent(Button button, string icon, string label, bool enabled)
    {
        var dock = new DockPanel { LastChildFill = true };
        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 22,
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
        StateStatusText.Text = string.Join(Environment.NewLine,
            $"亲密：{_state.Intimacy}/100",
            $"心情：{_state.Mood}/100",
            $"体力：{_state.Energy}/100",
            $"饥饿：{_state.Hunger}/100",
            $"衣装：{_state.Outfit}",
            $"今日学习：{_studyRecord.TodayMinutes} 分钟",
            $"点心/正餐/热茶：{GetFood("snack")}/{GetFood("meal")}/{GetFood("tea")}");
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
