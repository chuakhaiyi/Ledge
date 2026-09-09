namespace Ledge.App.Controls;

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ledge.Core.Models;
using Ledge.App.Services;

public partial class DockTab : UserControl
{
    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(nameof(Note), typeof(Note), typeof(DockTab),
            new PropertyMetadata(null, OnNoteChanged));

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public static readonly DependencyProperty IndexProperty =
        DependencyProperty.Register(nameof(Index), typeof(int), typeof(DockTab),
            new PropertyMetadata(0));

    public int Index
    {
        get => (int)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    public static readonly DependencyProperty IsPinnedProperty =
        DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(DockTab),
            new PropertyMetadata(false, OnPinnedChanged));

    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    public event Action<Note>? TabClicked;
    public event Action<Note>? TabHovered;

    public DockTab()
    {
        InitializeComponent();
    }

    private static void OnNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockTab tab && e.NewValue is Note note)
        {
            tab.UpdateVisuals(note);
        }
    }

    private static void OnPinnedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockTab tab)
        {
            tab.PinnedDot.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateVisuals(Note note)
    {
        try
        {
            TabBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(note.Color.ToHex()));
        }
        catch
        {
            TabBorder.Background = new SolidColorBrush(Color.FromRgb(199, 210, 184));
        }

        var lines = note.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        HeaderWord.Text = lines.Length > 0 ? lines[0].Trim() : "Empty note";
        BodyText.Text = lines.Length > 1 ? string.Join(" ", lines.Skip(1)).Trim() : (lines.Length > 0 ? lines[0].Trim() : "");
        TimestampText.Text = GetRelativeTime(note.ModifiedAt);
        PinnedDot.Visibility = note.Pinned ? Visibility.Visible : Visibility.Collapsed;
        ToolTip = string.IsNullOrWhiteSpace(note.Text) ? "Empty note" : note.Text;
    }

    private static string GetRelativeTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d";
        return dt.ToString("MMM d");
    }

    public void SetState(DockState state, bool animate = true)
    {
        // For compatibility with dock window
    }

    private void TabBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Note != null)
        {
            TabClicked?.Invoke(Note);
            var windowManager = App.GetService<WindowManager>();
            windowManager.ShowNoteWindow(Note);
            e.Handled = true;
        }
    }

    private void TabBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        TabShadow.Opacity = 0.35;
        TabShadow.BlurRadius = 12;
        if (Note != null)
        {
            TabHovered?.Invoke(Note);
        }
    }

    private void TabBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        TabShadow.Opacity = 0.15;
        TabShadow.BlurRadius = 8;
    }

    public void SetKeyboardHighlight(bool highlight)
    {
        if (highlight)
        {
            TabBorder.BorderBrush = (Brush)FindResource("AccentBrush");
            TabBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            TabBorder.BorderBrush = (Brush)FindResource("BorderBrush");
            TabBorder.BorderThickness = new Thickness(1);
        }
    }
}

public enum DockState
{
    Idle,
    Peeked,
    Expanded
}