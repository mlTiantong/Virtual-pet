namespace DesktopPet.App.Services;

public sealed class StudySessionService
{
    private TimeSpan _remainingWhenPaused;
    private DateTimeOffset _endsAt;

    public bool IsActive { get; private set; }
    public bool IsPaused { get; private set; }
    public TimeSpan TotalDuration { get; private set; }

    public TimeSpan Remaining
    {
        get
        {
            if (!IsActive) return TimeSpan.Zero;
            if (IsPaused) return _remainingWhenPaused;
            return _endsAt - DateTimeOffset.Now > TimeSpan.Zero ? _endsAt - DateTimeOffset.Now : TimeSpan.Zero;
        }
    }

    public void Start(TimeSpan duration)
    {
        TotalDuration = duration;
        _endsAt = DateTimeOffset.Now + duration;
        _remainingWhenPaused = duration;
        IsActive = true;
        IsPaused = false;
    }

    public void Pause()
    {
        if (!IsActive || IsPaused) return;
        _remainingWhenPaused = Remaining;
        IsPaused = true;
    }

    public void Resume()
    {
        if (!IsActive || !IsPaused) return;
        _endsAt = DateTimeOffset.Now + _remainingWhenPaused;
        IsPaused = false;
    }

    public void Cancel()
    {
        IsActive = false;
        IsPaused = false;
        _remainingWhenPaused = TimeSpan.Zero;
        TotalDuration = TimeSpan.Zero;
    }

    public bool HasCompleted()
    {
        return IsActive && !IsPaused && Remaining <= TimeSpan.Zero;
    }
}
