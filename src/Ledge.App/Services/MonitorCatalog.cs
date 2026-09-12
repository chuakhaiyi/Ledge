namespace Ledge.App.Services;

using System.Windows.Forms;

public sealed record DockMonitor(string Id, string Label, int Left, int Top, int Width, int Height, bool IsPrimary)
{
    public override string ToString() => Label;
}

public static class MonitorCatalog
{
    public static IReadOnlyList<DockMonitor> GetAvailable()
        => Screen.AllScreens.Select((screen, index) => new DockMonitor(
            screen.DeviceName,
            $"{index + 1} — {(screen.Primary ? "Primary" : "Display")} ({screen.WorkingArea.Width}×{screen.WorkingArea.Height})",
            screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height, screen.Primary)).ToList();

    public static DockMonitor Resolve(string? id)
    {
        var screens = GetAvailable();
        return screens.FirstOrDefault(screen => string.Equals(screen.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? screens.FirstOrDefault(screen => screen.IsPrimary)
            ?? screens[0];
    }
}
