// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using CaptureCord.Core;

namespace CaptureCord.Capture;

public sealed class MediaFoundation : IDisposable
{
    private bool disposed;
    public MediaFoundation() => Native.Check(Native.MFStartup(0x20070, 0));
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Native.Check(Native.MFShutdown());
    }
}

public static class VideoDevices
{
    public static IReadOnlyList<DeviceInfo> Enumerate()
    {
        using var apartment = new ComApartment();
        using var attributes = Native.Attributes();
        attributes.SetGuid(Native.SourceType, Native.VideoCapture);
        Native.Check(Native.MFEnumDeviceSources(attributes.Pointer, out var array, out var count));
        var result = new List<DeviceInfo>();
        try
        {
            // Release every activation, including when reading a property fails.
            for (var i = 0; i < count; i++)
            {
                using var activation = new Com(Marshal.ReadIntPtr(array, i * IntPtr.Size));
                Marshal.WriteIntPtr(array, i * IntPtr.Size, 0);
                result.Add(new(activation.GetString(Native.SymbolicLink), activation.GetString(Native.FriendlyName)));
            }
        }
        finally
        {
            for (var i = 0; i < count; i++)
            {
                var p = Marshal.ReadIntPtr(array, i * IntPtr.Size);
                if (p != 0) new Com(p).Dispose();
            }
            Marshal.FreeCoTaskMem(array);
        }
        return result;
    }

    public static unsafe IReadOnlyList<VideoMode> Modes(string id)
    {
        using var apartment = new ComApartment();
        using var source = Native.Source(id);
        try
        {
            using var reader = Native.Reader(source);
            var result = new List<VideoMode>();
            for (uint index = 0; ; index++)
            {
                nint p = 0;
                var hr = ((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, int>)reader.Slot(5))(reader.Pointer, Native.Stream, index, &p);
                if (hr == unchecked((int)0xc00d36b9)) break; // MF_E_NO_MORE_TYPES
                Native.Check(hr);
                using var type = new Com(p);
                var size = type.GetULong(Native.FrameSize);
                var rate = type.GetULong(Native.FrameRate);
                result.Add(new((int)index, checked((int)(size >> 32)), (int)(size & uint.MaxValue), (uint)(rate >> 32), (uint)rate, type.GetGuid(Native.Subtype)));
            }
            return result;
        }
        finally { source.ShutdownSource(); }
    }
}
