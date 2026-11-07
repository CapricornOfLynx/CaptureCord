// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace CaptureCord.Capture;

internal sealed class ComApartment : IDisposable
{
    public ComApartment() => Native.Check(CoInitializeEx(0, 0));
    public void Dispose() => CoUninitialize();
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern void CoUninitialize();
}
