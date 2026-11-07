// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using CaptureCord.Capture;

internal static unsafe class NativeChecks
{
    public static void SourceOwnership()
    {
        using var apartment = new ComApartment();
        using var mf = new MediaFoundation();
        var path = Path.Combine(Path.GetTempPath(), $"CaptureCord-source-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write("RIFF"u8); writer.Write(36 + 9600); writer.Write("WAVEfmt "u8);
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(48000); writer.Write(96000); writer.Write((short)2); writer.Write((short)16);
                writer.Write("data"u8); writer.Write(9600); writer.Write(new byte[9600]);
            }
            Native.Check(MFCreateSourceResolver(out var resolverPointer));
            using var resolver = new Com(resolverPointer);
            nint unknownPointer;
            int objectType;
            fixed (char* name = path)
                Native.Check(((delegate* unmanaged[Stdcall]<nint, char*, uint, nint, int*, nint*, int>)resolver.Slot(3))
                    (resolverPointer, name, 1, 0, &objectType, &unknownPointer));
            using var unknown = new Com(unknownPointer);
            var iid = new Guid("279a808d-aec7-40c8-9c6b-a6b492c78a66");
            nint sourcePointer;
            Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)unknown.Slot(0))(unknownPointer, &iid, &sourcePointer));
            using var source = new Com(sourcePointer);
            try
            {
                using (Native.Reader(source)) { }
                uint characteristics;
                // Before the fix, reader disposal shuts down the source and this fails.
                Native.Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)source.Slot(7))(sourcePointer, &characteristics));
                source.ShutdownSource();
                source.ShutdownSource(); // cleanup is safe even after an earlier shutdown
            }
            finally { source.ShutdownSource(); }
        }
        finally { File.Delete(path); }
    }
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
    [DllImport("mf.dll", ExactSpelling = true)] private static extern int MFCreateSourceResolver(out nint resolver);
}
