namespace Ledge.Core.Services;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Ledge.Core.Models;

public sealed class NoteStore : INotifyPropertyChanged, IDisposable
{
    private readonly ObservableCollection<Note> _notes = [];
    private readonly FilePersistence _persistence;
    private readonly Timer _saveTimer;
    private Note? _pendingDelete;
    private Timer? _undoTimer;
    // ponytail: one save lock; use an async queue if large stores stall editing.
    private readonly object _saveLock = new();
    private Note[] _saveSnapshot = [];
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<Note>? NoteDeleted;
    public event Action<Note>? NoteRestored;

    public IReadOnlyList<Note> Notes => _notes;
    public IReadOnlyList<Note> VisibleNotes => _notes.Where(n => !n.Archived).OrderByDescending(n => n.ModifiedAt).ToList();
    public IReadOnlyList<Note> PinnedNotes => _notes.Where(n => n.Pinned && !n.Archived).OrderByDescending(n => n.ModifiedAt).ToList();
    public IReadOnlyList<Note> UnpinnedNotes => _notes.Where(n => !n.Pinned && !n.Archived).OrderByDescending(n => n.ModifiedAt).ToList();
    public IReadOnlyList<Note> ArchivedNotes => _notes.Where(n => n.Archived).OrderByDescending(n => n.ModifiedAt).ToList();

    public NoteStore(FilePersistence persistence)
    {
        _persistence = persistence;
        _saveTimer = new Timer(OnSaveTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        _notes.CollectionChanged += OnCollectionChanged;
    }

    private void OnSaveTimerElapsed(object? state)
    {
        lock (_saveLock)
        {
            if (_disposed) return;
            try { _persistence.SaveAsync(_saveSnapshot).GetAwaiter().GetResult(); }
            catch (Exception exception) { System.Diagnostics.Trace.TraceError("Note save failed: {0}", exception); }
        }
    }

    public async Task LoadAsync()
    {
        var data = await _persistence.LoadAsync();
        _notes.Clear();
        foreach (var note in data.Notes)
        {
            _notes.Add(note);
        }
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(VisibleNotes));
        OnPropertyChanged(nameof(PinnedNotes));
        OnPropertyChanged(nameof(UnpinnedNotes));
        OnPropertyChanged(nameof(ArchivedNotes));
    }

    public Note Add(string text = "", NoteColor color = NoteColor.Moss)
    {
        var note = Note.Create(text, color.ToString());
        _notes.Insert(0, note);
        ScheduleSave();
        return note;
    }

    public Note Add(string text, string color)
    {
        var note = Note.Create(text, color);
        _notes.Insert(0, note);
        ScheduleSave();
        return note;
    }

    public void Update(Note note)
    {
        var index = _notes.IndexOf(_notes.FirstOrDefault(n => n.Id == note.Id));
        if (index >= 0)
        {
            _notes[index] = note with { ModifiedAt = DateTime.UtcNow };
            ScheduleSave();
        }
    }

    public void Delete(Note note)
    {
        _undoTimer?.Dispose();
        _pendingDelete = _notes.FirstOrDefault(n => n.Id == note.Id);
        if (_pendingDelete == null) return;
        _notes.Remove(_pendingDelete);
        NoteDeleted?.Invoke(note);
        ScheduleSave();

        _undoTimer = new Timer(_ =>
        {
            _pendingDelete = null;
            _undoTimer?.Dispose();
            _undoTimer = null;
        }, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
    }

    public bool UndoDelete()
    {
        if (_pendingDelete is null) return false;

        _notes.Insert(0, _pendingDelete);
        NoteRestored?.Invoke(_pendingDelete);
        _pendingDelete = null;
        _undoTimer?.Dispose();
        _undoTimer = null;
        ScheduleSave();
        return true;
    }

    public void Archive(Note note) => Update(note with { Archived = true });
    public void Unarchive(Note note) => Update(note with { Archived = false });
    public void Pin(Note note) => Update(note with { Pinned = true });
    public void Unpin(Note note) => Update(note with { Pinned = false });
    public void SetColor(Note note, string color)
        => Update(note with { Color = NoteColorExtensions.IsPresetName(color) ? color : nameof(NoteColor.Moss) });
    public void SetText(Note note, string text) => Update(note with { Text = text });
    public void SetPosition(Note note, NotePosition position) => Update(note with { Position = position });

    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _notes.Count || newIndex < 0 || newIndex >= _notes.Count) return;
        var note = _notes[oldIndex];
        _notes.RemoveAt(oldIndex);
        _notes.Insert(newIndex, note);
        ScheduleSave();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(VisibleNotes));
        OnPropertyChanged(nameof(PinnedNotes));
        OnPropertyChanged(nameof(UnpinnedNotes));
        OnPropertyChanged(nameof(ArchivedNotes));
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        lock (_saveLock)
        {
            if (_disposed) return;
            _saveSnapshot = _notes.Select(n => n with { }).ToArray();
            _saveTimer.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
        }
    }

    public void Save() => ForceSave();

    public void ForceSave()
    {
        lock (_saveLock)
        {
            _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _saveSnapshot = _notes.Select(n => n with { }).ToArray();
            _persistence.SaveAsync(_saveSnapshot).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        lock (_saveLock)
        {
            _disposed = true;
            _saveTimer.Dispose();
            _undoTimer?.Dispose();
            _notes.CollectionChanged -= OnCollectionChanged;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
