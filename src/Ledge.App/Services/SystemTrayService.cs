namespace Ledge.App.Services;

using System;
using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;

public sealed class SystemTrayService : IDisposable
{
    private readonly TaskbarIcon _notifyIcon;
    private WindowManager? _windowManager;
    private bool _iconSet = false;
    private bool _disposed;

    public SystemTrayService()
    {
        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "Ledge — Sticky Notes"
        };

        _notifyIcon.TrayLeftMouseUp += (_, _) => _windowManager?.ShowDock();
        _notifyIcon.TrayRightMouseUp += (_, _) => ShowContextMenu();
    }

    public void Initialize(WindowManager windowManager)
    {
        _windowManager = windowManager;
        SetIcon();
    }

    private void SetIcon()
    {
        if (_iconSet) return;

        // 1. Try to extract associated icon from current executable
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
            {
                var appIcon = Icon.ExtractAssociatedIcon(exePath);
                if (appIcon != null)
                {
                    _notifyIcon.Icon = appIcon;
                    _iconSet = true;
                    return;
                }
            }
        }
        catch { }

        // 2. Try to get icon from pack URIs
        string[] resourceUris = [
            "pack://application:,,,/Ledge.App;component/Resources/icon.ico",
            "pack://application:,,,/Ledge.App;component/assets/icon.ico",
            "pack://application:,,,/icon.ico"
        ];

        foreach (var uriStr in resourceUris)
        {
            try
            {
                var uri = new Uri(uriStr, UriKind.Absolute);
                var stream = Application.GetResourceStream(uri)?.Stream;
                if (stream != null)
                {
                    using (stream)
                    {
                        _notifyIcon.Icon = new Icon(stream);
                        _iconSet = true;
                        return;
                    }
                }
            }
            catch { }
        }

        // 3. Try to find icon.ico on disk relative to exe or working directory
        string[] diskPaths = [
            System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "icon.ico"),
            System.IO.Path.Combine(AppContext.BaseDirectory, "icon.ico"),
            "assets/icon.ico"
        ];

        foreach (var path in diskPaths)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    _notifyIcon.Icon = new Icon(path);
                    _iconSet = true;
                    return;
                }
            }
            catch { }
        }

        // 4. Fallback: Draw a crisp, distinct sticky note icon
        _notifyIcon.Icon = CreateDefaultIcon();
        _iconSet = true;
    }

    private static Icon CreateDefaultIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Note body in warm amber/copper tone
        using var noteBrush = new SolidBrush(Color.FromArgb(235, 140, 60));
        using var borderPen = new Pen(Color.FromArgb(160, 80, 25), 1f);
        using var linePen = new Pen(Color.FromArgb(255, 255, 255), 1f);
        using var foldBrush = new SolidBrush(Color.FromArgb(181, 100, 44));

        g.FillRectangle(noteBrush, 1, 1, 13, 14);
        g.DrawRectangle(borderPen, 1, 1, 13, 14);

        // Corner fold
        g.FillPolygon(foldBrush, new[] {
            new System.Drawing.Point(9, 1),
            new System.Drawing.Point(14, 6),
            new System.Drawing.Point(9, 6)
        });

        // Note lines
        g.DrawLine(linePen, 3, 7, 10, 7);
        g.DrawLine(linePen, 3, 10, 10, 10);
        g.DrawLine(linePen, 3, 12, 7, 12);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void ShowContextMenu()
    {
        if (_windowManager == null) return;

        var menu = new System.Windows.Controls.ContextMenu();

        var newNote = new System.Windows.Controls.MenuItem { Header = "New Note" };
        newNote.Click += (_, _) =>
        {
            CloseContextMenu(menu);
            _windowManager.CreateNewNote();
        };

        var allNotes = new System.Windows.Controls.MenuItem { Header = "All Notes" };
        allNotes.Click += (_, _) =>
        {
            CloseContextMenu(menu);
            _windowManager.ShowLibrary();
        };

        var archived = new System.Windows.Controls.MenuItem { Header = "Archived" };
        archived.Click += (_, _) =>
        {
            CloseContextMenu(menu);
            _windowManager.ShowArchive();
        };

        var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settings.Click += (_, _) =>
        {
            CloseContextMenu(menu);
            _windowManager.ShowSettings();
        };

        var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exit.Click += (_, _) =>
        {
            CloseContextMenu(menu);
            Application.Current?.Shutdown();
        };

        menu.Items.Add(newNote);
        menu.Items.Add(allNotes);
        menu.Items.Add(archived);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(settings);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exit);

        _notifyIcon.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void CloseContextMenu(System.Windows.Controls.ContextMenu menu)
    {
        menu.IsOpen = false;
        if (ReferenceEquals(_notifyIcon.ContextMenu, menu))
        {
            _notifyIcon.ContextMenu = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _notifyIcon.ContextMenu = null;
        _notifyIcon.Visibility = Visibility.Hidden;
        _notifyIcon.Dispose();
    }
}