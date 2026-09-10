namespace Ledge.Core.Services;

using System.Text.Json;
using Ledge.Core.Models;

public sealed class FilePersistence
{
    private readonly string _storePath;
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _notesWriteLock = new(1, 1);
    private readonly SemaphoreSlim _settingsWriteLock = new(1, 1);

    public FilePersistence(Settings settings, string? dataDirectory = null)
    {
        var baseDir = dataDirectory ?? (settings.PortableMode
            ? AppContext.BaseDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ledge"));

        Directory.CreateDirectory(baseDir);

        _storePath = Path.Combine(baseDir, "store.json");
        _settingsPath = Path.Combine(baseDir, "settings.json");
        _backupPath = Path.Combine(baseDir, "store.json.bak");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<StoreData> LoadAsync()
    {
        var notes = new List<Note>();
        var settings = Settings.Default;

        if (File.Exists(_storePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_storePath);
                var data = JsonSerializer.Deserialize<StoreData>(json, _jsonOptions);
                if (data?.Notes != null)
                {
                    notes.AddRange(data.Notes);
                }
            }
            catch
            {
                if (File.Exists(_backupPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(_backupPath);
                        var data = JsonSerializer.Deserialize<StoreData>(json, _jsonOptions);
                        if (data?.Notes != null)
                        {
                            notes.AddRange(data.Notes);
                        }
                    }
                    catch { }
                }
            }
        }

        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var loaded = JsonSerializer.Deserialize<Settings>(json, _jsonOptions);
                if (loaded != null) settings = loaded;
            }
            catch { }
        }

        return new StoreData { Notes = NormalizeNoteIds(notes), Settings = settings };
    }

    private static List<Note> NormalizeNoteIds(IEnumerable<Note> notes)
    {
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<Note>();

        foreach (var note in notes)
        {
            var id = note.Id;
            if (string.IsNullOrWhiteSpace(id) || !usedIds.Add(id))
            {
                do id = Guid.NewGuid().ToString("N");
                while (!usedIds.Add(id));

                normalized.Add(note with { Id = id });
            }
            else
            {
                normalized.Add(note);
            }
        }

        return normalized;
    }

    public async Task SaveAsync(IEnumerable<Note> notes)
    {
        var data = new StoreData
        {
            Version = 1,
            Notes = notes.ToList()
        };

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var tempPath = _storePath + ".tmp";

        await _notesWriteLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            if (File.Exists(_storePath))
            {
                File.Copy(_storePath, _backupPath, true);
            }
            File.Move(tempPath, _storePath, true);
        }
        finally
        {
            _notesWriteLock.Release();
        }
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var tempPath = _settingsPath + ".tmp";
        await _settingsWriteLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, _settingsPath, true);
        }
        finally
        {
            _settingsWriteLock.Release();
        }
    }

    public string GetStorePath() => _storePath;
    public string GetSettingsPath() => _settingsPath;
}

public sealed class StoreData
{
    public int Version { get; set; } = 1;
    public List<Note> Notes { get; set; } = [];
    public Settings Settings { get; set; } = Settings.Default;
}
