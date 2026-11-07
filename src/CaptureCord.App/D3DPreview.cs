// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CaptureCord.Core;

namespace CaptureCord.App;

public sealed class D3DPreview : HwndHost
{
    private D3DRenderer? renderer;
    public string AdapterName => renderer?.AdapterName ?? "Pending";
    protected override HandleRef BuildWindowCore(HandleRef parent)
    {
        var handle = CreateWindowEx(0, "static", "CaptureCord GPU preview", 0x56000000, 0, 0, 1, 1, parent.Handle, 0, 0, 0);
        if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(this, handle);
    }
    protected override void DestroyWindowCore(HandleRef window) { Reset(); DestroyWindow(window.Handle); }
    protected override nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Mouse input belongs to the WPF preview (including double-click fullscreen).
        if (message == 0x84) { handled = true; return -1; } // HTTRANSPARENT
        return base.WndProc(hwnd, message, wParam, lParam, ref handled);
    }
    public bool Present(VideoFrame frame)
    {
        if (Handle == 0) return false; // wait for WPF to create the child window
        EnsureRenderer(frame);
        return renderer!.Present(frame);
    }
    internal void VerifyUpload(VideoFrame frame)
    {
        EnsureRenderer(frame);
        if (!renderer!.VerifyUpload(frame).SequenceEqual(frame.Pixels))
            throw new InvalidOperationException("D3D11 GPU upload/readback verification failed.");
        renderer.Present(frame);
    }
    private void EnsureRenderer(VideoFrame frame)
    {
        if (renderer is not null && renderer.Width == frame.Width && renderer.Height == frame.Height) return;
        Reset();
        renderer = new(Handle, frame.Width, frame.Height);
    }
    public void Reset() { renderer?.Dispose(); renderer = null; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyWindow(nint window);
}
