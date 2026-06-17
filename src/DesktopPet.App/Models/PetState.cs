namespace DesktopPet.App.Models;

public sealed class PetState
{
    public int Intimacy { get; set; } = 20;
    public int Mood { get; set; } = 72;
    public int Energy { get; set; } = 76;
    public int Hunger { get; set; } = 36; // 0 = full, 100 = hungry
    public string Outfit { get; set; } = "daily";
    public PetMemory Memory { get; set; } = new();
    public Dictionary<string, int> FoodInventory { get; set; } = new()
    {
        ["snack"] = 9,
        ["meal"] = 5,
        ["tea"] = 9
    };
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class PetMemory
{
    public DateTimeOffset LastOpenedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastAnnoyedAt { get; set; }
    public DateTimeOffset? LastFedAt { get; set; }
    public int HeadPatCount { get; set; }
    public int FeedCount { get; set; }
    public int StudyCompletedCount { get; set; }
    public int StudyStreak { get; set; }
    public string? FavoriteOutfit { get; set; }
}
