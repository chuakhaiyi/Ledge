namespace Ledge.App.Windows;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;
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
    private bool _isKeyboardFocused = false;
    private int _keyboardIndex = -1;
    private DispatcherTimer? _hoverDelayTimer;
    private bool _mouseInHotzone = false;

    public static readonly DependencyProperty PinnedNotesProperty =
        DependencyProperty.Register(nameof(PinnedNotes), typeof(System.Collections.IEnumerable), typeof(DockWindow),
            new PropertyMetadata(null));

    public System.Collections.IEnumerable PinnedNotes
    {
        get => (System.Collections.IEnumerable)GetValue(PinnedNotesProperty);
        set => SetValue(PinnedNotesProperty, value);
    }

    public static readonly DependencyProperty UnpinnedNotesProperty =
        DependencyProperty.Register(nameof(UnpinnedNotes), typeof(System.Collections.IEnumerable), typeof(DockWindow),
            new PropertyMetadata(null));

    public System.Collections.IEnumerable UnpinnedNotes
    {
        get => (System.Collections.IEnumerable)GetValue(UnpinnedNotesProperty);
        set => SetValue(UnpinnedNotesProperty, value);
    }

    public static readonly DependencyProperty VisibleUnpinnedNotesProperty =
        DependencyProperty.Register(nameof(VisibleUnpinnedNotes), typeof(System.Collections.IEnumerable), typeof(DockWindow),
            new PropertyMetadata(null));

    public System.Collections.IEnumerable VisibleUnpinnedNotes
    {
        get => (System.Collections.IEnumerable)GetValue(VisibleUnpinnedNotesProperty);
        set => SetValue(VisibleUnpinnedNotesProperty, value);
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
            if (_mouseInHotzone && !_isExpanded)
            {
                Expand();
            }
        };

        _noteStore.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NoteStore.Notes)
                or nameof(NoteStore.VisibleNotes)
                or nameof(NoteStore.PinnedNotes)
                or nameof(NoteStore.UnpinnedNotes))
            {
                UpdateBindings();
            }
        };

        _settingsStore.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsStore.DockEdge))
            {
                UpdatePosition();
            }
        };

        UpdateBindings();
    }

    private void UpdateBindings()
    {
        PinnedNotes = _noteStore.PinnedNotes;
        UnpinnedNotes = _noteStore.UnpinnedNotes;
        VisibleUnpinnedNotes = _noteStore.UnpinnedNotes.Take(8);
        UpdateOverflowVisibility();
        UpdatePinnedVisibility();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowStyle(hwnd);
        UpdatePosition();

        _noteStore.NoteDeleted += OnNoteDeleted;
        _noteStore.NoteRestored += OnNoteRestored;
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        // Window source initialized - could add additional setup here if needed
    }

    private void SetWindowStyle(nint hwnd)
    {
        var exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        exStyle |= User32.WS_EX_TOOLWINDOW | User32.WS_EX_TOPMOST | User32.WS_EX_NOACTIVATE;
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle);

        var margins = new DwmApi.MARGINS { cxLeftWidth = -1 };
        DwmApi.DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    public void UpdatePosition()
    {
        var screen = SystemParameters.WorkArea;
        var edge = _settingsStore.DockEdge;

        if (edge == DockEdge.Right)
        {
            Left = screen.Right - Width;
        }
        else
        {
            Left = screen.Left;
        }

        Top = screen.Top;
        Height = screen.Height;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isExpanded)
        {
            ExpandedArea.Width = 200;
        }
    }

    private void Hotzone_MouseEnter(object sender, MouseEventArgs e)
    {
        _mouseInHotzone = true;
        if (!_isExpanded)
        {
            _hoverDelayTimer.Stop();
            _hoverDelayTimer.Start();
        }
    }

    private void Hotzone_MouseLeave(object sender, MouseEventArgs e)
    {
        _mouseInHotzone = false;
        _hoverDelayTimer.Stop();
        if (!_isKeyboardFocused)
        {
            Collapse();
        }
    }

    private void Expand()
    {
        _isExpanded = true;
        CollapsedEdge.Visibility = Visibility.Collapsed;
        ExpandedArea.Visibility = Visibility.Visible;
        Width = 220;

        // Animate tabs to peek state
        AnimateTabs(DockState.Peeked);
    }

    private void Collapse()
    {
        _isExpanded = false;
        ExpandedArea.Visibility = Visibility.Collapsed;
        CollapsedEdge.Visibility = Visibility.Visible;
        Width = 20;

        // Animate tabs to idle state
        AnimateTabs(DockState.Idle);
    }

    private void AnimateTabs(DockState state)
    {
        // Animate tabs in unpinned list
        AnimateTabsInList(TabsList, state);

        // Animate tabs in pinned list
        AnimateTabsInList(PinnedTabs, state);
    }

    private void AnimateTabsInList(ItemsControl itemsControl, DockState state)
    {
        if (itemsControl.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            return;

        for (int i = 0; i < itemsControl.Items.Count; i++)
        {
            if (itemsControl.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container)
            {
                if (FindTabControl(container) is DockTab tab)
                {
                    tab.SetState(state);
                }
            }
        }
    }

    private DockTab? FindTabControl(FrameworkElement element)
    {
        if (element is DockTab tab) return tab;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;
            if (child != null)
            {
                var result = FindTabControl(child);
                if (result != null) return result;
            }
        }
        return null;
    }

    private void UpdateOverflowVisibility()
    {
        var unpinnedCount = _noteStore.UnpinnedNotes.Count;
        var totalVisible = _noteStore.PinnedNotes.Count + Math.Min(unpinnedCount, 8);
        OverflowTab.Visibility = unpinnedCount > 8 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePinnedVisibility()
    {
        var hasPinned = _noteStore.PinnedNotes.Count > 0;
        PinnedTabs.Visibility = hasPinned ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OverflowTab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.ShowLibrary();
    }

    private void OnNoteDeleted(Note note)
    {
        UpdateBindings();
    }

    private void OnNoteRestored(Note note)
    {
        UpdateBindings();
    }

    public void FocusDock()
    {
        if (!_isExpanded)
        {
            Expand();
        }

        _isKeyboardFocused = true;
        _keyboardIndex = 0;
        UpdateKeyboardSelection();

        // Ensure focus for key events
        Focus();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_isKeyboardFocused) return;

        var totalTabs = _noteStore.PinnedNotes.Count + Math.Min(_noteStore.UnpinnedNotes.Count, 8);

        switch (e.Key)
        {
            case Key.Up:
                _keyboardIndex = Math.Max(0, _keyboardIndex - 1);
                UpdateKeyboardSelection();
                e.Handled = true;
                break;
            case Key.Down:
                _keyboardIndex = Math.Min(totalTabs - 1, _keyboardIndex + 1);
                UpdateKeyboardSelection();
                e.Handled = true;
                break;
            case Key.Home:
                _keyboardIndex = 0;
                UpdateKeyboardSelection();
                e.Handled = true;
                break;
            case Key.End:
                _keyboardIndex = totalTabs - 1;
                UpdateKeyboardSelection();
                e.Handled = true;
                break;
            case Key.Enter:
                OpenSelectedTab();
                e.Handled = true;
                break;
            case Key.Escape:
                _isKeyboardFocused = false;
                _keyboardIndex = -1;
                ClearKeyboardSelection();
                Collapse();
                e.Handled = true;
                break;
        }
    }

    private void UpdateKeyboardSelection()
    {
        // Clear previous selection
        ClearKeyboardSelection();

        var totalTabs = _noteStore.PinnedNotes.Count + Math.Min(_noteStore.UnpinnedNotes.Count, 8);
        if (_keyboardIndex < 0 || _keyboardIndex >= totalTabs) return;

        // Find and highlight the selected tab
        HighlightTabAtIndex(_keyboardIndex, true);
    }

    private void ClearKeyboardSelection()
    {
        // Clear highlight from all tabs
        HighlightTabAtIndex(-1, false);
    }

    private void HighlightTabAtIndex(int index, bool highlight)
    {
        var pinnedCount = _noteStore.PinnedNotes.Count;

        // Check pinned tabs first
        if (index < pinnedCount)
        {
            if (PinnedTabs.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                if (PinnedTabs.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
                {
                    if (FindTabControl(container) is DockTab tab)
                    {
                        tab.SetKeyboardHighlight(highlight);
                    }
                }
            }
        }
        else
        {
            // Check unpinned tabs
            var unpinnedIndex = index - pinnedCount;
            var unpinnedCount = Math.Min(_noteStore.UnpinnedNotes.Count, 8);

            if (unpinnedIndex < unpinnedCount)
            {
                if (TabsList.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    if (TabsList.ItemContainerGenerator.ContainerFromIndex(unpinnedIndex) is FrameworkElement container)
                    {
                        if (FindTabControl(container) is DockTab tab)
                        {
                            tab.SetKeyboardHighlight(highlight);
                        }
                    }
                }
            }
        }
    }

    private void OpenSelectedTab()
    {
        var pinnedCount = _noteStore.PinnedNotes.Count;
        Note? targetNote = null;

        if (_keyboardIndex < pinnedCount)
        {
            targetNote = _noteStore.PinnedNotes[_keyboardIndex];
        }
        else
        {
            var unpinnedIndex = _keyboardIndex - pinnedCount;
            var unpinned = _noteStore.UnpinnedNotes;
            if (unpinnedIndex < unpinned.Count)
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