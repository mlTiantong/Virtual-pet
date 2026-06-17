using System.Runtime.InteropServices;

namespace DesktopPet.App.Native;

public static class NativeWindowInterop
{
    public const int WM_NCHITTEST = 0x0084;
    public const int HTCLIENT = 1;
    public const int HTTRANSPARENT = -1;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public static void SetClickThrough(IntPtr hwnd, bool enabled)
    {
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        style = enabled ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, style);
    }

    public static void SetToolWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
    }

    public static System.Windows.Point ScreenPointFromLParam(IntPtr lParam)
    {
        var raw = lParam.ToInt64();
        var x = unchecked((short)(raw & 0xFFFF));
        var y = unchecked((short)((raw >> 16) & 0xFFFF));
        return new System.Windows.Point(x, y);
    }
}
