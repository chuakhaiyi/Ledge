namespace Ledge.Core.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

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

public sealed record NoteColorChoice(string Name, string Hex)
{
    public string ForegroundHex => NoteColorExtensions.ForegroundFor(Hex);
}

public sealed class NoteColorValueConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString() ?? nameof(NoteColor.Moss);
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) && Enum.IsDefined(typeof(NoteColor), number))
            return ((NoteColor)number).ToString();
        return nameof(NoteColor.Moss);
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(NoteColorExtensions.IsPresetName(value)
            ? NoteColorExtensions.Presets.First(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase)).Name
            : nameof(NoteColor.Moss));
}

public static class NoteColorExtensions
{
    private static readonly Dictionary<string, string> HexMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Butter"] = "#F0DFAE", ["Clay"] = "#E7C3AE", ["Moss"] = "#C7D2B8", ["Sky"] = "#BFD2D6",
        ["Blush"] = "#E3C6C6", ["Slate"] = "#C7CBD1", ["Sand"] = "#DED3BD", ["Ink"] = "#B9BCC2",
        ["Honey"] = "#C6A84A", ["Rust"] = "#B56E4B", ["Fern"] = "#7D936C", ["Denim"] = "#6E94A1",
        ["Berry"] = "#B8747D", ["Charcoal"] = "#737983", ["Umber"] = "#9C825B", ["Onyx"] = "#747982"
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

    public static IReadOnlyList<NoteColorChoice> Presets { get; } =
    [
        new("Butter", HexMap["Butter"]), new("Clay", HexMap["Clay"]), new("Moss", HexMap["Moss"]), new("Sky", HexMap["Sky"]),
        new("Blush", HexMap["Blush"]), new("Slate", HexMap["Slate"]), new("Sand", HexMap["Sand"]), new("Ink", HexMap["Ink"]),
        new("Honey", HexMap["Honey"]), new("Rust", HexMap["Rust"]), new("Fern", HexMap["Fern"]), new("Denim", HexMap["Denim"]),
        new("Berry", HexMap["Berry"]), new("Charcoal", HexMap["Charcoal"]), new("Umber", HexMap["Umber"]), new("Onyx", HexMap["Onyx"])
    ];

    public static string ToHex(this NoteColor color) => ToHex(color.ToString());

    public static string ToHex(this string? color)
        => color != null && HexMap.TryGetValue(color, out var hex) ? hex : IsHex(color) ? color! : HexMap["Moss"];

    public static string ToForegroundHex(this NoteColor color) => ForegroundFor(color.ToHex());

    public static string ToForegroundHex(this string? color) => ForegroundFor(color.ToHex());

    public static string DisplayName(this string? color)
        => color != null && HexMap.ContainsKey(color) ? color : "Custom";

    public static string ForegroundFor(string hex)
    {
        var clean = hex.TrimStart('#');
        if (clean.Length != 6) return "#1B1D22";
        var red = Convert.ToInt32(clean[0..2], 16) / 255.0;
        var green = Convert.ToInt32(clean[2..4], 16) / 255.0;
        var blue = Convert.ToInt32(clean[4..6], 16) / 255.0;

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

    public static string Next(this string? color)
    {
        var index = Array.FindIndex(CycleOrder, c => string.Equals(c.ToString(), color, StringComparison.OrdinalIgnoreCase));
        return CycleOrder[(index < 0 ? 0 : index + 1) % CycleOrder.Length].ToString();
    }

    public static bool IsHex(string? value)
        => value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    public static bool IsPresetName(string? value)
        => value != null && Presets.Any(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));

    public static NoteColor FromName(string name)
    {
        return Enum.TryParse<NoteColor>(name, true, out var color) ? color : NoteColor.Moss;
    }

    public static IEnumerable<NoteColor> All => CycleOrder;
}
