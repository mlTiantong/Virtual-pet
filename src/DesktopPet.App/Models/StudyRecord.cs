namespace DesktopPet.App.Models;

public sealed class StudyRecord
{
    public Dictionary<string, int> MinutesByDate { get; set; } = new();

    public int TodayMinutes
    {
        get
        {
            MinutesByDate.TryGetValue(DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"), out var minutes);
            return minutes;
        }
    }

    public void AddMinutes(int minutes)
    {
        var key = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        MinutesByDate.TryGetValue(key, out var current);
        MinutesByDate[key] = current + Math.Max(0, minutes);
    }
}
