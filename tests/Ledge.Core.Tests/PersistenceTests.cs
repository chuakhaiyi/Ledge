namespace Ledge.Core.Tests;

using Ledge.Core.Models;
using Ledge.Core.Services;
using Xunit;

public class PersistenceTests
{
    [Fact]
    public async Task SettingsChangesPersistAndConcurrentWritesKeepAValidStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Ledge-test-" + Guid.NewGuid());
        try
        {
            var persistence = new FilePersistence(Settings.Default, directory);
            var settings = new SettingsStore(persistence, Settings.Default);
            var changed = new List<string?>();
            settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            settings.DockEdge = DockEdge.Left;
            settings.Theme = AppTheme.Dark;
            settings.ShowDockOnHover = false;
            settings.NewNoteHotkey = HotkeyBinding.Empty;
            await settings.SaveAsync();
            var loaded = await persistence.LoadAsync();
            Assert.Equal(settings.Settings, loaded.Settings);
            Assert.Contains(nameof(SettingsStore.DockEdge), changed);

            await Task.WhenAll(Enumerable.Range(0, 20).Select(i => persistence.SaveAsync([Note.Create($"Note {i}")])));
            using var notes = new NoteStore(persistence);
            var note = notes.Add("Before");
            notes.SetText(note, "Latest text");
            notes.Delete(note); // The caller can hold an earlier version of a record.
            Assert.Empty(notes.Notes);
            Assert.True(notes.UndoDelete());
            notes.ForceSave();
            loaded = await persistence.LoadAsync();
            Assert.Equal("Latest text", Assert.Single(loaded.Notes).Text);
            await File.WriteAllTextAsync(persistence.GetStorePath(), "broken JSON");
            Assert.NotEmpty((await persistence.LoadAsync()).Notes);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task LoadAssignsNewIdsToDuplicateNotesWithoutDroppingThem()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Ledge-duplicate-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var duplicateId = "same-note-id";
            var json = $$"""
            {
              "version": 1,
              "notes": [
                { "id": "{{duplicateId}}", "text": "First" },
                { "id": "{{duplicateId}}", "text": "Second" }
              ],
              "settings": {}
            }
            """;
            await File.WriteAllTextAsync(Path.Combine(directory, "store.json"), json);

            var persistence = new FilePersistence(Settings.Default, directory);
            var loaded = await persistence.LoadAsync();

            Assert.Equal(2, loaded.Notes.Count);
            Assert.Equal(2, loaded.Notes.Select(note => note.Id).Distinct().Count());
            Assert.Equal(["First", "Second"], loaded.Notes.Select(note => note.Text));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
