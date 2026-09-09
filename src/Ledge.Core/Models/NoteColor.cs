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

    public static string ToForegroundHex(this NoteColor color)
    {
        var hex = color.ToHex().TrimStart('#');
        var red = Convert.ToInt32(hex[0..2], 16) / 255.0;
        var green = Convert.ToInt32(hex[2..4], 16) / 255.0;
        var blue = Convert.ToInt32(hex[4..6], 16) / 255.0;

        static double Linearize(double channel)
            => channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);

        var luminance = (0.2126 * Linearize(red))
            + (0.7152 * Linearize(green))
            + (0.0722 * Linearize(blue));
        var darkContrast = (luminance + 0.05) / 0.05;
        var lightContrast = 1.05 / (luminance + 0.05);

        return darkContrast >= lightContrast ? "#1B1D22" : "#EAE6DC";
    }

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