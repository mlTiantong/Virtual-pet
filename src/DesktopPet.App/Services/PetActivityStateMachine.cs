using DesktopPet.App.Models;

namespace DesktopPet.App.Services;

public sealed class PetActivityStateMachine
{
    private readonly TimeSpan _feedingDuration = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _chatDuration = TimeSpan.FromSeconds(4);
    private readonly TimeSpan _idleActionDelay = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _autoSleepDelay = TimeSpan.FromMinutes(10);

    private PetActivityMode? _modeBeforeChat;

    public PetActivityMode Mode { get; private set; } = PetActivityMode.Idle;
    public PetOverlayMode Overlay { get; private set; } = PetOverlayMode.None;
    public LearningKind LearningKind { get; private set; } = LearningKind.Reading;
    public LearningDurationPreset LearningPreset { get; private set; } = LearningDurationPreset.Focus;
    public DateTimeOffset LastInteractionAt { get; private set; } = DateTimeOffset.Now;
    public DateTimeOffset CompanionshipStartedAt { get; } = DateTimeOffset.Now;
    public DateTimeOffset NextIdleActionAt { get; private set; } = DateTimeOffset.Now + TimeSpan.FromMinutes(1);
    public DateTimeOffset? FeedingEndsAt { get; private set; }
    public DateTimeOffset? ChatEndsAt { get; private set; }
    public DateOnly InteractionDate { get; private set; } = DateOnly.FromDateTime(DateTime.Now);
    public int TodayInteractions { get; private set; }

    public bool IsDragging => Overlay == PetOverlayMode.Dragging;
    public bool IsBusy => Mode is PetActivityMode.Feeding or PetActivityMode.Learning or PetActivityMode.Sleeping;

    public TimeSpan UntilNextIdleAction
    {
        get
        {
            var remaining = NextIdleActionAt - DateTimeOffset.Now;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public TimeSpan CompanionshipDuration => DateTimeOffset.Now - CompanionshipStartedAt;

    public void RegisterInteraction()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today != InteractionDate)
        {
            InteractionDate = today;
            TodayInteractions = 0;
        }

        TodayInteractions++;
        LastInteractionAt = DateTimeOffset.Now;
        ScheduleNextIdleAction();
    }

    public bool CanStartFeeding()
    {
        if (IsDragging) return false;
        if (Mode == PetActivityMode.Idle) return true;
        return Mode == PetActivityMode.Chatting && _modeBeforeChat != PetActivityMode.Learning;
    }

    public bool TryStartFeeding()
    {
        if (!CanStartFeeding()) return false;
        RegisterInteraction();
        Mode = PetActivityMode.Feeding;
        FeedingEndsAt = DateTimeOffset.Now + _feedingDuration;
        ChatEndsAt = null;
        _modeBeforeChat = null;
        return true;
    }

    public bool CanStartLearning() => !IsDragging && Mode == PetActivityMode.Idle;

    public bool TryStartLearning(LearningKind kind, LearningDurationPreset preset)
    {
        if (!CanStartLearning()) return false;
        RegisterInteraction();
        Mode = PetActivityMode.Learning;
        LearningKind = kind;
        LearningPreset = preset;
        return true;
    }

    public bool CanPauseLearning(bool studyIsActive, bool studyIsPaused) =>
        !IsDragging && Mode == PetActivityMode.Learning && studyIsActive && !studyIsPaused;

    public bool CanResumeLearning(bool studyIsActive, bool studyIsPaused) =>
        !IsDragging && Mode == PetActivityMode.Learning && studyIsActive && studyIsPaused;

    public bool CanStopLearning(bool studyIsActive) =>
        !IsDragging && Mode == PetActivityMode.Learning && studyIsActive;

    public void StopLearning()
    {
        RegisterInteraction();
        Mode = PetActivityMode.Idle;
        ScheduleNextIdleAction();
    }

    public bool TryStartChat()
    {
        if (IsDragging) return false;
        RegisterInteraction();
        if (Mode != PetActivityMode.Chatting)
        {
            _modeBeforeChat = Mode;
        }
        ChatEndsAt = DateTimeOffset.Now + _chatDuration;
        Mode = PetActivityMode.Chatting;
        return true;
    }

    public bool TryEnterSleeping()
    {
        if (IsDragging || Mode != PetActivityMode.Idle) return false;
        Mode = PetActivityMode.Sleeping;
        return true;
    }

    public bool Wake()
    {
        if (Mode != PetActivityMode.Sleeping) return false;
        RegisterInteraction();
        Mode = PetActivityMode.Idle;
        ScheduleNextIdleAction();
        return true;
    }

    public void BeginDrag()
    {
        RegisterInteraction();
        Overlay = PetOverlayMode.Dragging;
    }

    public void EndDrag()
    {
        RegisterInteraction();
        Overlay = PetOverlayMode.None;
    }

    public bool ShouldAutoSleep() =>
        !IsDragging &&
        Mode == PetActivityMode.Idle &&
        DateTimeOffset.Now - LastInteractionAt >= _autoSleepDelay;

    public bool IsIdleActionDue() =>
        !IsDragging &&
        Mode == PetActivityMode.Idle &&
        DateTimeOffset.Now >= NextIdleActionAt &&
        DateTimeOffset.Now - LastInteractionAt >= _idleActionDelay;

    public void MarkIdleActionPlayed() => ScheduleNextIdleAction();

    public void CompleteLearning()
    {
        Mode = PetActivityMode.Idle;
        ScheduleNextIdleAction();
    }

    public bool TickTimedStates()
    {
        var changed = false;
        var now = DateTimeOffset.Now;

        if (Mode == PetActivityMode.Feeding && FeedingEndsAt is { } feedingEndsAt && now >= feedingEndsAt)
        {
            Mode = PetActivityMode.Idle;
            FeedingEndsAt = null;
            ScheduleNextIdleAction();
            changed = true;
        }

        if (Mode == PetActivityMode.Chatting && ChatEndsAt is { } chatEndsAt && now >= chatEndsAt)
        {
            Mode = _modeBeforeChat is PetActivityMode.Learning ? PetActivityMode.Learning : PetActivityMode.Idle;
            ChatEndsAt = null;
            _modeBeforeChat = null;
            if (Mode == PetActivityMode.Idle) ScheduleNextIdleAction();
            changed = true;
        }

        return changed;
    }

    public bool IsLearningUnderChat => Mode == PetActivityMode.Chatting && _modeBeforeChat == PetActivityMode.Learning;

    private void ScheduleNextIdleAction()
    {
        NextIdleActionAt = DateTimeOffset.Now + _idleActionDelay;
    }
}
