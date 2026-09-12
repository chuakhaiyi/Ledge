namespace Ledge.App.Windows;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Ledge.Native.Interop;
using Ledge.Core.Services;
using Ledge.Core.Models;
using Ledge.App.Services;

public partial class NoteWindow : Window
{
    private readonly NoteStore _noteStore;
    private readonly SettingsStore _settingsStore;
    private bool _isClosing;
    private bool _allowClose;
    private bool _closeStarted;
    private bool _closed;
    private HwndSource? _source;

    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(nameof(Note), typeof(Note), typeof(NoteWindow),
            new PropertyMetadata(null));

    public event Action? DeleteRequested;

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public NoteWindow(Note note, NoteStore noteStore, SettingsStore settingsStore)
    {
        _noteStore = noteStore;
        _settingsStore = settingsStore;
        Note = note;
        InitializeComponent();
        NoteRoot.ContextMenu = Ledge.App.Controls.AppMenus.NoteActions(Editor_DeleteRequested);
        _noteStore.PropertyChanged += OnNotesChanged;

        if (note.Position != null)
        {
            Left = note.Position.X;
            Top = note.Position.Y;
            Width = note.Position.Width;
            Height = note.Position.Height;
        }
        else
        {
            var screen = SystemParameters.WorkArea;
            var edge = _settingsStore.DockEdge;
            Width = 280;
            Height = 320;
            if (edge == DockEdge.Right)
            {
                Left = Math.Max(screen.Left + 20, screen.Right - Width - 60);
                Top = screen.Top + 60;
            }
            else if (edge == DockEdge.Left)
            {
                Left = screen.Left + 60;
                Top = screen.Top + 60;
            }
            else // Top
            {
                Left = (screen.Left + screen.Right - Width) / 2;
                Top = screen.Top + 60;
            }
        }
    }

    private void OnNotesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NoteStore.Notes) || Note == null) return;
        var current = _noteStore.Notes.FirstOrDefault(n => n.Id == Note.Id);
        if (current != null) Note = current;
    }

    private void Editor_TextChanged(string text)
    {
        if (Note != null) _noteStore.SetText(Note, text);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowStyle(hwnd);
        FocusEditor();
        if (SystemParameters.ClientAreaAnimation)
        {
            NoteScale.ScaleX = NoteScale.ScaleY = .94;
            NoteSlide.Y = 10;
            SpringMotion.To(NoteScale, ScaleTransform.ScaleXProperty, 1);
            SpringMotion.To(NoteScale, ScaleTransform.ScaleYProperty, 1);
            SpringMotion.To(NoteSlide, TranslateTransform.YProperty, 0);
            NoteRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == User32.WM_NCHITTEST)
        {
            var x = (short)(lParam.ToInt32() & 0xFFFF);
            var y = (short)(lParam.ToInt32() >> 16);
            var pt = new Point(x, y);
            pt = PointFromScreen(pt);

            var border = 18;
            var left = pt.X <= border;
            var right = pt.X >= ActualWidth - border;
            var top = pt.Y <= border;
            var bottom = pt.Y >= ActualHeight - border;

            if (top && left) { handled = true; return (nint)User32.HTTOPLEFT; }
            if (top && right) { handled = true; return (nint)User32.HTTOPRIGHT; }
            if (bottom && left) { handled = true; return (nint)User32.HTBOTTOMLEFT; }
            if (bottom && right) { handled = true; return (nint)User32.HTBOTTOMRIGHT; }
            if (left) { handled = true; return (nint)User32.HTLEFT; }
            if (right) { handled = true; return (nint)User32.HTRIGHT; }
            if (top) { handled = true; return (nint)User32.HTTOP; }
            if (bottom) { handled = true; return (nint)User32.HTBOTTOM; }
            if (pt.Y <= 56)
            {
                var hit = InputHitTest(pt) as DependencyObject;
                if (IsInteractiveControl(hit))
                {
                    return nint.Zero;
                }
                handled = true;
                return (nint)User32.HTCAPTION;
            }
        }
        return nint.Zero;
    }

    private static bool IsInteractiveControl(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase ||
                element is System.Windows.Controls.TextBox ||
                element is System.Windows.Controls.ComboBox ||
                element is System.Windows.Controls.ContextMenu)
            {
                return true;
            }
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void SetWindowStyle(nint hwnd)
    {
        var exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        exStyle |= User32.WS_EX_TOOLWINDOW | User32.WS_EX_TOPMOST;
        User32.SetWindowLongPtr(hwnd, User32.GWL_EXSTYLE, exStyle);

        var margins = new DwmApi.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmApi.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        var darkMode = 1;
        DwmApi.DwmSetWindowAttribute(hwnd, DwmApi.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, 4);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Back && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Editor_DeleteRequested();
        }
        else if (e.Key == Key.OemPeriod && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CycleColor();
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || !IsLoaded || !SystemParameters.ClientAreaAnimation) return;
        e.Cancel = true;
        if (_closeStarted) return;
        _closeStarted = true;
        Editor.ClosePalette();
        SpringMotion.To(NoteScale, ScaleTransform.ScaleXProperty, .94);
        SpringMotion.To(NoteScale, ScaleTransform.ScaleYProperty, .94);
        SpringMotion.To(NoteSlide, TranslateTransform.YProperty, 10);
        NoteRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } });
        await Task.Delay(170);
        if (_closed) return;
        _allowClose = true;
        Close();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _closed = true;
        SpringMotion.Stop(NoteScale);
        SpringMotion.Stop(NoteSlide);
        Editor.ClosePalette();
        _noteStore.PropertyChanged -= OnNotesChanged;
        _source?.RemoveHook(WndProc);
        _source = null;
        if (!_isClosing && Note != null)
        {
            _noteStore.SetPosition(Note, new NotePosition(Left, Top, Width, Height));
        }
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        if (IsLoaded && Note != null)
        {
            _noteStore.SetPosition(Note, new NotePosition(Left, Top, Width, Height));
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded && Note != null)
        {
            _noteStore.SetPosition(Note, new NotePosition(Left, Top, Width, Height));
        }
    }

    private void Editor_ColorChanged(string color)
    {
        if (Note != null)
        {
            _noteStore.SetColor(Note, color);
        }
    }

    private void CycleColor()
    {
        if (Note != null)
        {
            var nextColor = Note.Color.Next();
            _noteStore.SetColor(Note, nextColor);
        }
    }

    private void Editor_DeleteRequested()
{
    if (Note != null)
    {
        _isClosing = true;
        DeleteRequested?.Invoke();
        if (!_closed) Close();
    }
}

    private void Editor_PinToggled()
    {
        if (Note != null)
        {
            _noteStore.Update(Note with { Pinned = !Note.Pinned });
        }
    }

    private void Editor_CloseRequested()
    {
        Close();
    }

    private void Editor_NewNoteRequested()
    {
        var windowManager = App.GetService<WindowManager>();
        windowManager.CreateNewNote();
    }

    public void FocusEditor()
    {
        Editor.FocusEditor();
    }

    public void EnsureOnScreen()
    {
        var screen = SystemParameters.WorkArea;
        if (Left + Width > screen.Right) Left = screen.Right - Width;
        if (Top + Height > screen.Bottom) Top = screen.Bottom - Height;
        if (Left < screen.Left) Left = screen.Left;
        if (Top < screen.Top) Top = screen.Top;
    }
}
