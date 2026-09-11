namespace Ledge.App.Services;

using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;

public sealed class SystemTrayService : IDisposable
{
    private readonly TaskbarIcon _notifyIcon;
    private readonly Icon _icon;
    private WindowManager? _windowManager;
    private ContextMenu? _contextMenu;
    private bool _disposed;

    public SystemTrayService()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Ledge.App;component/assets/icon.ico"))!.Stream;
        _icon = new Icon(stream);
        _notifyIcon = new TaskbarIcon { ToolTipText = "Ledge — Sticky Notes", Icon = _icon };
        _notifyIcon.TrayMouseDoubleClick += (_, _) => DeferTrayAction(() => _windowManager?.ShowLibrary());
        _notifyIcon.TrayRightMouseUp += (_, _) => ShowContextMenu();
    }

    public void Initialize(WindowManager windowManager) => _windowManager = windowManager;

    private void ShowContextMenu()
    {
        if (_windowManager == null) return;
        if (_contextMenu != null) _contextMenu.IsOpen = false;
        // Assigning TaskbarIcon.ContextMenu makes its native callback reposition
        // the popup again at the hidden tray window. Own the WPF popup instead.
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint, StaysOpen = false };
        _contextMenu = menu;
        void Add(string title, Action action)
        {
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => { menu.IsOpen = false; DeferTrayAction(action); };
            menu.Items.Add(item);
        }
        Add("New Note", _windowManager.CreateNewNote);
        Add("All Notes", _windowManager.ShowLibrary);
        Add("Archived", _windowManager.ShowArchive);
        Add(_windowManager.IsDockHidden ? "Unhide notes" : "Hide notes", _windowManager.ToggleNotesVisibility);
        menu.Items.Add(new Separator());
        Add("Settings", _windowManager.ShowSettings);
        menu.Items.Add(new Separator());
        Add("Exit Ledge", () => Application.Current.Shutdown());
        menu.Closed += (_, _) => { if (ReferenceEquals(_contextMenu, menu)) _contextMenu = null; };
        menu.AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent,
            new MouseButtonEventHandler((_, _) => menu.IsOpen = false));
        menu.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            menu.IsOpen = false;
            e.Handled = true;
        };
        menu.IsOpen = true;
    }

    private static void DeferTrayAction(Action action)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try { action(); }
            catch (Exception exception)
            {
                MessageBox.Show($"The requested action could not be completed.\n\n{exception.Message}", "Ledge", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_contextMenu != null) _contextMenu.IsOpen = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
