namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ledge.Core.Models;

public partial class NoteEditor : UserControl
{
    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(nameof(Note), typeof(Note), typeof(NoteEditor),
            new PropertyMetadata(null, OnNoteChanged));

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public event Action<NoteColor>? ColorChanged;
    public event Action? DeleteRequested;
    public event Action? PinToggled;
    public event Action? CloseRequested;
    public event Action? NewNoteRequested;

    private readonly ColorChip[] _colorChips;

    public NoteEditor()
    {
        _colorChips =
        [
            new ColorChip(NoteColor.Butter),
            new ColorChip(NoteColor.Clay),
            new ColorChip(NoteColor.Moss),
            new ColorChip(NoteColor.Sky),
            new ColorChip(NoteColor.Blush),
            new ColorChip(NoteColor.Slate),
            new ColorChip(NoteColor.Sand),
            new ColorChip(NoteColor.Ink)
        ];

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TextBox.Focus();
        TextBox.CaretIndex = TextBox.Text.Length;
    }

    private static void OnNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteEditor editor && e.NewValue is Note note)
        {
            editor.TextBox.Text = note.Text;
            editor.UpdateColorIndicator(note.Color);
            editor.UpdateColorSelection(note.Color);
            editor.UpdatePinIndicator(note.Pinned);
        }
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (Note != null)
        {
            Note.Text = TextBox.Text;
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        ColorPopup.ItemsSource = _colorChips;
        ColorPopup.IsOpen = !ColorPopup.IsOpen;
    }

    private void ColorChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ColorChip chip)
        {
            ColorChanged?.Invoke(chip.Color);
            ColorPopup.IsOpen = false;
        }
    }

    private void NewNoteButton_Click(object sender, RoutedEventArgs e)
    {
        NewNoteRequested?.Invoke();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        PinToggled?.Invoke();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke();
    }

    public void UpdateColorIndicator(NoteColor color)
    {
        ColorIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color.ToHex()));
    }

    private void UpdateColorSelection(NoteColor color)
    {
        foreach (var chip in _colorChips)
        {
            chip.IsSelected = chip.Color == color;
        }
    }

    public void UpdatePinIndicator(bool pinned)
    {
        PinDot.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
        PinText.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
        PinIcon.Fill = pinned ? (Brush)FindResource("AccentBrush") : Brushes.Transparent;
        PinButton.ToolTip = pinned ? "Unpin note from dock" : "Pin note to dock";
    }

    public void FocusEditor()
    {
        TextBox.Focus();
        TextBox.CaretIndex = TextBox.Text.Length;
    }
}