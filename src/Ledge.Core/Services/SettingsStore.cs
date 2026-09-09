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
        set => Set(s => s.DockEdge, value);
    }

    public AppTheme Theme
    {
        get => _settings.Theme;
        set => Set(s => s.Theme, value);
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows != value)
            {
                Set(s => s.StartWithWindows, value);
                ApplyStartWithWindows(value);
            }
        }
    }

    public bool PortableMode
    {
        get => _settings.PortableMode;
        set => Set(s => s.PortableMode, value);
    }

    public bool ShowDockOnHover
    {
        get => _settings.ShowDockOnHover;
        set => Set(s => s.ShowDockOnHover, value);
    }

    public HotkeyBinding NewNoteHotkey
    {
        get => _settings.NewNoteHotkey;
        set => Set(s => s.NewNoteHotkey, value);
    }

    public HotkeyBinding LibraryHotkey
    {
        get => _settings.LibraryHotkey;
        set => Set(s => s.LibraryHotkey, value);
    }

    public HotkeyBinding ArchiveHotkey
    {
        get => _settings.ArchiveHotkey;
        set => Set(s => s.ArchiveHotkey, value);
    }

    public HotkeyBinding FocusDockHotkey
    {
        get => _settings.FocusDockHotkey;
        set => Set(s => s.FocusDockHotkey, value);
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
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
                key.SetValue(AppRunValueName, $"\"{exePath}\"");
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

    public async Task SaveAsync() => await _persistence.SaveSettingsAsync(_settings);

    private void Set<T>(Func<Settings, T> selector, T value)
    {
        var propName = GetPropertyName(selector);
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

    private static string GetPropertyName<T>(Func<Settings, T> selector)
    {
        return selector.Method.Name switch
        {
            "get_DockEdge" => nameof(Settings.DockEdge),
            "get_Theme" => nameof(Settings.Theme),
            "get_StartWithWindows" => nameof(Settings.StartWithWindows),
            "get_PortableMode" => nameof(Settings.PortableMode),
            "get_ShowDockOnHover" => nameof(Settings.ShowDockOnHover),
            "get_NewNoteHotkey" => nameof(Settings.NewNoteHotkey),
            "get_LibraryHotkey" => nameof(Settings.LibraryHotkey),
            "get_ArchiveHotkey" => nameof(Settings.ArchiveHotkey),
            "get_FocusDockHotkey" => nameof(Settings.FocusDockHotkey),
            _ => throw new ArgumentException("Unknown selector")
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}