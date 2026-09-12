namespace Ledge.Core.Services;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using Ledge.Core.Models;

public sealed class SettingsStore : INotifyPropertyChanged
{
    private readonly FilePersistence _persistence;
    private Settings _settings;
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRunValueName = "Ledge";

    public event PropertyChangedEventHandler? PropertyChanged;

    public Settings Settings => _settings;
    public string DataPath => _persistence.GetStorePath();

    public DockEdge DockEdge
    {
        get => _settings.DockEdge;
        set => Set(value);
    }

    public string? DockMonitorId
    {
        get => _settings.DockMonitorId;
        set => Set(value);
    }

    public AppTheme Theme
    {
        get => _settings.Theme;
        set => Set(value);
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows != value)
            {
                Set(value);
                ApplyStartWithWindows(value);
            }
        }
    }

    public bool PortableMode
    {
        get => _settings.PortableMode;
        set => Set(value);
    }

    public bool ShowDockOnHover
    {
        get => _settings.ShowDockOnHover;
        set => Set(value);
    }

    public HotkeyBinding NewNoteHotkey
    {
        get => _settings.NewNoteHotkey;
        set => Set(value);
    }

    public HotkeyBinding LibraryHotkey
    {
        get => _settings.LibraryHotkey;
        set => Set(value);
    }

    public HotkeyBinding ArchiveHotkey
    {
        get => _settings.ArchiveHotkey;
        set => Set(value);
    }

    public HotkeyBinding FocusDockHotkey
    {
        get => _settings.FocusDockHotkey;
        set => Set(value);
    }

    public SettingsStore(FilePersistence persistence, Settings initial)
    {
        _persistence = persistence;
        _settings = initial;
    }

    public void ApplyStartupSettings()
    {
        ApplyStartWithWindows(_settings.StartWithWindows);
    }

    private void ApplyStartWithWindows(bool enable)
    {
        try
        {
            using var key = enable
                ? Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
                : Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
                key.SetValue(AppRunValueName, $"\"{exePath}\" --background");
            }
            else
            {
                key.DeleteValue(AppRunValueName, false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }

    public Task SaveAsync() => _persistence.SaveSettingsAsync(_settings);

    private void Set<T>(T value, [CallerMemberName] string propName = "")
    {
        var prop = typeof(Settings).GetProperty(propName);
        if (prop != null && !Equals(prop.GetValue(_settings), value))
        {
            var newSettings = _settings with { };
            prop.SetValue(newSettings, value);
            _settings = newSettings;
            OnPropertyChanged(propName);
            _ = SaveAsync();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
