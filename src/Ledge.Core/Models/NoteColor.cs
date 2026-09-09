namespace Ledge.Core.Models;

public enum NoteColor
{
    Butter = 0,
    Clay = 1,
    Moss = 2,
    Sky = 3,
    Blush = 4,
    Slate = 5,
    Sand = 6,
    Ink = 7
}

public static class NoteColorExtensions
{
    private static readonly Dictionary<NoteColor, string> HexMap = new()
    {
        [NoteColor.Butter] = "#F0DFAE",
        [NoteColor.Clay] = "#E7C3AE",
        [NoteColor.Moss] = "#C7D2B8",
        [NoteColor.Sky] = "#BFD2D6",
        [NoteColor.Blush] = "#E3C6C6",
        [NoteColor.Slate] = "#C7CBD1",
        [NoteColor.Sand] = "#DED3BD",
        [NoteColor.Ink] = "#B9BCC2"
    };

    private static readonly NoteColor[] CycleOrder =
    [
        NoteColor.Butter,
        NoteColor.Clay,
        NoteColor.Moss,
        NoteColor.Sky,
        NoteColor.Blush,
        NoteColor.Slate,
        NoteColor.Sand,
        NoteColor.Ink
    ];

    public static string ToHex(this NoteColor color) => HexMap[color];

    public static NoteColor Next(this NoteColor color)
    {
        var index = Array.IndexOf(CycleOrder, color);
        return CycleOrder[(index + 1) % CycleOrder.Length];
    }

    public static NoteColor FromName(string name)
    {
        return Enum.TryParse<NoteColor>(name, true, out var color) ? color : NoteColor.Moss;
    }

    public static IEnumerable<NoteColor> All => CycleOrder;
}