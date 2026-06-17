using DesktopPet.App.Models;

namespace DesktopPet.App.Services;

public sealed class DefaultBehaviorScheduler
{
    public PetAction HandleTap(PetHitRegion region, int repeatCount, PetState state)
    {
        if (repeatCount >= 5) return new PetAction("tap_annoyed", "tap.repeat3", BubbleSeconds: 4);
        if (repeatCount == 4) return new PetAction("tap_annoyed", "tap.repeat2", BubbleSeconds: 4);
        if (repeatCount == 3) return new PetAction("tap_annoyed", "tap.repeat1", BubbleSeconds: 3);

        return region switch
        {
            PetHitRegion.Head or PetHitRegion.Hair => new PetAction("pat_head_m8", "tap.head"),
            PetHitRegion.Face => new PetAction("face_reaction_m8", "tap.face"),
            PetHitRegion.Hand => new PetAction("hand_invite_m8", "tap.hand"),
            PetHitRegion.Body or PetHitRegion.Outfit => new PetAction("face_reaction_m8", "tap.outfit"),
            PetHitRegion.Accessory => new PetAction("face_reaction_m8", "tap.accessory"),
            PetHitRegion.Feet => new PetAction("drop", "tap.feet"),
            _ => new PetAction("hover_m8", "hover.default")
        };
    }

    public PetAction HandleHover(PetHitRegion region, PetState state)
    {
        if (state.Energy < 24) return new PetAction("study_guard_m8", "idle.lowEnergy", BubbleSeconds: 3);
        if (region is PetHitRegion.Head or PetHitRegion.Face) return new PetAction("hover_m8", "hover.head", BubbleSeconds: 3);
        if (region is PetHitRegion.Hand) return new PetAction("hand_invite_m8", "hover.hand", BubbleSeconds: 3);
        return new PetAction("hover_m8", "hover.default", BubbleSeconds: 3);
    }

    public PetAction HandleFeed(string foodKind) => foodKind switch
    {
        "meal" => new PetAction("feed_meal", "feed.meal"),
        "tea" => new PetAction("rest_tea", "feed.tea"),
        _ => new PetAction("feed_snack", "feed.snack")
    };

    public PetAction HandleOutfit(string outfit) => new("idle_cheer_m8", "outfit.change");
}
