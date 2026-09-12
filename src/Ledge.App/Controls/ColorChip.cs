namespace Ledge.App.Controls;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ledge.Core.Models;

public sealed class ColorChip : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Color { get; }
    public string ColorName { get; }
    public string ColorBrush { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public ColorChip(NoteColor color) : this(color.ToString()) { }

    public ColorChip(string color)
    {
        Color = color;
        ColorName = color.DisplayName();
        ColorBrush = color.ToHex();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
