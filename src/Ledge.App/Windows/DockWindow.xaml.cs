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
    private bool _dismissOnDeactivate;
    private int _keyboardIndex = -1;
    private readonly DispatcherTimer _hoverDelayTimer;
    private readonly DispatcherTimer _hoverExitTimer;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Dictionary<string, NoteRow> _rows = new();
    private readonly ObservableCollection<NoteRow> _dockItems = new();
    private Dictionary<string, Note> _observedNotes = new();
    private bool _changingEdge;
    private bool _closed;
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

    private async void ChangeEdge()
    {
        if (_changingEdge || _closed) return;
        _changingEdge = true;
        var cards = CollapsedCards().ToArray();
        var scale = new ScaleTransform();
        RootGrid.RenderTransform = scale;
        foreach (var tab in cards) { tab.IsRelocating = true; tab.FreezeSlide(); }
        SpringMotion.Stop(PanelSlide);
        _hoverDelayTimer.Stop();
        _hoverExitTimer.Stop();

        // Animate the live presenters inside their original HWND and clipping region.
        // Relocate only at zero scale: no screenshot, new window, or visible position jump.
        async Task Phase(bool merge)
        {
            RootGrid.UpdateLayout();
            var surfaces = _isExpanded
                ? new[] { (Owner: (FrameworkElement)ExpandedGrid, Surface: (FrameworkElement)ExpandedGrid) }
                : cards.Select(tab => (Owner: (FrameworkElement)VisualTreeHelper.GetParent(tab),
                    Surface: (FrameworkElement)tab.FindName("TabBorder"))).ToArray();
            if (surfaces.Length == 0)
                surfaces = new[] { (Owner: (FrameworkElement)CollapsedEmptyMark, Surface: (FrameworkElement)CollapsedEmptyMark) };
            var centers = surfaces.Select(item =>
            {
                var bounds = item.Surface.TransformToAncestor(RootGrid).TransformBounds(new Rect(item.Surface.RenderSize));
                return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            }).ToArray();
            var center = new Point(centers.Average(p => p.X), centers.Average(p => p.Y));
            scale.CenterX = Math.Clamp(center.X, 0, ActualWidth);
            scale.CenterY = Math.Clamp(center.Y, 0, ActualHeight);
            var storyboard = new Storyboard();
            void Animate(FrameworkElement target, string property, double start, double end, int delay, int duration)
            {
                var animation = new DoubleAnimation(start, end, TimeSpan.FromMilliseconds(duration))
                {
                    BeginTime = TimeSpan.FromMilliseconds(delay),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(animation, target);
                Storyboard.SetTargetProperty(animation, new PropertyPath(property));
                storyboard.Children.Add(animation);
            }
            for (var i = 0; i < surfaces.Length; i++)
            {
                var offset = center - centers[i];
                var owner = surfaces[i].Owner;
                owner.RenderTransform = new TranslateTransform(merge ? 0 : offset.X, merge ? 0 : offset.Y);
                Animate(owner, "RenderTransform.X", merge ? 0 : offset.X, merge ? offset.X : 0, merge ? 0 : 100, 240);
                Animate(owner, "RenderTransform.Y", merge ? 0 : offset.Y, merge ? offset.Y : 0, merge ? 0 : 100, 240);
            }
            Animate(RootGrid, "RenderTransform.ScaleX", merge ? 1 : 0, merge ? 0 : 1, merge ? 240 : 0, 100);
            Animate(RootGrid, "RenderTransform.ScaleY", merge ? 1 : 0, merge ? 0 : 1, merge ? 240 : 0, 100);
            var completed = new TaskCompletionSource();
            void Finish(object? sender, EventArgs args) => completed.TrySetResult();
            storyboard.Completed += Finish;
            Closed += Finish;
            storyboard.Begin(this, true);
            try { await completed.Task; }
            finally
            {
                // Commit the invisible/visible endpoint before removing animation clocks.
                scale.ScaleX = scale.ScaleY = merge ? 0 : 1;
                storyboard.Remove(this);
                Closed -= Finish;
                foreach (var item in surfaces) item.Owner.RenderTransform = Transform.Identity;
            }
        }

        try
        {
            while (!_closed && CurrentDockEdge != _settingsStore.DockEdge)
            {
                if (IsLoaded && IsVisible) await Phase(true);
                if (_closed) return;
                // Scale remains exactly zero through the orientation and native-window swap.
                PlaceDock();
                UpdateLayoutForEdge();
                UpdateLayout();
                PositionCollapsedItems();
                foreach (var tab in cards) tab.SettleLayout();
                UpdateLayout();
                if (IsLoaded && IsVisible) await Phase(false);
            }
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Dock transition failed: {0}", exception);
            if (!_closed) { PlaceDock(); UpdateLayoutForEdge(); PositionCollapsedItems(); }
        }
        finally
        {
            RootGrid.RenderTransform = Transform.Identity;
            foreach (var tab in cards) tab.IsRelocating = false;
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
        _dismissOnDeactivate = false;
        SpringMotion.To(PanelSlide, TranslateTransform.XProperty, CurrentDockEdge == DockEdge.Left ? -16 : CurrentDockEdge == DockEdge.Right ? 16 : 0, alwaysAnimate: true);
        SpringMotion.To(PanelSlide, TranslateTransform.YProperty, CurrentDockEdge == DockEdge.Top ? -12 : 0, alwaysAnimate: true);
        FadeView(ExpandedView, false);
        FadeView(CollapsedView, true);
        UpdatePosition();
        UpdateLayoutForEdge();
        PositionCollapsedItems();
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
        PositionCollapsedItems();
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

        var edge = CurrentDockEdge;
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
                // Open the whole fan once, so individual hover never covers a sibling.
                cardWidth = Math.Max(32, Math.Min(216, (ActualWidth - 16 - (count - 1) * 8) / count));
                var topSpacing = Math.Min(72, cardWidth + 8);
                var totalWidth = cardWidth + (count - 1) * topSpacing;
                var restingLeft = (ActualWidth - totalWidth) / 2 + index * topSpacing;
                var spreadWidth = count * cardWidth + (count - 1) * 8;
                var spreadLeft = (ActualWidth - spreadWidth) / 2 + index * (cardWidth + 8);
                Canvas.SetLeft(container, restingLeft);
                Canvas.SetTop(container, 0);
                if (container is ContentPresenter presenter && VisualTreeHelper.GetChild(presenter, 0) is DockTab tab)
                    tab.SetFanLayout(cardWidth, IsPeeking ? spreadLeft - restingLeft : 0, !_changingEdge);
                HoverZone.Width = spreadWidth;
                HoverZone.Height = 92;
                HoverZone.Margin = new Thickness(Math.Max(0, (ActualWidth - spreadWidth) / 2), 0, 0, 0);
            }
            else
            {
                if (container is ContentPresenter presenter && VisualTreeHelper.GetChild(presenter, 0) is DockTab tab)
                {
                    // Keep the vertical stack's visible/hidden proportion identical to
                    // the top fan: 72px of a 216px card remains visible at rest.
                    var restingSpacing = cardHeight * (72.0 / 216.0);
                    var spreadSpacing = cardHeight + 8;
                    var availableSpacing = (ActualHeight - cardHeight - 80) / Math.Max(1, count - 1);
                    restingSpacing = Math.Min(restingSpacing, Math.Max(0, availableSpacing));
                    var separatedSpacing = Math.Min(spreadSpacing, Math.Max(restingSpacing, availableSpacing));
                    var restingHeight = cardHeight + (count - 1) * restingSpacing;
                    var separatedHeight = cardHeight + (count - 1) * separatedSpacing;
                    var restingTop = Math.Max(0, (ActualHeight - restingHeight) / 2 + index * restingSpacing);
                    var separatedTop = Math.Max(0, (ActualHeight - separatedHeight) / 2 + index * separatedSpacing);
                    Canvas.SetLeft(container, edge == DockEdge.Right ? ActualWidth - cardWidth : 0);
                    Canvas.SetTop(container, restingTop);
                    tab.SetFanLayout(cardWidth, 0, IsPeeking ? separatedTop - restingTop : 0, !_changingEdge);
                    HoverZone.Width = cardWidth;
                    HoverZone.Height = separatedHeight + 16;
                    HoverZone.Margin = new Thickness(edge == DockEdge.Right ? ActualWidth - cardWidth : 0, Math.Max(0, (ActualHeight - separatedHeight) / 2 - 8), 0, 0);
                }
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
        _dismissOnDeactivate = true;
        _keyboardIndex = 0;
        Activate();
        Focus();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateKeyboardHighlight));
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (_dismissOnDeactivate && _isExpanded && !_changingEdge)
            Collapse();
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
