namespace Ledge.Core.Models;

public enum DockEdge
{
    Left,
    Right,
    Top
}

public enum AppTheme
{
    System,
    Light,
    Dark
}

// Virtual key codes (subset of Win32 VK_* constants)
public enum VirtualKey : int
{
    // Modifiers
    Control = 0x11,
    Menu = 0x12,      // Alt
    Shift = 0x10,
    LWin = 0x5B,
    RWin = 0x5C,

    // Letters
    A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46, G = 0x47,
    H = 0x48, I = 0x49, J = 0x4A, K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E,
    O = 0x4F, P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54, U = 0x55,
    V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,

    // Function keys
    F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75,
    F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,

    // Special
    Escape = 0x1B,
    Space = 0x20,
    Enter = 0x0D,
    Tab = 0x09,
    Back = 0x08,
    Delete = 0x2E,
    Insert = 0x2D,
    Home = 0x24,
    End = 0x23,
    PageUp = 0x21,
    PageDown = 0x22,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    OemPeriod = 0xBE,  // .
    OemComma = 0xBC,   // ,
    OemSemicolon = 0xBA, // ;
    OemPlus = 0xBB,    // =
    OemMinus = 0xBD,   // -
    OemTilde = 0xC0,   // `
    OemOpenBrackets = 0xDB,  // [
    OemCloseBrackets = 0xDD, // ]
    OemPipe = 0xDC,    // \
    OemQuestion = 0xBF, // /
    OemQuotes = 0xDE,  // '
}

public record HotkeyBinding(int Modifiers, int Key)
{
    public static readonly HotkeyBinding Empty = new(0, 0);

    public string ToDisplayString()
    {
        if (Key == 0) return "Not set";
        var parts = new List<string>();
        if ((Modifiers & 1) != 0) parts.Add("Ctrl");
        if ((Modifiers & 2) != 0) parts.Add("Alt");
        if ((Modifiers & 4) != 0) parts.Add("Shift");
        if ((Modifiers & 8) != 0) parts.Add("Win");
        parts.Add(KeyToString(Key));
        return string.Join("+", parts);
    }

    public bool IsEmpty => Key == 0;

    private static string KeyToString(int key)
    {
        return key switch
        {
            0x41 => "A", 0x42 => "B", 0x43 => "C", 0x44 => "D", 0x45 => "E", 0x46 => "F", 0x47 => "G",
            0x48 => "H", 0x49 => "I", 0x4A => "J", 0x4B => "K", 0x4C => "L", 0x4D => "M", 0x4E => "N",
            0x4F => "O", 0x50 => "P", 0x51 => "Q", 0x52 => "R", 0x53 => "S", 0x54 => "T", 0x55 => "U",
            0x56 => "V", 0x57 => "W", 0x58 => "X", 0x59 => "Y", 0x5A => "Z",
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4", 0x74 => "F5", 0x75 => "F6",
            0x76 => "F7", 0x77 => "F8", 0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            0x1B => "Esc", 0x20 => "Space", 0x0D => "Enter", 0x09 => "Tab", 0x08 => "Backspace",
            0x2E => "Del", 0x2D => "Ins", 0x24 => "Home", 0x23 => "End", 0x21 => "PgUp", 0x22 => "PgDn",
            0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
            0xBE => ".", 0xBC => ",", 0xBA => ";", 0xBB => "=", 0xBD => "-",
            0xC0 => "`", 0xDB => "[", 0xDD => "]", 0xDC => "\\", 0xBF => "/", 0xDE => "'",
            _ => $"VK_{key:X}"
        };
    }
}

public record Settings
{
    public DockEdge DockEdge { get; set; } = DockEdge.Right;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool StartWithWindows { get; set; } = false;
    public bool PortableMode { get; set; } = false;
    public bool ShowDockOnHover { get; set; } = true;
    public HotkeyBinding NewNoteHotkey { get; set; } = new(1 | 2, (int)VirtualKey.N); // Ctrl+Alt+N
    public HotkeyBinding LibraryHotkey { get; set; } = new(1 | 2, (int)VirtualKey.A);  // Ctrl+Alt+A
    public HotkeyBinding ArchiveHotkey { get; set; } = new(1 | 2, (int)VirtualKey.L);  // Ctrl+Alt+L
    public HotkeyBinding FocusDockHotkey { get; set; } = new(1 | 2, (int)VirtualKey.D); // Ctrl+Alt+D

    public static Settings Default => new();
}