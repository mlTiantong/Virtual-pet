using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.App.Models;

namespace DesktopPet.App.Services;

public sealed class HitTestService
{
    private readonly IReadOnlyList<HitRegionSpec> _regions;

    public HitTestService(IReadOnlyList<HitRegionSpec> regions)
    {
        _regions = regions;
    }

    public bool IsOpaque(BitmapSource? bitmap, Point imagePoint, Size imageRenderSize, byte alphaThreshold = 10)
    {
        if (bitmap is null || imageRenderSize.Width <= 0 || imageRenderSize.Height <= 0) return false;
        if (imagePoint.X < 0 || imagePoint.Y < 0 || imagePoint.X >= imageRenderSize.Width || imagePoint.Y >= imageRenderSize.Height) return false;

        var pixelX = Math.Clamp((int)(imagePoint.X / imageRenderSize.Width * bitmap.PixelWidth), 0, bitmap.PixelWidth - 1);
        var pixelY = Math.Clamp((int)(imagePoint.Y / imageRenderSize.Height * bitmap.PixelHeight), 0, bitmap.PixelHeight - 1);
        var converted = bitmap.Format == PixelFormats.Bgra32 || bitmap.Format == PixelFormats.Pbgra32
            ? bitmap
            : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[4];
        converted.CopyPixels(new Int32Rect(pixelX, pixelY, 1, 1), pixels, 4, 0);
        return pixels[3] > alphaThreshold;
    }

    public PetHitRegion HitTest(Point imagePoint, Size imageRenderSize, BitmapSource? bitmap)
    {
        if (!IsOpaque(bitmap, imagePoint, imageRenderSize)) return PetHitRegion.None;
        var nx = imagePoint.X / imageRenderSize.Width;
        var ny = imagePoint.Y / imageRenderSize.Height;

        foreach (var r in _regions)
        {
            if (nx >= r.X && nx <= r.X + r.W && ny >= r.Y && ny <= r.Y + r.H)
            {
                return r.Id.ToLowerInvariant() switch
                {
                    "head" => PetHitRegion.Head,
                    "face" => PetHitRegion.Face,
                    "hair" => PetHitRegion.Hair,
                    "hand" => PetHitRegion.Hand,
                    "body" => PetHitRegion.Body,
                    "outfit" => PetHitRegion.Outfit,
                    "accessory" => PetHitRegion.Accessory,
                    "feet" => PetHitRegion.Feet,
                    _ => PetHitRegion.None
                };
            }
        }

        return PetHitRegion.Body;
    }
}
