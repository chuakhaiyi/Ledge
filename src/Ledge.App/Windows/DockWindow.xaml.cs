namespace Ledge.App.Windows;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.Windows.Threading;
using Ledge.Native.Interop;
using Ledge.Core.Services;
using Ledge.Core.Models;
using Ledge.App.Controls;
using Ledge.App.Services;

public partial class DockWindow : Window
{
    private readonly NoteStore _noteStore;
    private readonly SettingsStore _settingsStore;
    private bool _isExpanded = false;
    private bool _isPeeking = false;
    private bool _isKeyboardFocused = false;
    private int _keyboardIndex = -1;
    private readonly DispatcherTimer _hoverDelayTimer;
    private readonly DispatcherTimer _hoverExitTimer;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Dictionary<string, NoteRow> _rows = new();
    private readonly ObservableCollection<NoteRow> _dockItems = new();
    private Dictionary<string, Note> _observedNotes = new();
    private bool _changingEdge;
    private bool _closed;
    private Window? _travelWindow;
    private bool _bindingsInitialized;

    public static readonly DependencyProperty PinnedNotesProperty =
        DependencyProperty.Register(nameof(PinnedNotes), typeof(System.Collections.IEnumerable), typeof(DockWindow),
            new PropertyMetadata(null));

    public System.Collections.IEnumerable PinnedNotes
    {
        get => (System.Collections.IEnumerable)GetValue(PinnedNotesProperty);
        set => SetValue(PinnedNotesProperty, value);
    }

    public static readonly DependencyProperty VisibleUnpinnedNotesProperty =
        DependencyProperty.Register(nameof(VisibleUnpinnedNotes), typeof(System.Collections.IEnumerable), typeof(DockWindow),
            new PropertyMetadata(null));

    public System.Collections.IEnumerable VisibleUnpinnedNotes
    {
        get => (System.Collections.IEnumerable)GetValue(VisibleUnpinnedNotesProperty);
        set => SetValue(VisibleUnpinnedNotesProperty, value);
    }

    public static readonly DependencyProperty CollapsedNotesProperty =
        DependencyProperty.Register(nameof(CollapsedNotes), typeof(System.Collections.IEnumerable), typeof(DockWindow),
            new PropertyMetadata(null));

    public System.Collections.IEnumerable CollapsedNotes
    {
        get => (System.Collections.IEnumerable)GetValue(CollapsedNotesProperty);
        set => SetValue(CollapsedNotesProperty, value);
    }

    public static readonly DependencyProperty IsPeekingProperty =
        DependencyProperty.Register(nameof(IsPeeking), typeof(bool), typeof(DockWindow),
            new PropertyMetadata(false));

    public bool IsPeeking
    {
        get => (bool)GetValue(IsPeekingProperty);
        set => SetValue(IsPeekingProperty, value);
    }

    public static readonly DependencyProperty CurrentDockEdgeProperty =
        DependencyProperty.Register(nameof(CurrentDockEdge), typeof(DockEdge), typeof(DockWindow),
            new PropertyMetadata(DockEdge.Right));

    public DockEdge CurrentDockEdge
    {
        get => (DockEdge)GetValue(CurrentDockEdgeProperty);
        private set => SetValue(CurrentDockEdgeProperty, value);
    }

