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

        try
        {
            // Try to get the app icon from resources
            var uri = new Uri("pack://application:,,,/Ledge.App;component/assets/icon.ico", UriKind.Absolute);
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream != null)
            {
                _notifyIcon.Icon = new Icon(stream);
                _iconSet = true;
            }
        }
        catch
        {
            // Fallback: create a simple icon programmatically
            _notifyIcon.Icon = CreateDefaultIcon();
            _iconSet = true;
        }
    }

    private static Icon CreateDefaultIcon()
    {
        var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.FillRectangle(new SolidBrush(Color.FromArgb(181, 100, 44)), 2, 2, 12, 12); // Copper color
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void ShowContextMenu()
    {
        if (_windowManager == null) return;

        var menu = new System.Windows.Controls.ContextMenu
        {
            ItemsSource = new object[]
            {
                new System.Windows.Controls.MenuItem
                {
                    Header = "New Note",
                    Command = new RelayCommand(() => _windowManager?.CreateNewNote())
                },
                new System.Windows.Controls.MenuItem
                {
                    Header = "All Notes",
                    Command = new RelayCommand(() => _windowManager?.ShowLibrary())
                },
                new System.Windows.Controls.MenuItem
                {
                    Header = "Archived",
                    Command = new RelayCommand(() => _windowManager?.ShowArchive())
                },
                new System.Windows.Controls.Separator(),
                new System.Windows.Controls.MenuItem
                {
                    Header = "Settings",
                    Command = new RelayCommand(() => _windowManager?.ShowSettings())
                },
                new System.Windows.Controls.Separator(),
                new System.Windows.Controls.MenuItem
                {
                    Header = "Exit",
                    Command = new RelayCommand(() => Application.Current?.Shutdown())
                }
            }
        };

        _notifyIcon.ContextMenu = menu;
        menu.IsOpen = true;
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}

public sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    public event EventHandler? CanExecuteChanged;
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}