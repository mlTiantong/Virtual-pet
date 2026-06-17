namespace DesktopPet.App.Models;

public sealed class MotionSequenceManifest
{
    public int Schema { get; set; } = 1;
    public Dictionary<string, MotionSequenceDef> Sequences { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MotionSequenceDef
{
    public List<MotionStep> Steps { get; set; } = new();
}

public sealed class MotionStep
{
    public string? Animation { get; set; }
    public string? Prop { get; set; }
    public string? Motion { get; set; }
    public int DurationMs { get; set; } = 500;
}

public sealed class PropManifest
{
    public int Schema { get; set; } = 1;
    public Dictionary<string, PropDef> Props { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PropDef
{
    public string Id { get; set; } = "";
    public string Sheet { get; set; } = "";
    public int Width { get; set; } = 64;
    public int Height { get; set; } = 64;
}
