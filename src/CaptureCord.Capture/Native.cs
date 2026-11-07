// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;

namespace CaptureCord.Capture;

// Minimal Windows SDK ABI surface. Slots are from mfobjects.h, mfidl.h and mfreadwrite.h.
// Each Com owns exactly one reference; no RCW and raw-pointer ownership are mixed.
internal sealed unsafe class Com(nint pointer) : IDisposable
{
    public nint Pointer { get; private set; } = pointer;
    public void* Slot(int index) => (void*)(*(nint**)Pointer)[index];
    public void Dispose()
    {
        if (Pointer == 0) return;
        ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(2))(Pointer);
        Pointer = 0;
    }
    public void SetGuid(Guid key, Guid value) => Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Slot(24))(Pointer, &key, &value));
    public Guid GetGuid(Guid key)
    {
        Guid value;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Slot(10))(Pointer, &key, &value));
        return value;
    }
    public void SetUInt(Guid key, uint value) => Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Slot(21))(Pointer, &key, value));
    public uint GetUInt(Guid key, uint fallback)
    {
        uint value;
        return ((delegate* unmanaged[Stdcall]<nint, Guid*, uint*, int>)Slot(7))(Pointer, &key, &value) < 0 ? fallback : value;
    }
    public ulong GetULong(Guid key)
    {
        ulong value;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, ulong*, int>)Slot(8))(Pointer, &key, &value));
        return value;
    }
    public void SetString(Guid key, string value)
    {
        fixed (char* text = value) Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, char*, int>)Slot(25))(Pointer, &key, text));
    }
    public string GetString(Guid key)
    {
        nint text = 0;
        uint length;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, uint*, int>)Slot(13))(Pointer, &key, &text, &length));
        try { return Marshal.PtrToStringUni(text, checked((int)length)) ?? ""; }
        finally { Marshal.FreeCoTaskMem(text); }
    }
    public void ShutdownSource()
    {
        var hr = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(12))(Pointer);
        // Stop/disconnect may already have shut down the source. Cleanup must not
        // replace a successfully enumerated result or an earlier capture error.
        if (hr != unchecked((int)0xc00d3e85)) Native.Check(hr); // MF_E_SHUTDOWN
    }
}

internal static class Native
{
    public static readonly Guid SourceType = new("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");
    public static readonly Guid VideoCapture = new("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");
    public static readonly Guid FriendlyName = new("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");
    public static readonly Guid SymbolicLink = new("58f0aad8-22bf-4f8a-bb3d-d2c4978c6e2f");
    public static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid Video = new("73646976-0000-0010-8000-00aa00389b71");
    public static readonly Guid Rgb32 = new("00000016-0000-0010-8000-00aa00389b71");
    public static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    public static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    public static readonly Guid Stride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    public static readonly Guid Processing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
    public static readonly Guid LowLatency = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");
    public static readonly Guid DisconnectOnShutdown = new("56b67165-219e-456d-a22e-2d3004c7fe56");
    public const uint Stream = 0xfffffffc; // MF_SOURCE_READER_FIRST_VIDEO_STREAM
    public static void Check(int hr) { Marshal.ThrowExceptionForHR(hr); }
    public static Com Attributes()
    {
        Check(MFCreateAttributes(out var result, 4));
        return new(result);
    }
    public static Com Source(string id)
    {
        using var attributes = Attributes();
        attributes.SetGuid(SourceType, VideoCapture);
        attributes.SetString(SymbolicLink, id);
        Check(MFCreateDeviceSource(attributes.Pointer, out var result));
        return new(result);
    }
    public static Com Reader(Com source)
    {
        using var attributes = Attributes();
        attributes.SetUInt(Processing, 1);
        attributes.SetUInt(LowLatency, 1);
        // The caller owns the source lifetime and always shuts it down explicitly.
        attributes.SetUInt(DisconnectOnShutdown, 1);
        Check(MFCreateSourceReaderFromMediaSource(source.Pointer, attributes.Pointer, out var result));
        return new(result);
    }
    [DllImport("mfplat.dll", ExactSpelling = true)] internal static extern int MFStartup(uint version, uint flags);
    [DllImport("mfplat.dll", ExactSpelling = true)] internal static extern int MFShutdown();
    [DllImport("mfplat.dll", ExactSpelling = true)] internal static extern int MFCreateAttributes(out nint attributes, uint size);
    [DllImport("mfplat.dll", ExactSpelling = true)] internal static extern int MFCreateMediaType(out nint type);
    [DllImport("mf.dll", ExactSpelling = true)] internal static extern int MFEnumDeviceSources(nint attributes, out nint devices, out uint count);
    [DllImport("mf.dll", ExactSpelling = true)] internal static extern int MFCreateDeviceSource(nint attributes, out nint source);
    [DllImport("mfreadwrite.dll", ExactSpelling = true)] internal static extern int MFCreateSourceReaderFromMediaSource(nint source, nint attributes, out nint reader);
}
