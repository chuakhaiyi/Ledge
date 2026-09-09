namespace Ledge.Core.Services;

using System.Text.Json;
using Ledge.Core.Models;

public sealed class FilePersistence
{
    private readonly string _storePath;
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FilePersistence(Settings settings)
    {
        var baseDir = settings.PortableMode
            ? AppContext.BaseDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ledge");

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

        return new StoreData { Notes = notes, Settings = settings };
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

        await File.WriteAllTextAsync(tempPath, json);

        if (File.Exists(_storePath))
        {
            File.Copy(_storePath, _backupPath, true);
        }

        File.Move(tempPath, _storePath, true);
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var tempPath = _settingsPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _settingsPath, true);
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