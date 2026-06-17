using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DesktopPet.App.Models;

namespace DesktopPet.App.Services;

public sealed class PropLayerService
{
    private readonly Canvas _canvas;
    private readonly string _assetRoot;
    private readonly Dictionary<string, Image> _activeProps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BitmapSource> _propCache = new(StringComparer.OrdinalIgnoreCase);
    private PropManifest _manifest;

    public PropLayerService(Canvas canvas, string assetRoot)
    {
        _canvas = canvas;
        _assetRoot = assetRoot;
        _manifest = new PropManifest();
    }

    public void LoadManifest()
    {
        var path = System.IO.Path.Combine(_assetRoot, "prop-manifest.m8.json");
        if (!System.IO.File.Exists(path)) return;

        var json = System.IO.File.ReadAllText(path);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        _manifest = System.Text.Json.JsonSerializer.Deserialize<PropManifest>(json, options)
                     ?? new PropManifest();
    }

    public Image? ShowProp(string propId, double x, double y)
    {
        if (!_manifest.Props.TryGetValue(propId, out var def)) return null;

        if (_activeProps.TryGetValue(propId, out var oldImage))
            _canvas.Children.Remove(oldImage);

        var bitmap = LoadPropImage(def);
        var image = new Image
        {
            Source = bitmap,
            Width = def.Width,
            Height = def.Height,
            Stretch = System.Windows.Media.Stretch.Uniform,
            RenderTransformOrigin = new Point(0.5, 0.5),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(image, x);
        Canvas.SetTop(image, y);
        _canvas.Children.Add(image);
        _activeProps[propId] = image;
        return image;
    }

    public void HideProp(string propId)
    {
        if (!_activeProps.TryGetValue(propId, out var image)) return;
        _canvas.Children.Remove(image);
        _activeProps.Remove(propId);
    }

    public Image? GetProp(string propId)
    {
        return _activeProps.TryGetValue(propId, out var image) ? image : null;
    }

    public void HideAllProps()
    {
        foreach (var kv in _activeProps)
        {
            _canvas.Children.Remove(kv.Value);
        }
        _activeProps.Clear();
    }

    private BitmapSource LoadPropImage(PropDef def)
    {
        if (_propCache.TryGetValue(def.Id, out var cached)) return cached;

        var path = System.IO.Path.Combine(_assetRoot, "props", def.Sheet.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(path))
        {
            var placeholder = BitmapSource.Create(def.Width, def.Height, 96, 96,
                System.Windows.Media.PixelFormats.Pbgra32, null, new byte[def.Width * def.Height * 4], def.Width * 4);
            placeholder.Freeze();
            _propCache[def.Id] = placeholder;
            return placeholder;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        _propCache[def.Id] = bitmap;
        return bitmap;
    }
}
