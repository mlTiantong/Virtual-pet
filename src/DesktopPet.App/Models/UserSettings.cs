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
    public string ChatProvider { get; set; } = "OpenAI-compatible";
    public string ChatBaseUrl { get; set; } = "";
    public string ChatModel { get; set; } = "";
    public string ChatApiKey { get; set; } = "";
    public string ChatSystemPrompt { get; set; } = "你是一只粘人的蓝发桌宠，会用简短、温柔、有点俏皮的话陪伴用户。";
    public int MaxContextTurns { get; set; } = 20;
}
