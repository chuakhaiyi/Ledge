namespace Ledge.App.Services;

using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Text;
using Ledge.Native.Interop;

public sealed class FullscreenDetector : IDisposable
{
    private readonly DispatcherTimer _timer;
    private WindowManager? _windowManager;
    private nint _lastForeground = nint.Zero;

    public FullscreenDetector()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += CheckFullscreen;
    }

    public void Initialize(WindowManager windowManager)
    {
        _windowManager = windowManager;
        _timer.Start();
    }

    private void CheckFullscreen(object? sender, EventArgs e)
    {
        if (_windowManager == null) return;

        var foreground = User32.GetForegroundWindow();
        if (foreground == _lastForeground) return;
        _lastForeground = foreground;

        if (IsFullscreen(foreground))
        {
            _windowManager.HideDock();
        }
        else
        {
            _windowManager.ShowDock();
        }
    }

    private bool IsFullscreen(nint hwnd)
    {
        if (hwnd == nint.Zero) return false;
        if (IsDesktopShell(hwnd)) return false;

        User32.GetWindowRect(hwnd, out var rect);
        var monitor = User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULTTONEAREST);

        var info = new User32.MONITORINFO { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
        if (!User32.GetMonitorInfo(monitor, ref info)) return false;

        var style = User32.GetWindowLongPtr(hwnd, User32.GWL_STYLE);
        var hasCaption = (style & User32.WS_CAPTION) != 0;

        return rect.Left <= info.rcMonitor.Left &&
               rect.Top <= info.rcMonitor.Top &&
               rect.Right >= info.rcMonitor.Right &&
               rect.Bottom >= info.rcMonitor.Bottom &&
               !hasCaption;
    }

    private static bool IsDesktopShell(nint hwnd)
    {
        var className = new StringBuilder(256);
        var length = User32.GetClassName(hwnd, className, className.Capacity);
        return length > 0 && (className.ToString() is "Progman" or "WorkerW");
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= CheckFullscreen;
        _windowManager = null;
    }
}
