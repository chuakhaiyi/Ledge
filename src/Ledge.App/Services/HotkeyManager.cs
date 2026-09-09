namespace Ledge.App.Services;

using System.Windows.Input;
using System.Windows.Threading;
using Ledge.Native.Interop;
using Ledge.Core.Services;
using Ledge.Core.Models;

public sealed class HotkeyManager : IDisposable
{
    private readonly HotKeyWindow _hotkeyWindow;
    private SettingsStore? _settingsStore;
    private WindowManager? _windowManager;
    private int _newNoteId;
    private int _libraryId;
    private int _archiveId;
    private int _focusDockId;

    public HotkeyManager()
    {
        _hotkeyWindow = new HotKeyWindow(Dispatcher.CurrentDispatcher);
    }

    public void Initialize(SettingsStore settingsStore, WindowManager windowManager)
    {
        _settingsStore = settingsStore;
        _windowManager = windowManager;

        RegisterAll();
        _settingsStore.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsStore.NewNoteHotkey)
            or nameof(SettingsStore.LibraryHotkey)
            or nameof(SettingsStore.ArchiveHotkey)
            or nameof(SettingsStore.FocusDockHotkey))
        {
            RegisterAll();
        }
    }

    private void RegisterAll()
    {
        if (_settingsStore is null) return;

        UnregisterAll();

        _newNoteId = RegisterHotkey(_settingsStore.NewNoteHotkey, () => _windowManager?.CreateNewNote());
        _libraryId = RegisterHotkey(_settingsStore.LibraryHotkey, () => _windowManager?.ShowLibrary());
        _archiveId = RegisterHotkey(_settingsStore.ArchiveHotkey, () => _windowManager?.ShowArchive());
        _focusDockId = RegisterHotkey(_settingsStore.FocusDockHotkey, () => _windowManager?.FocusDock());
    }

    private int RegisterHotkey(HotkeyBinding binding, Action action)
    {
        if (binding.IsEmpty) return 0;
        return _hotkeyWindow.Register(binding.Modifiers, binding.Key, action);
    }

    private void UnregisterAll()
    {
        if (_newNoteId != 0) _hotkeyWindow.Unregister(_newNoteId);
        if (_libraryId != 0) _hotkeyWindow.Unregister(_libraryId);
        if (_archiveId != 0) _hotkeyWindow.Unregister(_archiveId);
        if (_focusDockId != 0) _hotkeyWindow.Unregister(_focusDockId);
        _newNoteId = _libraryId = _archiveId = _focusDockId = 0;
    }

    public void Dispose()
    {
        _settingsStore.PropertyChanged -= OnSettingsChanged;
        UnregisterAll();
        _hotkeyWindow.Dispose();
    }
}