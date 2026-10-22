using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PcCleaner.App;

/// <summary>Asks Windows (10 20H1+ / 11) to draw the window's title bar dark to match the app.</summary>
public static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void Enable(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            int on = 1;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref on, sizeof(int)); // ignored on older Windows
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
