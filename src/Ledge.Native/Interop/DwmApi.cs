namespace Ledge.Native.Interop;

using System.Runtime.InteropServices;

public static class DwmApi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_DONOTROUND = 1;
}