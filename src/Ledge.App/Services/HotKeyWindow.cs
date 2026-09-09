namespace Ledge.App.Services;

using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Ledge.Native.Interop;

internal sealed class HotKeyWindow : IDisposable
{
    private readonly nint _hwnd;
    private readonly Dispatcher _dispatcher;
    private int _nextId = 1;
    private readonly Dictionary<int, Action> _callbacks = new();
    private bool _disposed;
    private readonly User32.WndProcDelegate _wndProcDelegate;

    public event Action<int>? HotKeyPressed;

    public HotKeyWindow(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _wndProcDelegate = WndProc;

        var wc = new User32.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<User32.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = Marshal.GetHINSTANCE(typeof(HotKeyWindow).Module),
            lpszClassName = "LedgeHotKeyWindow",
            style = 0,
            hIcon = 0,
            hCursor = 0,
            hbrBackground = 0,
            lpszMenuName = null,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hIconSm = 0
        };

        User32.RegisterClassExInt(ref wc);

        _hwnd = User32.CreateWindowEx(
            0,
            "LedgeHotKeyWindow",
            "",
            0,
            0, 0, 0, 0,
            nint.Zero,
            nint.Zero,
            Marshal.GetHINSTANCE(typeof(HotKeyWindow).Module),
            nint.Zero);
    }

    public int Register(int modifiers, int key, Action callback)
    {
        var id = _nextId++;
        if (User32.RegisterHotKey(_hwnd, id, modifiers, key))
        {
            _callbacks[id] = callback;
            return id;
        }
        return 0;
    }

    public bool Unregister(int id)
    {
        if (_callbacks.Remove(id))
        {
            return User32.UnregisterHotKey(_hwnd, id);
        }
        return false;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam)
    {
        if (msg == User32.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_callbacks.TryGetValue(id, out var callback))
            {
                _dispatcher.InvokeAsync(callback);
            }
            HotKeyPressed?.Invoke(id);
        }
        return User32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var id in _callbacks.Keys.ToList())
            {
                User32.UnregisterHotKey(_hwnd, id);
            }
            _callbacks.Clear();
            User32.DestroyWindow(_hwnd);
            _disposed = true;
        }
    }
}