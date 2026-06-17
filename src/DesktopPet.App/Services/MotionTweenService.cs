using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DesktopPet.App.Services;

public sealed class MotionTweenService
{
    private readonly Canvas _canvas;

    public MotionTweenService(Canvas canvas)
    {
        _canvas = canvas;
    }

    private static ScaleTransform GetOrAddScaleTransform(UIElement element)
    {
        if (element.RenderTransform is ScaleTransform st)
            return st;

        if (element.RenderTransform is TransformGroup group)
        {
            var existing = group.Children.OfType<ScaleTransform>().FirstOrDefault();
            if (existing != null) return existing;
            var newScale = new ScaleTransform(1, 1);
            group.Children.Add(newScale);
            return newScale;
        }

        var fresh = new ScaleTransform(1, 1);
        var newGroup = new TransformGroup();
        if (element.RenderTransform != null && element.RenderTransform != Transform.Identity)
            newGroup.Children.Add(element.RenderTransform);
        newGroup.Children.Add(fresh);
        element.RenderTransform = newGroup;
        return fresh;
    }

    public async Task FlyToAsync(UIElement element, Point from, Point to, int durationMs)
    {
        Canvas.SetLeft(element, from.X);
        Canvas.SetTop(element, from.Y);
        element.Visibility = Visibility.Visible;

        var tcs = new TaskCompletionSource<bool>();
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var animX = new DoubleAnimation(to.X, duration) { EasingFunction = ease };
        var animY = new DoubleAnimation(to.Y, duration) { EasingFunction = ease };

        animX.Completed += (_, _) => tcs.TrySetResult(true);
        element.BeginAnimation(Canvas.LeftProperty, animX);
        element.BeginAnimation(Canvas.TopProperty, animY);

        await tcs.Task;
    }

    public async Task ScaleToAsync(UIElement element, double fromScale, double toScale, int durationMs)
    {
        var tcs = new TaskCompletionSource<bool>();
        var duration = TimeSpan.FromMilliseconds(durationMs);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        var scaleTransform = GetOrAddScaleTransform(element);

        var animX = new DoubleAnimation(fromScale, toScale, duration);
        var animY = new DoubleAnimation(fromScale, toScale, duration);

        animX.Completed += (_, _) => tcs.TrySetResult(true);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);

        await tcs.Task;
    }

    public async Task FadeToAsync(UIElement element, double from, double to, int durationMs)
    {
        var tcs = new TaskCompletionSource<bool>();
        var duration = TimeSpan.FromMilliseconds(durationMs);

        element.Opacity = from;
        element.Visibility = Visibility.Visible;

        var anim = new DoubleAnimation(to, duration);
        anim.Completed += (_, _) =>
        {
            if (to <= 0) element.Visibility = Visibility.Collapsed;
            tcs.TrySetResult(true);
        };
        element.BeginAnimation(UIElement.OpacityProperty, anim);

        await tcs.Task;
    }

    public async Task BounceAsync(UIElement element, double amplitude, int durationMs)
    {
        var tcs = new TaskCompletionSource<bool>();
        var duration = TimeSpan.FromMilliseconds(durationMs);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        var scaleTransform = GetOrAddScaleTransform(element);

        var anim = new DoubleAnimation
        {
            From = 1,
            To = 1 + amplitude,
            Duration = TimeSpan.FromMilliseconds(durationMs * 0.3),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2)
        };
        anim.Completed += (_, _) => tcs.TrySetResult(true);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

        await tcs.Task;
    }
}
