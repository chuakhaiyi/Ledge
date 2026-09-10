namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using Ledge.Core.Services;
using Ledge.Core.Models;

public partial class SettingsPanel : UserControl
{
    private readonly SettingsStore _settingsStore;

    public SettingsPanel(SettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        InitializeComponent();
        DataContext = _settingsStore;
    }

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        DataPathText.Text = _settingsStore.DataPath;
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = System.IO.Path.GetDirectoryName(_settingsStore.DataPath);
        if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
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
            _settingsStore.ShowDockOnHover = true;
            _settingsStore.StartWithWindows = false;
            _settingsStore.PortableMode = false;
            _settingsStore.NewNoteHotkey = new HotkeyBinding(1 | 2, (int)VirtualKey.N);
            _settingsStore.LibraryHotkey = new HotkeyBinding(1 | 2, (int)VirtualKey.A);
            _settingsStore.ArchiveHotkey = new HotkeyBinding(1 | 2, (int)VirtualKey.L);
            _settingsStore.FocusDockHotkey = new HotkeyBinding(1 | 2, (int)VirtualKey.D);
        }
    }

}
