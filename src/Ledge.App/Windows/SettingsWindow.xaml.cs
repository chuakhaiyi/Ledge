namespace Ledge.App.Windows;

using System.Windows;
using System.Windows.Input;
using Ledge.Core.Services;
using Ledge.Core.Models;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settingsStore;

    public SettingsWindow(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        DataContext = _settingsStore;
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var persistence = App.GetService<FilePersistence>();
        DataPathText.Text = persistence.GetStorePath();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        var persistence = App.GetService<FilePersistence>();
        var folder = System.IO.Path.GetDirectoryName(persistence.GetStorePath());
        if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start("explorer.exe", folder);
        }
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Reset all settings to defaults? This cannot be undone.",
            "Confirm Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _settingsStore.DockEdge = DockEdge.Right;
            _settingsStore.Theme = AppTheme.System;
            _settingsStore.StartWithWindows = false;
            _settingsStore.PortableMode = false;
            _settingsStore.NewNoteHotkey = new HotkeyBinding(1 | 2, (int)Key.N);
            _settingsStore.LibraryHotkey = new HotkeyBinding(1 | 2, (int)Key.A);
            _settingsStore.ArchiveHotkey = new HotkeyBinding(1 | 2, (int)Key.L);
            _settingsStore.FocusDockHotkey = new HotkeyBinding(1 | 2, (int)Key.D);
        }
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}