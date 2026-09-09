namespace Ledge.App.Windows;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

    public DockWindow(NoteStore noteStore, SettingsStore settingsStore)
    {
        _noteStore = noteStore;
        _settingsStore = settingsStore;
        InitializeComponent();

        _hoverDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _hoverDelayTimer.Tick += (_, _) =>
        {
            _hoverDelayTimer.Stop();
            if (IsMouseOver && !_isExpanded && !_isPeeking)
            {
                Peek();
            }
        };

        _noteStore.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NoteStore.Notes)
                or nameof(NoteStore.VisibleNotes)
                or nameof(NoteStore.PinnedNotes)
                or nameof(NoteStore.UnpinnedNotes))
            {
                Dispatcher.Invoke(UpdateBindings);
            }
        };

        _settingsStore.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsStore.DockEdge))
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateLayoutForEdge();
                    UpdatePosition();
                });
            }
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowStyle(hwnd);
        UpdateLayoutForEdge();
        Collapse();
        UpdateBindings();

        _noteStore.NoteDeleted += _ => Dispatcher.Invoke(UpdateBindings);
        _noteStore.NoteRestored += _ => Dispatcher.Invoke(UpdateBindings);
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
        var screen = SystemParameters.WorkArea;
        var edge = _settingsStore.DockEdge;

        if (edge == DockEdge.Top)
        {
            var topWidth = Math.Min(620, Math.Max(400, screen.Width - 100));
            Width = topWidth;
            Height = _isExpanded ? 260 : _isPeeking ? 120 : 72;
            Left = screen.Left + (screen.Width - topWidth) / 2;
            Top = screen.Top;
        }
        else if (edge == DockEdge.Left)
        {
            Width = _isExpanded ? 260 : _isPeeking ? 230 : 72;
            Height = screen.Height;
            Left = screen.Left;
            Top = screen.Top;
        }
        else // DockEdge.Right
        {
            var width = _isExpanded ? 260 : _isPeeking ? 230 : 72;
            Width = width;
            Height = screen.Height;
            Left = screen.Right - width;
            Top = screen.Top;
        }
    }

    private void UpdateLayoutForEdge()
    {
        var edge = _settingsStore.DockEdge;
        if (edge == DockEdge.Top)
        {
            Height = _isExpanded ? 260 : _isPeeking ? 120 : 72;
            SetCollapsedOrientation(Orientation.Horizontal);
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
            SetCollapsedOrientation(Orientation.Vertical);
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
            SetCollapsedOrientation(Orientation.Vertical);
            CollapsedTabs.HorizontalAlignment = HorizontalAlignment.Right;
            CollapsedTabs.VerticalAlignment = VerticalAlignment.Center;
            CollapsedEmptyMark.HorizontalAlignment = HorizontalAlignment.Right;
            CollapsedEmptyMark.VerticalAlignment = VerticalAlignment.Center;

            NotesStackPanel.Orientation = Orientation.Vertical;
            DockScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            DockScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    private void SetCollapsedOrientation(Orientation orientation)
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, orientation);
        CollapsedTabs.ItemsPanel = new ItemsPanelTemplate(panel);
    }

    public void Expand()
    {
        _hoverDelayTimer.Stop();
        _isPeeking = false;
        IsPeeking = false;
        _isExpanded = true;
        CollapsedView.Visibility = Visibility.Collapsed;
        ExpandedView.Visibility = Visibility.Visible;
        UpdatePosition();
        UpdateBindings();
    }

    public void Collapse()
    {
        _hoverDelayTimer.Stop();
        _isPeeking = false;
        IsPeeking = false;
        _isExpanded = false;
        _isKeyboardFocused = false;
        ExpandedView.Visibility = Visibility.Collapsed;
        CollapsedView.Visibility = Visibility.Visible;
        UpdatePosition();
    }

    private void Peek()
    {
        _hoverDelayTimer.Stop();
        _isPeeking = true;
        IsPeeking = true;
        _isExpanded = false;
        CollapsedView.Visibility = Visibility.Visible;
        ExpandedView.Visibility = Visibility.Collapsed;
        UpdatePosition();
    }

    private void UpdateBindings()
    {
        var pinned = _noteStore.PinnedNotes;
        var unpinned = _noteStore.UnpinnedNotes;
        var totalActive = pinned.Count + unpinned.Count;

        PinnedNotes = pinned;
        VisibleUnpinnedNotes = unpinned.Take(8);
        CollapsedNotes = pinned.Concat(unpinned).Take(8).ToList();
        CollapsedEmptyMark.Visibility = totalActive == 0 ? Visibility.Visible : Visibility.Collapsed;

        NoteCountText.Text = $"({totalActive})";

        // Headers
        PinnedHeader.Visibility = pinned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PinnedTabs.Visibility = pinned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        NotesHeader.Visibility = (pinned.Count > 0 && unpinned.Count > 0) ? Visibility.Visible : Visibility.Collapsed;
        TabsList.Visibility = unpinned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

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
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isExpanded)
        {
            _hoverDelayTimer.Stop();
            _hoverDelayTimer.Start();
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverDelayTimer.Stop();
        if ((_isExpanded || _isPeeking) && !_isKeyboardFocused)
        {
            // Verify pointer really left window
            var pos = e.GetPosition(this);
            if (pos.X < 0 || pos.Y < 0 || pos.X >= ActualWidth || pos.Y >= ActualHeight)
            {
                Collapse();
            }
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

    private void OnTabClicked(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element != null && element != sender)
        {
            if (element is FrameworkElement fe && fe.DataContext is Note note)
            {
                var windowManager = App.GetService<WindowManager>();
                windowManager.ShowNoteWindow(note);
                e.Handled = true;
                return;
            }
            element = VisualTreeHelper.GetParent(element);
        }
    }

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
        Focus();
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
    }

    private void OpenSelectedTab()
    {
        var pinned = _noteStore.PinnedNotes;
        var unpinned = _noteStore.UnpinnedNotes;

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