    public DockWindow(NoteStore noteStore, SettingsStore settingsStore)
    {
        _noteStore = noteStore;
        _settingsStore = settingsStore;
        InitializeComponent();
        CollapsedNotes = _dockItems;
        PinnedNotes = Array.Empty<NoteRow>();
        VisibleUnpinnedNotes = _dockItems;
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); UpdateBindings(); };

        _hoverExitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _hoverExitTimer.Tick += (_, _) =>
        {
            _hoverExitTimer.Stop();
            if (!IsMouseOver && !IsMouseCaptureWithin && !_isKeyboardFocused && CollapsedView.ContextMenu?.IsOpen != true) Collapse();
        };
        _hoverDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _hoverDelayTimer.Tick += (_, _) =>
        {
            _hoverDelayTimer.Stop();
            if (IsMouseOver && !_isExpanded && !_isPeeking)
            {
                Peek();
            }
        };

        _noteStore.PropertyChanged += OnNotesChanged;
        _settingsStore.PropertyChanged += OnSettingsChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            _travelWindow?.Close();
            _previewTimer.Stop();
            _hoverDelayTimer.Stop();
            _hoverExitTimer.Stop();
            SpringMotion.Stop(PanelSlide);
            _noteStore.PropertyChanged -= OnNotesChanged;
            _settingsStore.PropertyChanged -= OnSettingsChanged;
        };
    }

    private void OnNotesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NoteStore.Notes)) return;
        Dispatcher.Invoke(() =>
        {
            var notes = _noteStore.Notes;
            var immediate = notes.Count != _observedNotes.Count || notes.Any(n => !_observedNotes.TryGetValue(n.Id, out var old) || old.Color != n.Color || old.Pinned != n.Pinned || old.Archived != n.Archived);
            var textChanged = notes.Any(n => !_observedNotes.TryGetValue(n.Id, out var old) || old.Text != n.Text);
            _observedNotes = notes.ToDictionary(n => n.Id);
            if (immediate) { _previewTimer.Stop(); UpdateBindings(); }
            else if (textChanged) { _previewTimer.Stop(); _previewTimer.Start(); }
        });
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsStore.DockEdge)) Dispatcher.Invoke(ChangeEdge);
        else if (e.PropertyName == nameof(SettingsStore.ShowDockOnHover))
            Dispatcher.Invoke(() => { Collapse(); UpdateBindings(); Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PositionCollapsedItems)); });
    }

    private IEnumerable<DockTab> CollapsedCards()
    {
        for (var i = 0; i < CollapsedTabs.Items.Count; i++)
            if (CollapsedTabs.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter presenter && VisualTreeHelper.GetChildrenCount(presenter) > 0 && VisualTreeHelper.GetChild(presenter, 0) is DockTab tab)
                yield return tab;
    }

    private Dictionary<string, (BitmapSource Image, Rect Bounds)> SnapshotDock()
    {
        UpdateLayout();
        var snapshots = new Dictionary<string, (BitmapSource, Rect)>();
        void Capture(string key, FrameworkElement surface)
        {
            var region = surface.TransformToAncestor(this).TransformBounds(new Rect(surface.RenderSize));
            var dpi = VisualTreeHelper.GetDpi(this);
            var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(region.Width * dpi.DpiScaleX)), Math.Max(1, (int)Math.Ceiling(region.Height * dpi.DpiScaleY)), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
                context.DrawRectangle(new VisualBrush(surface) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(surface.RenderSize) }, null, new Rect(surface.RenderSize));
            bitmap.Render(drawing);
            bitmap.Freeze();
            snapshots.Add(key, (bitmap, new Rect(Left + region.X, Top + region.Y, region.Width, region.Height)));
        }
        if (_isExpanded) Capture("panel", ExpandedView);
        else
        {
            foreach (var tab in CollapsedCards())
                Capture(tab.Note!.Id, (FrameworkElement)tab.FindName("TabBorder"));
            if (snapshots.Count == 0) Capture("empty", CollapsedEmptyMark);
        }
        return snapshots;
    }

    private async void ChangeEdge()
    {
        if (_changingEdge || _closed) return;
        _changingEdge = true;
        try
        {
            while (!_closed && CurrentDockEdge != _settingsStore.DockEdge)
            {
                if (!IsLoaded || !IsVisible)
                {
                    PlaceDock(); UpdateLayoutForEdge(); UpdateLayout(); PositionCollapsedItems();
                    foreach (var tab in CollapsedCards()) tab.SettleLayout();
                    break;
                }
                _hoverDelayTimer.Stop(); _hoverExitTimer.Stop();
                var before = SnapshotDock();
                var screen = SystemParameters.WorkArea;
                var canvas = new Canvas();
                foreach (var card in before.Values)
                {
                    var stage = new Grid { Width = card.Bounds.Width, Height = card.Bounds.Height, RenderTransform = new TranslateTransform(card.Bounds.Left - screen.Left, card.Bounds.Top - screen.Top) };
                    stage.Children.Add(new Image { Source = card.Image, Width = card.Bounds.Width, Height = card.Bounds.Height, Stretch = Stretch.Fill });
                    canvas.Children.Add(stage);
                }
                var travel = new Window { Left = screen.Left, Top = screen.Top, Width = screen.Width, Height = screen.Height, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowActivated = false, ShowInTaskbar = false, Topmost = true, IsHitTestVisible = false, Content = canvas };
                _travelWindow = travel;
                travel.SourceInitialized += (_, _) =>
                {
                    var hwnd = new WindowInteropHelper(travel).Handle;
                    User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE) | User32.WS_EX_TOOLWINDOW | User32.WS_EX_TRANSPARENT | User32.WS_EX_NOACTIVATE);
                };
                travel.Show();
                Opacity = 0;
                PlaceDock(); UpdateLayoutForEdge(); UpdateLayout(); PositionCollapsedItems();
                foreach (var tab in CollapsedCards()) tab.SettleLayout();
                UpdateLayout();
                var after = SnapshotDock();
                // One clock owns travel and crossfade. No spring overshoot, image stretching,
                // or timer racing the final compositor frame at the destination edge.
                var duration = TimeSpan.FromMilliseconds(720);
                var storyboard = new Storyboard();
                void Animate(FrameworkElement target, string property, double from, double to)
                {
                    var animation = new DoubleAnimation(from, to, property == "Opacity" ? TimeSpan.FromMilliseconds(120) : duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                    Storyboard.SetTarget(animation, target);
                    Storyboard.SetTargetProperty(animation, new PropertyPath(property));
                    storyboard.Children.Add(animation);
                }
                var index = 0;
                foreach (var (key, oldCard) in before)
                {
                    var card = after[key];
                    var stage = (Grid)canvas.Children[index++];
                    var slide = (TranslateTransform)stage.RenderTransform;
                    var oldImage = (Image)stage.Children[0];
                    var newImage = new Image { Source = card.Image, Width = card.Bounds.Width, Height = card.Bounds.Height, Opacity = 0, Stretch = Stretch.Fill };
                    stage.Children.Add(newImage);
                    stage.Width = Math.Max(oldCard.Bounds.Width, card.Bounds.Width);
                    stage.Height = Math.Max(oldCard.Bounds.Height, card.Bounds.Height);
                    slide.X -= (stage.Width - oldCard.Bounds.Width) / 2;
                    slide.Y -= (stage.Height - oldCard.Bounds.Height) / 2;
                    Animate(stage, "RenderTransform.X", slide.X, card.Bounds.Left - screen.Left - (stage.Width - card.Bounds.Width) / 2);
                    Animate(stage, "RenderTransform.Y", slide.Y, card.Bounds.Top - screen.Top - (stage.Height - card.Bounds.Height) / 2);
                    Animate(oldImage, "Opacity", 1, 0);
                    Animate(newImage, "Opacity", 0, 1);
                }
                var completed = new TaskCompletionSource();
                storyboard.Completed += (_, _) => completed.TrySetResult();
                travel.Closed += (_, _) => completed.TrySetResult();
                travel.UpdateLayout();
                storyboard.Begin(travel);
                await completed.Task;
                if (_closed) return;
                Opacity = 1;
                travel.Close(); _travelWindow = null;
            }
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Dock transition failed: {0}", exception);
            if (!_closed) { PlaceDock(); UpdateLayoutForEdge(); PositionCollapsedItems(); }
        }
        finally
        {
            _travelWindow?.Close(); _travelWindow = null;
            if (!_closed) Opacity = 1;
            _changingEdge = false;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowStyle(hwnd);
        UpdatePosition();
        UpdateLayoutForEdge();
        Collapse();
        UpdateBindings();


    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        // Interop hooks if needed
    }

    private void SetWindowStyle(nint hwnd)
    {
        var exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        exStyle |= User32.WS_EX_TOOLWINDOW | User32.WS_EX_TOPMOST;
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle);

        var margins = new DwmApi.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmApi.DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    public void UpdatePosition()
    {
        if (!_changingEdge) PlaceDock();
    }

    private void PlaceDock()
    {
        var screen = SystemParameters.WorkArea;
        var edge = _settingsStore.DockEdge;

        if (edge == DockEdge.Top)
        {
            var topWidth = Math.Min(620, Math.Max(400, screen.Width - 100));
            Width = topWidth;
            Height = 320;
            Left = screen.Left + (screen.Width - topWidth) / 2;
            Top = screen.Top;
        }
        else if (edge == DockEdge.Left)
        {
            Width = 280;
            Height = screen.Height;
            Left = screen.Left;
            Top = screen.Top;
        }
        else // DockEdge.Right
        {
            var width = 280;
            Width = width;
            Height = screen.Height;
            Left = screen.Right - width;
            Top = screen.Top;
        }
    }

    private void UpdateLayoutForEdge()
    {
        var edge = _settingsStore.DockEdge;
        CurrentDockEdge = edge;
        ExpandedView.VerticalAlignment = edge == DockEdge.Top ? VerticalAlignment.Top : VerticalAlignment.Center;
        ExpandedView.MaxHeight = edge == DockEdge.Top ? 300 : Math.Max(100, SystemParameters.WorkArea.Height - 40);
        if (edge == DockEdge.Top)
        {
            Height = 320;
            CollapsedTabs.HorizontalAlignment = HorizontalAlignment.Center;
            CollapsedTabs.VerticalAlignment = VerticalAlignment.Bottom;
            CollapsedEmptyMark.Width = 52;
            CollapsedEmptyMark.Height = 8;
            CollapsedEmptyMark.HorizontalAlignment = HorizontalAlignment.Center;
            CollapsedEmptyMark.VerticalAlignment = VerticalAlignment.Bottom;

            NotesStackPanel.Orientation = Orientation.Horizontal;
            DockScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            DockScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        else if (edge == DockEdge.Left)
        {
            Height = SystemParameters.WorkArea.Height;
            CollapsedEmptyMark.Width = 20;
            CollapsedEmptyMark.Height = 52;
            CollapsedTabs.HorizontalAlignment = HorizontalAlignment.Left;
            CollapsedTabs.VerticalAlignment = VerticalAlignment.Center;
            CollapsedEmptyMark.HorizontalAlignment = HorizontalAlignment.Left;
            CollapsedEmptyMark.VerticalAlignment = VerticalAlignment.Center;

            NotesStackPanel.Orientation = Orientation.Vertical;
            DockScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            DockScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        else // Right
        {
            Height = SystemParameters.WorkArea.Height;
            CollapsedEmptyMark.Width = 20;
            CollapsedEmptyMark.Height = 52;
            CollapsedTabs.HorizontalAlignment = HorizontalAlignment.Right;
            CollapsedTabs.VerticalAlignment = VerticalAlignment.Center;
            CollapsedEmptyMark.HorizontalAlignment = HorizontalAlignment.Right;
            CollapsedEmptyMark.VerticalAlignment = VerticalAlignment.Center;

            NotesStackPanel.Orientation = Orientation.Vertical;
            DockScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            DockScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    public void Expand()
    {
        if (_changingEdge) return;
        if (_isExpanded) return;
        _hoverExitTimer.Stop();
        _hoverDelayTimer.Stop();
        _isPeeking = false;
        IsPeeking = false;
        _isExpanded = true;
        PanelSlide.X = CurrentDockEdge == DockEdge.Left ? -16 : CurrentDockEdge == DockEdge.Right ? 16 : 0;
        PanelSlide.Y = CurrentDockEdge == DockEdge.Top ? -12 : 0;
        SpringMotion.To(PanelSlide, TranslateTransform.XProperty, 0, alwaysAnimate: true);
        SpringMotion.To(PanelSlide, TranslateTransform.YProperty, 0, alwaysAnimate: true);
        FadeView(CollapsedView, false);
        FadeView(ExpandedView, true);
        UpdatePosition();
        UpdateLayoutForEdge();
        UpdateBindings();
    }

    public void Collapse()
    {
        if (_changingEdge) return;
        _hoverDelayTimer.Stop();
        _isPeeking = false;
        IsPeeking = !_settingsStore.ShowDockOnHover;
        HoverZone.Visibility = IsPeeking ? Visibility.Visible : Visibility.Collapsed;
        _isExpanded = false;
        _isKeyboardFocused = false;
        SpringMotion.To(PanelSlide, TranslateTransform.XProperty, CurrentDockEdge == DockEdge.Left ? -16 : CurrentDockEdge == DockEdge.Right ? 16 : 0, alwaysAnimate: true);
        SpringMotion.To(PanelSlide, TranslateTransform.YProperty, CurrentDockEdge == DockEdge.Top ? -12 : 0, alwaysAnimate: true);
        FadeView(ExpandedView, false);
        FadeView(CollapsedView, true);
        UpdatePosition();
        UpdateLayoutForEdge();
    }

    private static void FadeView(UIElement view, bool visible)
    {
        view.IsHitTestVisible = visible;
        var from = view.Visibility == Visibility.Visible ? view.Opacity : 0;
        view.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(from, visible ? 1 : 0, TimeSpan.FromMilliseconds(180))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        animation.Completed += (_, _) => { if (!view.IsHitTestVisible) view.Visibility = Visibility.Collapsed; };
        view.BeginAnimation(OpacityProperty, animation);
    }

    private void Peek()
    {
        if (_changingEdge) return;
        _hoverDelayTimer.Stop();
        _isPeeking = true;
        IsPeeking = true;
        HoverZone.Visibility = Visibility.Visible;
        _isExpanded = false;
        CollapsedView.Visibility = Visibility.Visible;
        ExpandedView.Visibility = Visibility.Collapsed;
        UpdatePosition();
        UpdateLayoutForEdge();
    }

    private void UpdateBindings()
    {
        var notes = _noteStore.Notes;
        var structureChanged = _rows.Count != notes.Count || notes.Any(n => !_rows.TryGetValue(n.Id, out var row) || row.Note.Pinned != n.Pinned || row.Note.Archived != n.Archived);
        foreach (var note in notes)
        {
            if (_rows.TryGetValue(note.Id, out var row)) row.Update(note);
            else _rows.Add(note.Id, new NoteRow(note));
        }
        foreach (var id in _rows.Keys.Except(notes.Select(n => n.Id)).ToArray()) _rows.Remove(id);
        _observedNotes = notes.ToDictionary(n => n.Id);
        // Text and color edits retain the same containers, hover state and screen positions.
        if (!structureChanged && _bindingsInitialized) return;
        _bindingsInitialized = true;
        var pinned = _noteStore.PinnedNotes.Select(n => _rows[n.Id]).ToList();
        var unpinned = _noteStore.UnpinnedNotes.Select(n => _rows[n.Id]).ToList();
        var totalActive = pinned.Count + unpinned.Count;

        var visible = pinned.Concat(unpinned.Take(8)).ToList();
        // A pin changes membership only when a note enters/leaves the dock limit.
        // Keep existing cards and their positions; replacing ItemsSource resets hover.
        foreach (var row in _dockItems.Where(r => !visible.Contains(r)).ToArray()) _dockItems.Remove(row);
        foreach (var row in visible.Where(r => !_dockItems.Contains(r))) _dockItems.Add(row);
        CollapsedEmptyMark.Visibility = totalActive == 0 ? Visibility.Visible : Visibility.Collapsed;

        NoteCountText.Text = $"({totalActive})";

        // Headers
        PinnedHeader.Visibility = Visibility.Collapsed;
        PinnedTabs.Visibility = Visibility.Collapsed;

        NotesHeader.Visibility = Visibility.Collapsed;
        TabsList.Visibility = visible.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Empty state
        EmptyStatePanel.Visibility = totalActive == 0 ? Visibility.Visible : Visibility.Collapsed;
        DockScrollViewer.Visibility = totalActive > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Overflow
        if (unpinned.Count > 8)
        {
            OverflowTab.Visibility = Visibility.Visible;
            OverflowText.Text = $"+ {unpinned.Count - 8} more notes in Library →";
        }
        else
        {
            OverflowTab.Visibility = Visibility.Collapsed;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PositionCollapsedItems));
    }

    private void PositionCollapsedItems()
    {
        if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        CollapsedTabs.Width = ActualWidth;
        CollapsedTabs.Height = ActualHeight;
        CollapsedTabs.UpdateLayout();
        var count = CollapsedTabs.Items.Count;
        if (count == 0)
        {
            return;
        }

        var edge = _settingsStore.DockEdge;
        var cardSpacing = 86.0;
        var cardHeight = 76.0;
        var cardWidth = 216.0;

        for (var index = 0; index < count; index++)
        {
            if (CollapsedTabs.ItemContainerGenerator.ContainerFromIndex(index) is not UIElement container)
            {
                continue;
            }

            if (edge == DockEdge.Top)
            {
                var topSpacing = Math.Min(72, (ActualWidth - cardWidth) / Math.Max(1, count - 1));
                var totalWidth = cardWidth + (count - 1) * topSpacing;
                Canvas.SetLeft(container, Math.Max(0, (ActualWidth - totalWidth) / 2 + index * topSpacing));
                Canvas.SetTop(container, 0);
                HoverZone.Width = totalWidth;
                HoverZone.Height = 92;
                HoverZone.Margin = new Thickness(Math.Max(0, (ActualWidth - totalWidth) / 2), 0, 0, 0);
            }
            else
            {
                cardSpacing = Math.Min(cardSpacing, (ActualHeight - cardHeight - 80) / Math.Max(1, count - 1));
                var totalHeight = cardHeight + (count - 1) * cardSpacing;
                Canvas.SetLeft(container, edge == DockEdge.Right ? ActualWidth - cardWidth : 0);
                Canvas.SetTop(container, Math.Max(0, (ActualHeight - totalHeight) / 2 + index * cardSpacing));
                HoverZone.Width = cardWidth;
                HoverZone.Height = totalHeight + 16;
                HoverZone.Margin = new Thickness(edge == DockEdge.Right ? ActualWidth - cardWidth : 0, Math.Max(0, (ActualHeight - totalHeight) / 2 - 8), 0, 0);
            }
        }

    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isExpanded)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PositionCollapsedItems));
            }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_changingEdge) return;
        _hoverExitTimer.Stop();
        if (!_isExpanded && !_isPeeking)
        {
            _hoverDelayTimer.Stop();
            _hoverDelayTimer.Start();
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_changingEdge) return;
        _hoverDelayTimer.Stop();
        if ((_isExpanded || _isPeeking) && !_isKeyboardFocused)
        {
            _hoverExitTimer.Start();
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isExpanded)
        {
            Expand();
            e.Handled = true;
        }
    }

    private void ExpandDock_Click(object sender, RoutedEventArgs e) => Expand();

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.CreateNewNote();
    }

    private void OpenLibrary_Click(object sender, RoutedEventArgs e)
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.ShowLibrary();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.ShowSettings();
    }

    private void OverflowTab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.ShowLibrary();
    }

    public void FocusDock()
    {
        if (!_isExpanded)
        {
            Expand();
        }

        _isKeyboardFocused = true;
        _keyboardIndex = 0;
        Activate();
        Focus();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateKeyboardHighlight));
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Collapse();
            e.Handled = true;
            return;
        }

        if (!_isKeyboardFocused) return;

        var totalTabs = _noteStore.PinnedNotes.Count + Math.Min(_noteStore.UnpinnedNotes.Count, 8);
        if (totalTabs == 0) return;

        switch (e.Key)
        {
            case Key.Up:
            case Key.Left:
                _keyboardIndex = Math.Max(0, _keyboardIndex - 1);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Right:
                _keyboardIndex = Math.Min(totalTabs - 1, _keyboardIndex + 1);
                e.Handled = true;
                break;
            case Key.Home:
                _keyboardIndex = 0;
                e.Handled = true;
                break;
            case Key.End:
                _keyboardIndex = totalTabs - 1;
                e.Handled = true;
                break;
            case Key.Enter:
                OpenSelectedTab();
                e.Handled = true;
                break;
        }
        UpdateKeyboardHighlight();
    }

    private void UpdateKeyboardHighlight()
    {
        var index = 0;
        foreach (var list in new[] { PinnedTabs, TabsList })
        {
            for (var i = 0; i < list.Items.Count; i++)
            {
                if (list.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter presenter)
                {
                    presenter.ApplyTemplate();
                    if (VisualTreeHelper.GetChildrenCount(presenter) > 0 && VisualTreeHelper.GetChild(presenter, 0) is DockTab tab)
                        tab.SetKeyboardHighlight(index == _keyboardIndex);
                }
                index++;
            }
        }
    }

    private void OpenSelectedTab()
    {
        var pinned = PinnedNotes.Cast<NoteRow>().Select(r => r.Note).ToList();
        var unpinned = VisibleUnpinnedNotes.Cast<NoteRow>().Select(r => r.Note).ToList();

        Note? targetNote = null;
        if (_keyboardIndex >= 0 && _keyboardIndex < pinned.Count)
        {
            targetNote = pinned[_keyboardIndex];
        }
        else
        {
            var unpinnedIndex = _keyboardIndex - pinned.Count;
            if (unpinnedIndex >= 0 && unpinnedIndex < unpinned.Count)
            {
                targetNote = unpinned[unpinnedIndex];
            }
        }

        if (targetNote != null)
        {
            var windowManager = App.GetService<WindowManager>();
            windowManager.ShowNoteWindow(targetNote);
        }
    }
}
