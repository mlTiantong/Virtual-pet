using DesktopPet.App.Models;

namespace DesktopPet.App.Services;

public sealed class DefaultPetInteractionStateReducer
{
    public void ApplyTap(PetState state, PetHitRegion region, int repeatCount)
    {
        if (repeatCount >= 4)
        {
            state.Mood = Clamp(state.Mood - 8);
            state.Energy = Clamp(state.Energy - 2);
            state.Memory.LastAnnoyedAt = DateTimeOffset.Now;
            Touch(state);
            return;
        }

        switch (region)
        {
            case PetHitRegion.Head:
                state.Intimacy = Clamp(state.Intimacy + 2);
                state.Mood = Clamp(state.Mood + 3);
                state.Memory.HeadPatCount++;
                break;
            case PetHitRegion.Face:
                state.Mood = Clamp(state.Mood + 1);
                break;
            case PetHitRegion.Hand:
                state.Intimacy = Clamp(state.Intimacy + 1);
                state.Mood = Clamp(state.Mood + 2);
                break;
            case PetHitRegion.Outfit:
            case PetHitRegion.Accessory:
                state.Mood = Clamp(state.Mood + 2);
                break;
            case PetHitRegion.Feet:
                state.Mood = Clamp(state.Mood - 1);
                break;
        }
        Touch(state);
    }

    public void ApplyFeed(PetState state, string foodKind)
    {
        if (state.FoodInventory.TryGetValue(foodKind, out var count) && count > 0)
        {
            state.FoodInventory[foodKind] = count - 1;
        }

        state.Memory.FeedCount++;
        state.Memory.LastFedAt = DateTimeOffset.Now;
        state.Mood = Clamp(state.Mood + (foodKind == "tea" ? 9 : 7));
        state.Hunger = Clamp(state.Hunger - (foodKind == "meal" ? 28 : foodKind == "tea" ? 6 : 12));
        state.Energy = Clamp(state.Energy + (foodKind == "meal" ? 12 : foodKind == "tea" ? 5 : 3));

        if (state.Memory.LastAnnoyedAt is { } annoyedAt && (DateTimeOffset.Now - annoyedAt).TotalMinutes < 20)
        {
            state.Mood = Clamp(state.Mood + 6);
            state.Memory.LastAnnoyedAt = null;
        }
        Touch(state);
    }

    public void ApplyStudyComplete(PetState state)
    {
        state.Memory.StudyCompletedCount++;
        state.Memory.StudyStreak++;
        state.Mood = Clamp(state.Mood + 5);
        state.Energy = Clamp(state.Energy - 4);
        Touch(state);
    }

    public void ApplyOutfit(PetState state, string outfit)
    {
        state.Outfit = outfit;
        state.Memory.FavoriteOutfit = outfit;
        state.Mood = Clamp(state.Mood + 2);
        Touch(state);
    }

    private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));

    private static void Touch(PetState state) => state.LastUpdatedAt = DateTimeOffset.Now;
}
