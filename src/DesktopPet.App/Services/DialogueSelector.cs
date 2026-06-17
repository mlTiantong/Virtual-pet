using System.IO;
using System.Text.Json;

namespace DesktopPet.App.Services;

public sealed class DialogueSelector
{
    private readonly Dictionary<string, string[]> _lines;
    private readonly Dictionary<string, string> _lastLineByCategory = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastPickedAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();

    private DialogueSelector(Dictionary<string, string[]> lines)
    {
        _lines = lines;
    }

    public static DialogueSelector Load(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var catalog = JsonSerializer.Deserialize<DialogueCatalog>(File.ReadAllText(path), options) ?? new DialogueCatalog();
        return new DialogueSelector(catalog.Lines ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
    }

    public string Pick(string category, string fallback = "嗯？", int cooldownMs = 900)
    {
        if (_lastPickedAt.TryGetValue(category, out var last) &&
            (DateTimeOffset.Now - last).TotalMilliseconds < cooldownMs &&
            _lastLineByCategory.TryGetValue(category, out var lastLine))
        {
            return lastLine;
        }

        if (!_lines.TryGetValue(category, out var candidates) || candidates.Length == 0)
        {
            return fallback;
        }

        var line = candidates[_random.Next(candidates.Length)];
        if (candidates.Length > 1 && _lastLineByCategory.TryGetValue(category, out var previous))
        {
            for (var i = 0; i < 4 && line == previous; i++)
            {
                line = candidates[_random.Next(candidates.Length)];
            }
        }

        _lastLineByCategory[category] = line;
        _lastPickedAt[category] = DateTimeOffset.Now;
        return line;
    }
}

public sealed class DialogueCatalog
{
    public int Schema { get; set; }
    public string Locale { get; set; } = "zh-CN";
    public Dictionary<string, string[]>? Lines { get; set; }
}
