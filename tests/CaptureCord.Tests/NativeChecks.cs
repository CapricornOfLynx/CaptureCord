// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using CaptureCord.Capture;

internal static unsafe class NativeChecks
{
    public static void Buffers()
    {
        using var apartment = new ComApartment();
        using var mf = new MediaFoundation();
        using var attributes = Native.Attributes();
        attributes.SetUInt(Native.LowLatency, 1);
        if (attributes.GetUInt(Native.LowLatency, 0) != 1) throw new Exception("IMFAttributes ABI mismatch");
        attributes.SetGuid(Native.MajorType, Native.Video);
        if (attributes.GetGuid(Native.MajorType) != Native.Video) throw new Exception("IMFAttributes GUID mismatch");
        attributes.SetString(Native.FriendlyName, "synthetic");
        if (attributes.GetString(Native.FriendlyName) != "synthetic") throw new Exception("IMFAttributes string mismatch");
        Native.Check(MFCreateMemoryBuffer(24, out var p));
        using var buffer = new Com(p);
        nint data;
        uint maximum, length;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, nint*, uint*, uint*, int>)buffer.Slot(3))(p, &data, &maximum, &length));
        try { Marshal.Copy(new byte[] { 1,2,3,4,5,6,7,8,99,99,99,99,9,10,11,12,13,14,15,16,99,99,99,99 }, 0, data, 24); }
        finally { Native.Check(((delegate* unmanaged[Stdcall]<nint, int>)buffer.Slot(4))(p)); }
        Native.Check(((delegate* unmanaged[Stdcall]<nint, uint, int>)buffer.Slot(6))(p, 24));
        var topDown = NativeBuffer.CopyRgb32(buffer, 2, 2, 12);
        if (!topDown.SequenceEqual(Enumerable.Range(1,16).Select(i => (byte)i))) throw new Exception("Padded top-down copy failed");
        var bottomUp = NativeBuffer.CopyRgb32(buffer, 2, 2, -12);
        if (bottomUp[0] != 9 || bottomUp[8] != 1 || bottomUp.Length != 16) throw new Exception("Negative-stride copy failed");
        try { NativeBuffer.CopyRgb32(buffer, 2, 3, 12); throw new Exception("Short buffer accepted"); }
        catch (IOException) { }
        if (NativeBuffer.CopyRgb32(buffer, 2, 2, 12)[0] != 1) throw new Exception("Buffer remained locked");
    }
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateMemoryBuffer(uint size, out nint buffer);
}
