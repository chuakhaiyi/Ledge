namespace Ledge.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

using Ledge.Core.Models;
using Ledge.App.Services;

public partial class DockTab : UserControl
{
    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(Note), typeof(DockTab), new PropertyMetadata(null, OnNoteChanged));
    public static readonly DependencyProperty PeekOnlyProperty = DependencyProperty.Register(
        nameof(PeekOnly), typeof(bool), typeof(DockTab), new PropertyMetadata(false, OnLayoutChanged));
    public static readonly DependencyProperty IsPeekedProperty = DependencyProperty.Register(
        nameof(IsPeeked), typeof(bool), typeof(DockTab), new PropertyMetadata(false, OnLayoutChanged));
    public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register(
        nameof(Edge), typeof(DockEdge), typeof(DockTab), new PropertyMetadata(DockEdge.Right, OnLayoutChanged));

    public Note? Note { get => (Note?)GetValue(NoteProperty); set => SetValue(NoteProperty, value); }
    public bool PeekOnly { get => (bool)GetValue(PeekOnlyProperty); set => SetValue(PeekOnlyProperty, value); }
    public bool IsPeeked { get => (bool)GetValue(IsPeekedProperty); set => SetValue(IsPeekedProperty, value); }
    public DockEdge Edge { get => (DockEdge)GetValue(EdgeProperty); set => SetValue(EdgeProperty, value); }

    private bool _hovered;

    public DockTab()
    {
        InitializeComponent();
        ContextMenu = AppMenus.NoteActions(() =>
        {
            if (Note != null) App.GetService<WindowManager>().DeleteNote(Note);
        });
        Margin = new Thickness(0, 5, 0, 5);
        Unloaded += (_, _) => SpringMotion.Stop(Slide);
    }

    private static void OnNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tab = (DockTab)d;
        if (e.NewValue is not Note note || tab.TabBorder == null) return;
        if (e.OldValue is not Note old || old.Color != note.Color)
        {
            tab.TabBorder.Background = ContinuousSurface.NoteFill(note.Color);
            var ink = new SolidColorBrush((Color)ColorConverter.ConvertFromString(note.Color.ToForegroundHex()));
            tab.HeaderWord.Foreground = tab.BodyText.Foreground = tab.PeekWord.Foreground = ink;
        }
        var lines = note.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        tab.HeaderWord.Text = lines.FirstOrDefault() ?? "Empty note";
        tab.PeekWord.Text = tab.HeaderWord.Text;
        tab.BodyText.Text = lines.Length > 1 ? string.Join(" ", lines.Skip(1)) : "Click to edit";
        tab.PinnedDot.Visibility = note.Pinned ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(tab, tab.HeaderWord.Text);
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DockTab)d).UpdateSlide();

    internal void SettleLayout()
    {
        SpringMotion.Stop(Slide);
        UpdateSlide(false);
    }

    private void UpdateSlide(bool animate = true)
    {
        if (Slide == null) return;
        var foldOnLeft = Edge == DockEdge.Right;
        PaperFold.HorizontalAlignment = foldOnLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        PaperFold.Margin = foldOnLeft ? new Thickness(-13, 0, 0, -11) : new Thickness(0, 0, -13, -11);
        PaperFold.RenderTransform = new ScaleTransform(foldOnLeft ? -1 : 1, 1);
        Margin = PeekOnly ? new Thickness(0) : new Thickness(0, 4, 0, 4);
        var hidden = PeekOnly && !_hovered;
        FullContent.Visibility = PeekWord.Visibility = Visibility.Visible;
        void Fade(UIElement content, bool visible)
        {
            content.IsHitTestVisible = visible;
            if (!IsLoaded || !animate) { content.BeginAnimation(OpacityProperty, null); content.Opacity = visible ? 1 : 0; }
            else content.BeginAnimation(OpacityProperty, new DoubleAnimation(visible ? 1 : 0, TimeSpan.FromMilliseconds(160))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        }
        Fade(FullContent, !hidden);
        Fade(PeekWord, hidden && IsPeeked);
        PeekWord.HorizontalAlignment = Edge == DockEdge.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        var x = hidden && Edge != DockEdge.Top ? (IsPeeked ? 100 : 196) * (Edge == DockEdge.Left ? -1 : 1) : 0;
        var y = hidden && Edge == DockEdge.Top ? (IsPeeked ? -20 : -56) : 0;
        if (!IsLoaded || !animate) { Slide.X = x; Slide.Y = y; }
        else
        {
            SpringMotion.To(Slide, TranslateTransform.XProperty, x, alwaysAnimate: true);
            SpringMotion.To(Slide, TranslateTransform.YProperty, y, alwaysAnimate: true);
        }
    }

    private void Tab_MouseEnter(object sender, MouseEventArgs e)
    {
        _hovered = true;
        if (VisualTreeHelper.GetParent(this) is ContentPresenter presenter) Panel.SetZIndex(presenter, 1);
        UpdateSlide();
    }

    private void Tab_MouseLeave(object sender, MouseEventArgs e)
    {
        _hovered = false;
        if (VisualTreeHelper.GetParent(this) is ContentPresenter presenter) Panel.SetZIndex(presenter, 0);
        UpdateSlide();
    }

    private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Note == null) return;
        App.GetService<WindowManager>().ShowNoteWindow(Note);
        e.Handled = true;
    }

    public void SetKeyboardHighlight(bool highlight)
    {
        TabBorder.SetResourceReference(Border.BorderBrushProperty, highlight ? "FocusRingBrush" : "BorderBrush");
        TabBorder.BorderThickness = new Thickness(highlight ? 2 : 1);
        if (highlight) BringIntoView();
    }
}
