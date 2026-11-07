// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CaptureCord.App;

internal static class WindowAppearance
{
    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        int enabled = 1;
        // Older Windows builds may reject these optional DWM attributes.
        _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        int caption = 0x001B110B; // COLORREF: #0B111B
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
    }
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
}
