namespace Ledge.App.Controls;

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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

    public static readonly DependencyProperty PeekOnlyProperty =
        DependencyProperty.Register(nameof(PeekOnly), typeof(bool), typeof(DockTab),
            new PropertyMetadata(false, OnPeekOnlyChanged));

    public bool PeekOnly
    {
        get => (bool)GetValue(PeekOnlyProperty);
        set => SetValue(PeekOnlyProperty, value);
    }

    public static readonly DependencyProperty IsPeekedProperty =
        DependencyProperty.Register(nameof(IsPeeked), typeof(bool), typeof(DockTab),
            new PropertyMetadata(false, OnIsPeekedChanged));

    public bool IsPeeked
    {
        get => (bool)GetValue(IsPeekedProperty);
        set => SetValue(IsPeekedProperty, value);
    }

    public event Action<Note>? TabClicked;
    public event Action<Note>? TabHovered;

    public DockTab()
    {
        InitializeComponent();

        TabBorder.RenderTransform = CreateHoverTransform();
        TabBorder.RenderTransformOrigin = new Point(0.5, 0.5);
        PeekBorder.RenderTransform = CreateHoverTransform();
        PeekBorder.RenderTransformOrigin = new Point(0.5, 0.5);
    }

    private static TransformGroup CreateHoverTransform()
    {
        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(1, 1));
        transform.Children.Add(new TranslateTransform());
        transform.Children.Add(new RotateTransform());
        return transform;
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

    private static void OnPeekOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockTab tab)
        {
            tab.ApplyPeekLayout();
        }
    }

    private static void OnIsPeekedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockTab tab)
        {
            tab.ApplyPeekLayout();
        }
    }

    private void ApplyPeekLayout()
    {
        if (!PeekOnly)
        {
            Width = 220;
            MinHeight = 56;
            Margin = new Thickness(0, 3, 0, 3);
            TabBorder.Visibility = Visibility.Visible;
            PeekBorder.Visibility = Visibility.Collapsed;
            return;
        }

        Width = 216;
        Height = 58;
        MinHeight = 58;
        Margin = new Thickness(0, -10, 0, 0);
        TabBorder.Visibility = Visibility.Collapsed;
        PeekBorder.Visibility = Visibility.Visible;
        PeekLabel.Visibility = IsPeeked ? Visibility.Visible : Visibility.Collapsed;
        SetStackTransform(IsPeeked ? 112 : 198, false);
    }

    private void UpdateVisuals(Note note)
    {
        try
        {
            TabBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(note.Color.ToHex()));
            var foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(note.Color.ToForegroundHex()));
            HeaderWord.Foreground = foreground;
            BodyText.Foreground = foreground;
            TimestampText.Foreground = foreground;
            PeekLabel.Foreground = foreground;
        }
        catch
        {
            TabBorder.Background = (Brush)FindResource("MossNoteBrush");
        }

        var lines = note.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        HeaderWord.Text = lines.Length > 0 ? lines[0].Trim() : "Empty note";
        BodyText.Text = lines.Length > 1 ? string.Join(" ", lines.Skip(1)).Trim() : (lines.Length > 0 ? lines[0].Trim() : "");
        TimestampText.Text = GetRelativeTime(note.ModifiedAt);
        PeekLabel.Text = HeaderWord.Text;
        PeekBorder.Background = TabBorder.Background;
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
        Panel.SetZIndex(this, 100);
        if (PeekOnly)
        {
            TabBorder.Visibility = Visibility.Visible;
            PeekBorder.Visibility = Visibility.Collapsed;
            PeekLabel.Visibility = Visibility.Collapsed;
        }

        AnimateHover(true);
        if (Note != null)
        {
            TabHovered?.Invoke(Note);
        }
    }

    private void TabBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        AnimateHover(false);
        if (PeekOnly)
        {
            ApplyPeekLayout();
        }
        Panel.SetZIndex(this, 0);
    }

    private void AnimateHover(bool isHovered)
    {
        var duration = SystemParameters.ClientAreaAnimation
            ? new Duration(TimeSpan.FromMilliseconds(180))
            : new Duration(TimeSpan.Zero);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var targetScale = isHovered ? 1.0 : 1.0;
        var targetOffset = isHovered ? 0.0 : 0.0;
        var targetX = PeekOnly ? (isHovered ? 0.0 : IsPeeked ? 112.0 : 198.0) : 0.0;
        var targetY = PeekOnly ? Index * 14.0 : 0.0;
        var targetRotation = PeekOnly && !isHovered ? Index * -2.2 : 0.0;

        foreach (var border in new[] { TabBorder, PeekBorder })
        {
            var group = (TransformGroup)border.RenderTransform;
            var scale = (ScaleTransform)group.Children[0];
            var translate = (TranslateTransform)group.Children[1];
            var rotate = (RotateTransform)group.Children[2];
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = easing });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, duration) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(targetX, duration) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY + targetOffset, duration) { EasingFunction = easing });
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(targetRotation, duration) { EasingFunction = easing });
        }

        TabShadow.BeginAnimation(DropShadowEffect.OpacityProperty,
            new DoubleAnimation(isHovered ? 0.28 : 0.15, duration) { EasingFunction = easing });
        TabShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
            new DoubleAnimation(isHovered ? 16 : 8, duration) { EasingFunction = easing });
    }

    private void SetStackTransform(double x, bool animate)
    {
        var duration = animate && SystemParameters.ClientAreaAnimation
            ? new Duration(TimeSpan.FromMilliseconds(260))
            : new Duration(TimeSpan.Zero);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        foreach (var border in new[] { TabBorder, PeekBorder })
        {
            var group = (TransformGroup)border.RenderTransform;
            var translate = (TranslateTransform)group.Children[1];
            var rotate = (RotateTransform)group.Children[2];
            translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, duration) { EasingFunction = easing });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(Index * 14, duration) { EasingFunction = easing });
            rotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(x == 0 ? 0 : Index * -2.2, duration) { EasingFunction = easing });
        }
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
