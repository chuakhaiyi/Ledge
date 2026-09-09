namespace Ledge.App.Windows;

using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Ledge.Native.Interop;
using Ledge.Core.Services;
using Ledge.Core.Models;

public partial class NoteWindow : Window
{
    private readonly NoteStore _noteStore;
    private readonly SettingsStore _settingsStore;
    private bool _isClosing;

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

        if (note.Position != null)
        {
            Left = note.Position.X;
            Top = note.Position.Y;
            Width = note.Position.Width;
            Height = note.Position.Height;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowStyle(hwnd);
        FocusEditor();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == User32.WM_NCHITTEST)
        {
            var x = (short)(lParam.ToInt32() & 0xFFFF);
            var y = (short)(lParam.ToInt32() >> 16);
            var pt = new Point(x, y);
            pt = PointFromScreen(pt);

            var border = 6;
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
            if (pt.Y <= 30) { handled = true; return (nint)User32.HTCAPTION; }
        }
        return nint.Zero;
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

    private void Window_Closed(object sender, EventArgs e)
    {
        if (!_isClosing && Note != null)
        {
            _noteStore.SetPosition(Note, new NotePosition(Left, Top, Width, Height));
        }
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        if (Note != null)
        {
            _noteStore.SetPosition(Note, new NotePosition(Left, Top, Width, Height));
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Note != null)
        {
            _noteStore.SetPosition(Note, new NotePosition(Left, Top, Width, Height));
        }
    }

    private void Editor_ColorChanged(NoteColor color)
    {
        if (Note != null)
        {
            _noteStore.SetColor(Note, color);
            Note = Note with { Color = color };
        }
    }

    private void CycleColor()
    {
        if (Note != null)
        {
            var nextColor = Note.Color.Next();
            _noteStore.SetColor(Note, nextColor);
            Note = Note with { Color = nextColor };
        }
    }

    private void Editor_DeleteRequested()
{
    if (Note != null)
    {
        _isClosing = true;
        DeleteRequested?.Invoke();
        Close();
    }
}

    private void Editor_PinToggled()
    {
        if (Note != null)
        {
            _noteStore.Update(Note with { Pinned = !Note.Pinned });
        }
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