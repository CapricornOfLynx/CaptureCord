// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using CaptureCord.Core;

namespace CaptureCord.App;

/// <summary>Hardware-only D3D11 upload and DXGI presentation. This deliberately does
/// not label WARP software rendering or CPU RGB conversion as GPU decoding.</summary>
internal sealed unsafe class D3DRenderer : IDisposable
{
    private Resource? device, context, swapChain, backBuffer;
    public string AdapterName { get; private set; } = "D3D11 hardware";
    public int Width { get; }
    public int Height { get; }
    public D3DRenderer(nint window, int width, int height)
    {
        Width = width; Height = height;
        var description = new SwapChainDescription
        {
            Mode = new() { Width = (uint)width, Height = (uint)height, Format = 87 }, // BGRA8
            Sample = new() { Count = 1 }, Usage = 0x20, BufferCount = 2,
            OutputWindow = window, Windowed = 1, SwapEffect = 0 // windowed blt stretch
        };
        nint nativeSwap = 0, nativeDevice = 0, nativeContext = 0;
        uint level;
        var hr = D3D11CreateDeviceAndSwapChain(0, 1, 0, 0x20, 0, 0, 7,
            ref description, out nativeSwap, out nativeDevice, out level, out nativeContext);
        // Own partial results as well, before checking the HRESULT.
        swapChain = new(nativeSwap); device = new(nativeDevice); context = new(nativeContext);
        try
        {
            Check(hr);
            var iid = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c"); // ID3D11Texture2D
            nint texture;
            Check(((delegate* unmanaged[Stdcall]<nint, uint, Guid*, nint*, int>)swapChain.Slot(9))(nativeSwap, 0, &iid, &texture));
            backBuffer = new(texture);
            AdapterName = ReadAdapter(device);
            // Keep native fullscreen under WPF's control; DXGI must not steal Alt+Enter.
            var factoryIid = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
            nint factoryPointer;
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)swapChain.Slot(6))(nativeSwap, &factoryIid, &factoryPointer));
            using var factory = new Resource(factoryPointer);
            Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, int>)factory.Slot(8))(factoryPointer, window, 2));
        }
        catch { Dispose(); throw; }
    }
    public bool Present(VideoFrame frame)
    {
        Upload(frame);
        var hr = ((delegate* unmanaged[Stdcall]<nint, uint, uint, int>)swapChain!.Slot(8))(swapChain.Pointer, 0, 8); // DO_NOT_WAIT
        if (hr == unchecked((int)0x887a000a) || hr == 0x087a0001) return false; // busy / occluded
        Check(hr); // device removal/reset falls back to WPF in the owner
        return true;
    }
    private void Upload(VideoFrame frame)
    {
        if (frame.Width != Width || frame.Height != Height || frame.Pixels.Length < checked(Width * Height * 4))
            throw new ArgumentException("Frame dimensions do not match the GPU surface.");
        fixed (byte* data = frame.Pixels)
            ((delegate* unmanaged[Stdcall]<nint, nint, uint, nint, void*, uint, uint, void>)context!.Slot(48))
                (context.Pointer, backBuffer!.Pointer, 0, 0, data, (uint)(Width * 4), 0);
    }
    // Synthetic verification only; readback is intentionally absent from live playback.
    internal byte[] VerifyUpload(VideoFrame frame)
    {
        Upload(frame);
        var desc = new TextureDescription { Width = (uint)Width, Height = (uint)Height, MipLevels = 1, ArraySize = 1, Format = 87, Sample = new() { Count = 1 }, Usage = 3, CpuAccess = 0x20000 };
        nint pointer;
        Check(((delegate* unmanaged[Stdcall]<nint, TextureDescription*, nint, nint*, int>)device!.Slot(5))(device.Pointer, &desc, 0, &pointer));
        using var staging = new Resource(pointer);
        ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)context!.Slot(47))(context.Pointer, pointer, backBuffer!.Pointer);
        MappedResource mapped;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, MappedResource*, int>)context.Slot(14))(context.Pointer, pointer, 0, 1, 0, &mapped));
        try
        {
            var result = new byte[checked(Width * Height * 4)];
            for (int y = 0; y < Height; y++) Marshal.Copy(mapped.Data + checked((int)(y * mapped.RowPitch)), result, y * Width * 4, Width * 4);
            return result;
        }
        finally { ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)context.Slot(15))(context.Pointer, pointer, 0); }
    }
    private static string ReadAdapter(Resource device)
    {
        var iid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        nint dxgiPointer;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)device.Slot(0))(device.Pointer, &iid, &dxgiPointer));
        using var dxgi = new Resource(dxgiPointer);
        nint adapterPointer;
        Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)dxgi.Slot(7))(dxgiPointer, &adapterPointer));
        using var adapter = new Resource(adapterPointer);
        AdapterDescription desc;
        Check(((delegate* unmanaged[Stdcall]<nint, AdapterDescription*, int>)adapter.Slot(8))(adapterPointer, &desc));
        return new string(desc.Description);
    }
    public void Dispose()
    {
        backBuffer?.Dispose(); backBuffer = null;
        swapChain?.Dispose(); swapChain = null;
        context?.Dispose(); context = null;
        device?.Dispose(); device = null;
    }
    private static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
    private sealed class Resource(nint pointer) : IDisposable
    {
        public nint Pointer { get; private set; } = pointer;
        public void* Slot(int slot) => (void*)(*(nint**)Pointer)[slot];
        public void Dispose() { if (Pointer != 0) { ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(2))(Pointer); Pointer = 0; } }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rational { public uint Numerator, Denominator; }
    [StructLayout(LayoutKind.Sequential)] private struct ModeDescription { public uint Width, Height; public Rational RefreshRate; public uint Format, ScanlineOrdering, Scaling; }
    [StructLayout(LayoutKind.Sequential)] private struct SampleDescription { public uint Count, Quality; }
    [StructLayout(LayoutKind.Sequential)] private struct SwapChainDescription { public ModeDescription Mode; public SampleDescription Sample; public uint Usage, BufferCount; public nint OutputWindow; public int Windowed; public uint SwapEffect, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct TextureDescription { public uint Width, Height, MipLevels, ArraySize, Format; public SampleDescription Sample; public uint Usage, BindFlags, CpuAccess, MiscFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct MappedResource { public nint Data; public uint RowPitch, DepthPitch; }
    [StructLayout(LayoutKind.Sequential)] private struct AdapterDescription { public fixed char Description[128]; public uint VendorId, DeviceId, SubsystemId, Revision; public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory; public long Luid; }
    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDeviceAndSwapChain(nint adapter, uint driverType, nint software, uint flags, nint levels, uint levelCount, uint sdkVersion, ref SwapChainDescription description, out nint swapChain, out nint device, out uint featureLevel, out nint context);
}
