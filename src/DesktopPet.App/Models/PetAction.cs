namespace DesktopPet.App.Models;

public sealed record PetAction(
    string AnimationId,
    string DialogueCategory,
    bool Pinned = false,
    int BubbleSeconds = 4,
    bool ReturnToIdle = true);
