namespace Ledge.App.Services;

using System;
using System.Windows;
using Ledge.Core.Services;
using Ledge.Core.Models;
using Ledge.App.Windows;
using Ledge.App.Controls;

public sealed class WindowManager
{
    private NoteStore? _noteStore;
    private SettingsStore? _settingsStore;
    private DockWindow? _dockWindow;
    private LibraryWindow? _libraryWindow;
    private readonly Dictionary<string, NoteWindow> _noteWindows = new();
    private ToastManager? _toastManager;
    private bool _dockHidden;

    public bool IsDockHidden => _dockHidden;

    public void Initialize(NoteStore noteStore, SettingsStore settingsStore)
    {
        _noteStore = noteStore;
        _settingsStore = settingsStore;
        _toastManager = new ToastManager();
    }

    public void ShowDock()
    {
        if (_dockHidden) return;
        if (_dockWindow == null)
        {
            _dockWindow = new DockWindow(_noteStore!, _settingsStore!);
            _dockWindow.Closed += (_, _) => _dockWindow = null;
        }
        _dockWindow.Show();
        _dockWindow.UpdatePosition();
    }

    public void HideDock()
    {
        _dockWindow?.Hide();
    }

    public void ToggleNotesVisibility()
    {
        if (_dockHidden)
        {
            _dockHidden = false;
            ShowDock();
            return;
        }

        _dockHidden = true;
        _dockWindow?.Collapse();
        _dockWindow?.Hide();
    }

    public void CreateNewNote()
    {
        var note = _noteStore!.Add();
        ShowNoteWindow(note);
    }

    public void ShowNoteWindow(Note note)
    {
        var current = _noteStore!.Notes.FirstOrDefault(n => n.Id == note.Id);
        if (current == null) return;
        note = current;
        if (_noteWindows.TryGetValue(note.Id, out var existing))
        {
            existing.Activate();
            existing.FocusEditor();
            return;
        }

        var window = new NoteWindow(note, _noteStore!, _settingsStore!);
        window.DeleteRequested += () => DeleteNote(window.Note ?? note);
        window.Closed += (_, _) => _noteWindows.Remove(note.Id);
        _noteWindows[note.Id] = window;
        window.Show();
        window.FocusEditor();
    }

    public void DeleteNote(Note note)
    {
        CloseNoteWindow(note.Id);
        _noteStore!.Delete(note);
        
        // Show undo toast
        _toastManager?.ShowDeleteToast(note, () =>
        {
            _noteStore.UndoDelete();
        });
    }

    public void CloseNoteWindow(string noteId)
    {
        if (_noteWindows.TryGetValue(noteId, out var window))
        {
            window.Close();
        }
    }

    public void ShowLibrary()
    {
        if (_libraryWindow == null)
        {
            _libraryWindow = new LibraryWindow(_noteStore!, _settingsStore!);
            _libraryWindow.Closed += (_, _) => _libraryWindow = null;
        }
        _libraryWindow.Show();
        if (_libraryWindow.WindowState == WindowState.Minimized) _libraryWindow.WindowState = WindowState.Normal;
        _libraryWindow.ShowNotes();
        _libraryWindow.Activate();
    }

    public void ShowArchive()
    {
        ShowLibrary();
        _libraryWindow?.ShowArchive();
    }

    public void ShowSettings()
    {
        ShowLibrary();
        _libraryWindow!.ShowSettings();
    }

    public void FocusDock()
    {
        if (_dockHidden) return;
        _dockWindow?.FocusDock();
    }

    public void UpdateDockPosition()
    {
        _dockWindow?.UpdatePosition();
    }

    public void OnDisplayChanged()
    {
        UpdateDockPosition();
        foreach (var window in _noteWindows.Values)
        {
            window.EnsureOnScreen();
        }
    }
}
