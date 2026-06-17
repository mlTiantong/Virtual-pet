using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DesktopPet.App.Services;

public sealed class AnimationCatalog
{
    private readonly string _assetRoot;
    private readonly Dictionary<string, BitmapSource> _imageCache = new(StringComparer.OrdinalIgnoreCase);

    public string DefaultAnimation { get; private init; } = "idle";
    public Dictionary<string, AnimationSpec> Animations { get; private init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<HitRegionSpec> HitRegions { get; private init; } = new();

    private AnimationCatalog(string assetRoot)
    {
        _assetRoot = assetRoot;
    }

    public static AnimationCatalog Load(string assetRoot)
    {
        var manifestPath = Path.Combine(assetRoot, "animation-manifest.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<AnimationManifest>(json, options)
                       ?? throw new InvalidOperationException("animation-manifest.json is invalid.");

        return new AnimationCatalog(assetRoot)
        {
            DefaultAnimation = string.IsNullOrWhiteSpace(manifest.DefaultAnimation) ? "idle" : manifest.DefaultAnimation,
            Animations = manifest.Animations ?? new Dictionary<string, AnimationSpec>(StringComparer.OrdinalIgnoreCase),
            HitRegions = manifest.HitRegions ?? new List<HitRegionSpec>()
        };
    }

    public AnimationSpec Get(string id)
    {
        if (Animations.TryGetValue(id, out var spec)) return spec;
        if (Animations.TryGetValue(DefaultAnimation, out var fallback)) return fallback;
        throw new KeyNotFoundException($"Animation '{id}' and default animation '{DefaultAnimation}' were not found.");
    }

    public bool Has(string id) => Animations.ContainsKey(id);

    public BitmapSource LoadFrame(AnimationSpec spec, int frameIndex)
    {
        if (spec.IsSpriteSheet)
        {
            var count = Math.Max(1, spec.FrameCount);
            frameIndex = Math.Clamp(frameIndex, 0, count - 1);
            var key = $"sheet:{spec.Sheet}:{frameIndex}";
            if (_imageCache.TryGetValue(key, out var cached)) return cached;

            var sheet = LoadImage(spec.Sheet!);
            var columns = Math.Max(1, spec.Columns);
            var rows = Math.Max(1, spec.Rows);
            var frameWidth = spec.FrameWidth > 0 ? spec.FrameWidth : sheet.PixelWidth / columns;
            var frameHeight = spec.FrameHeight > 0 ? spec.FrameHeight : sheet.PixelHeight / rows;
            var col = frameIndex % columns;
            var row = frameIndex / columns;
            var x = Math.Clamp(col * frameWidth, 0, Math.Max(0, sheet.PixelWidth - 1));
            var y = Math.Clamp(row * frameHeight, 0, Math.Max(0, sheet.PixelHeight - 1));
            frameWidth = Math.Min(frameWidth, sheet.PixelWidth - x);
            frameHeight = Math.Min(frameHeight, sheet.PixelHeight - y);

            var cropped = new CroppedBitmap(sheet, new Int32Rect(x, y, frameWidth, frameHeight));
            cropped.Freeze();
            _imageCache[key] = cropped;
            return cropped;
        }

        if (spec.Frames.Count == 0) throw new InvalidOperationException($"Animation '{spec.Id}' has no frames.");
        frameIndex = Math.Clamp(frameIndex, 0, spec.Frames.Count - 1);
        return LoadImage(spec.Frames[frameIndex]);
    }

    public BitmapSource LoadImage(string relativePath)
    {
        if (_imageCache.TryGetValue(relativePath, out var cached)) return cached;

        var path = Path.Combine(_assetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        _imageCache[relativePath] = bitmap;
        return bitmap;
    }

    public int GetFrameCount(AnimationSpec spec)
    {
        if (spec.IsSpriteSheet) return Math.Max(1, spec.FrameCount);
        return Math.Max(1, spec.Frames.Count);
    }

    public void Preload(params string[] animationIds)
    {
        foreach (var id in animationIds)
        {
            if (!Animations.TryGetValue(id, out var spec)) continue;
            if (spec.IsSpriteSheet)
            {
                _ = LoadImage(spec.Sheet!);
                var count = GetFrameCount(spec);
                for (var i = 0; i < count; i++) _ = LoadFrame(spec, i);
                continue;
            }

            foreach (var frame in spec.Frames) _ = LoadImage(frame);
        }
    }
}

public sealed class AnimationManifest
{
    public int Schema { get; set; }
    public string CharacterId { get; set; } = "blue_girl_m1";
    public string DefaultAnimation { get; set; } = "idle";
    public Dictionary<string, AnimationSpec>? Animations { get; set; }
    public List<HitRegionSpec>? HitRegions { get; set; }
}

public sealed class AnimationSpec
{
    public string Id { get; set; } = "idle";
    public string Type { get; set; } = "frames";
    public int Fps { get; set; } = 8;
    public bool Loop { get; set; }
    public int DurationMs { get; set; } = 1200;
    public List<string> Frames { get; set; } = new();
    public string? Sheet { get; set; }
    public int Columns { get; set; } = 4;
    public int Rows { get; set; } = 4;
    public int FrameCount { get; set; } = 16;
    public int FrameWidth { get; set; } = 256;
    public int FrameHeight { get; set; } = 256;
    public bool ReturnToIdle { get; set; } = true;

    public bool IsSpriteSheet => string.Equals(Type, "spritesheet", StringComparison.OrdinalIgnoreCase)
                                 || !string.IsNullOrWhiteSpace(Sheet);
}

public sealed class HitRegionSpec
{
    public string Id { get; set; } = "none";
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
}
