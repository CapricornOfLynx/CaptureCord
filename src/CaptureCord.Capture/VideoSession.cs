// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Runtime.InteropServices;
using CaptureCord.Core;

namespace CaptureCord.Capture;

public sealed class VideoSession
{
    private readonly object gate = new();
    private Com? source;
    private CancellationTokenSource? cancellation;
    private Task? worker;
    private long captured;
    public LatestFrame Frames { get; } = new();
    public long Captured => Interlocked.Read(ref captured);
    public event Action<Exception>? Failed;
    public Task StartAsync(string id, VideoMode mode)
    {
        if (worker is not null) throw new InvalidOperationException("Stop the current video session first.");
        cancellation = new();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        worker = Task.Run(() => Run(id, mode, ready, cancellation.Token));
        return ready.Task;
    }
    public async Task StopAsync()
    {
        cancellation?.Cancel();
        // Shutdown unblocks a synchronous ReadSample on unplugged/stalled hardware.
        await Task.Run(() =>
        {
            using var apartment = new ComApartment();
            lock (gate)
            {
                try { source?.ShutdownSource(); }
                catch (COMException e) { Log.Write("video.shutdown", e); }
            }
        });
        if (worker is not null) await worker;
        worker = null;
        cancellation?.Dispose();
        cancellation = null;
        Frames.Take();
    }

    private unsafe void Run(string id, VideoMode mode, TaskCompletionSource ready, CancellationToken token)
    {
        ComApartment? apartment = null;
        try
        {
            apartment = new ComApartment();
            lock (gate) source = Native.Source(id);
            using var reader = Native.Reader(source);
            Native.Check(((delegate* unmanaged[Stdcall]<nint, uint, int, int>)reader.Slot(4))(reader.Pointer, 0xfffffffe, 0));
            Native.Check(((delegate* unmanaged[Stdcall]<nint, uint, int, int>)reader.Slot(4))(reader.Pointer, Native.Stream, 1));
            nint native = 0;
            Native.Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, nint*, int>)reader.Slot(5))(reader.Pointer, Native.Stream, (uint)mode.NativeIndex, &native));
            using (var nativeType = new Com(native)) SetType(reader, nativeType);
            Native.Check(Native.MFCreateMediaType(out var rgb));
            using (var rgbType = new Com(rgb))
            {
                rgbType.SetGuid(Native.MajorType, Native.Video);
                rgbType.SetGuid(Native.Subtype, Native.Rgb32);
                SetType(reader, rgbType);
            }
            var (width, height, stride) = ReadLayout(reader);
            ready.TrySetResult();
            while (!token.IsCancellationRequested)
            {
                uint actual, flags;
                long timestamp;
                nint sample = 0;
                var hr = ((delegate* unmanaged[Stdcall]<nint, uint, uint, uint*, uint*, long*, nint*, int>)reader.Slot(9))
                    (reader.Pointer, Native.Stream, 0, &actual, &flags, &timestamp, &sample);
                using var sampleOwner = new Com(sample);
                if (token.IsCancellationRequested) break;
                Native.Check(hr);
                if ((flags & 3) != 0) throw new IOException("Video device stopped delivering frames. Check its connection, then retry.");
                if ((flags & 0x20) != 0) (width, height, stride) = ReadLayout(reader);
                if (sample == 0) continue;
                nint buffer = 0;
                Native.Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)sampleOwner.Slot(41))(sample, &buffer));
                using var bufferOwner = new Com(buffer);
                var pixels = NativeBuffer.CopyRgb32(bufferOwner, width, height, stride);
                Frames.Publish(new(pixels, width, height, timestamp, Stopwatch.GetTimestamp()));
                Interlocked.Increment(ref captured);
            }
        }
        catch (Exception e)
        {
            ready.TrySetException(e);
            if (!token.IsCancellationRequested) { Log.Write("video.capture", e); Failed?.Invoke(e); }
        }
        finally
        {
            lock (gate)
            {
                if (source is not null)
                {
                    try { source.ShutdownSource(); } catch (COMException e) { Log.Write("video.cleanup", e); }
                    source.Dispose();
                    source = null;
                }
            }
            apartment?.Dispose();
        }
    }
    private static unsafe void SetType(Com reader, Com type) => Native.Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)reader.Slot(7))(reader.Pointer, Native.Stream, 0, type.Pointer));
    private static unsafe (int Width, int Height, int Stride) ReadLayout(Com reader)
    {
        nint p = 0;
        Native.Check(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)reader.Slot(6))(reader.Pointer, Native.Stream, &p));
        using var type = new Com(p);
        if (type.GetGuid(Native.Subtype) != Native.Rgb32) throw new NotSupportedException("Media Foundation could not negotiate RGB32 output for this mode.");
        var size = type.GetULong(Native.FrameSize);
        var width = checked((int)(size >> 32));
        var height = checked((int)(size & uint.MaxValue));
        if (width < 1 || height < 1 || width > 16384 || height > 16384) throw new NotSupportedException("Invalid frame dimensions.");
        // RGB DIBs default to bottom-up when no explicit stride is supplied.
        return (width, height, unchecked((int)type.GetUInt(Native.Stride, unchecked((uint)(-width * 4)))));
    }
}
