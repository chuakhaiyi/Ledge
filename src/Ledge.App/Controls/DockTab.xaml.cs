namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Media;
using Ledge.Core.Models;

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
            new PropertyMetadata(0, OnIndexChanged));

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

    private readonly Storyboard _peekStoryboard;
    private readonly Storyboard _collapseStoryboard;
    private DockState _state = DockState.Idle;

    public DockTab()
    {
        InitializeComponent();
        _peekStoryboard = (Storyboard)Resources["PeekAnimation"];
        _collapseStoryboard = (Storyboard)Resources["CollapseAnimation"];
    }

    private static void OnNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockTab tab && e.NewValue is Note note)
        {
            tab.UpdateVisuals(note);
        }
    }

    private static void OnIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockTab tab)
        {
            tab.ApplyTransform();
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
        TabBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(note.Color.ToHex()));
        PeekWord.Text = GetFirstWord(note.Text);
        FullText.Text = note.Text;
        FullMeta.Text = $"{note.Color} · {GetRelativeTime(note.ModifiedAt)}";
        PinnedDot.Visibility = note.Pinned ? Visibility.Visible : Visibility.Collapsed;
        ApplyTransform();
    }

    private static string GetFirstWord(string text)
    {
        var trimmed = text.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;
    }

    private static string GetRelativeTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dt.ToString("MMM d");
    }

    private void ApplyTransform()
    {
        var translate = (TranslateTransform)((TransformGroup)RenderTransform).Children[0];
        var rotate = (RotateTransform)((TransformGroup)RenderTransform).Children[1];

        if (_state == DockState.Idle)
        {
            translate.X = 198;
            translate.Y = Index * 14;
            rotate.Angle = Index * -2.2;
        }
        else if (_state == DockState.Peeked)
        {
            translate.X = 112;
            translate.Y = Index * 16;
            rotate.Angle = Index * -1.4;
        }
    }

    public void SetState(DockState state, bool animate = true)
    {
        _state = state;

        if (animate)
        {
            if (state == DockState.Peeked || state == DockState.Expanded)
            {
                _peekStoryboard.Begin(this);
            }
            else
            {
                _collapseStoryboard.Begin(this);
            }
        }

        ApplyTransform();
        UpdateContentVisibility();
    }

    private void UpdateContentVisibility()
    {
        if (_state == DockState.Idle)
        {
            PeekWord.Opacity = 0;
            FullContent.Opacity = 0;
        }
        else if (_state == DockState.Peeked)
        {
            PeekWord.Opacity = 1;
            FullContent.Opacity = 0;
        }
        else if (_state == DockState.Expanded)
        {
            PeekWord.Opacity = 0;
            FullContent.Opacity = 1;
        }
    }

    private void TabBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Note != null)
        {
            TabClicked?.Invoke(Note);
        }
    }

    private void TabBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        if (Note != null && _state != DockState.Expanded)
        {
            TabHovered?.Invoke(Note);
        }
    }

    private void TabBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        // Handled by parent hotzone
    }

    public void SetKeyboardHighlight(bool highlight)
    {
        if (highlight)
        {
            TabBorder.BorderBrush = (Brush)FindResource("AccentBrush");
            TabBorder.BorderThickness = new Thickness(0, 0, 2, 0);
        }
        else
        {
            TabBorder.BorderBrush = null;
            TabBorder.BorderThickness = new Thickness(0);
        }
    }
}

public enum DockState
{
    Idle,
    Peeked,
    Expanded
}