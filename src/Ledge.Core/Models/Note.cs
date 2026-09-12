namespace Ledge.Core.Models;

using System.Text.Json.Serialization;

public record NotePosition(double X, double Y, double Width, double Height)
{
    public static NotePosition Default => new(0, 0, 280, 320);
    public static NotePosition Empty => new(0, 0, 0, 0);
}

public record Note
{
    public required string Id { get; init; }
    public string Text { get; set; } = string.Empty;
    // Named presets are persisted as strings for compatibility with legacy stores.
    [JsonConverter(typeof(NoteColorValueConverter))]
    public string Color { get; set; } = nameof(NoteColor.Moss);
    public bool Pinned { get; set; }
    public bool Archived { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public NotePosition? Position { get; set; }

    public Note WithText(string text) => this with { Text = text, ModifiedAt = DateTime.UtcNow };
    public Note WithColor(string color) => this with { Color = color, ModifiedAt = DateTime.UtcNow };
    public Note WithPinned(bool pinned) => this with { Pinned = pinned, ModifiedAt = DateTime.UtcNow };
    public Note WithArchived(bool archived) => this with { Archived = archived, ModifiedAt = DateTime.UtcNow };
    public Note WithPosition(NotePosition position) => this with { Position = position, ModifiedAt = DateTime.UtcNow };

    public static Note Create(string text = "", string color = nameof(NoteColor.Moss)) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Text = text,
        Color = NoteColorExtensions.IsPresetName(color) ? NoteColorExtensions.Presets.First(p => p.Name.Equals(color, StringComparison.OrdinalIgnoreCase)).Name : nameof(NoteColor.Moss),
        CreatedAt = DateTime.UtcNow,
        ModifiedAt = DateTime.UtcNow
    };
}
