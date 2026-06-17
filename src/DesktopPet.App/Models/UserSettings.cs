namespace DesktopPet.App.Models;

public sealed class UserSettings
{
    public bool Topmost { get; set; } = true;
    public bool ClickThrough { get; set; } = false;
    public bool SoundEnabled { get; set; } = true;
    public bool VoiceEnabled { get; set; } = false;
    public double Scale { get; set; } = 1.0;
    public double InteractionFrequency { get; set; } = 1.0;
    public double IdleFrequency { get; set; } = 1.0;
}
