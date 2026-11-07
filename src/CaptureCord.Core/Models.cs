// SPDX-License-Identifier: GPL-3.0-or-later
namespace CaptureCord.Core;

public sealed record DeviceInfo(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record VideoMode(int NativeIndex, int Width, int Height, uint Numerator, uint Denominator, Guid Subtype)
{
    public double Fps => Denominator == 0 ? 0 : (double)Numerator / Denominator;
    public string PixelFormat
    {
        get
        {
            var bytes = Subtype.ToByteArray();
            return bytes.Take(4).All(b => b >= 32 && b < 127)
                ? System.Text.Encoding.ASCII.GetString(bytes, 0, 4) : Subtype.ToString("D");
        }
    }
    public override string ToString() => $"{Width} × {Height} · {Fps:0.##} FPS · {PixelFormat}";
}

public static class ModeSelection
{
    public static VideoMode? Preferred(IEnumerable<VideoMode> modes) => modes
        .OrderBy(m => Math.Abs(m.Width - 1920) + Math.Abs(m.Height - 1080))
        .ThenBy(m => Math.Abs(m.Fps - 60)).ThenBy(m => m.NativeIndex).FirstOrDefault();
}

public sealed record VideoFrame(byte[] Pixels, int Width, int Height, long Timestamp, long ArrivalTicks);

/// <summary>A single-frame mailbox. Slow consumers never create a queue of stale video.</summary>
public sealed class LatestFrame
{
    private VideoFrame? frame;
    private long dropped;
    public long Dropped => Interlocked.Read(ref dropped);
    public void Publish(VideoFrame value)
    {
        if (Interlocked.Exchange(ref frame, value) is not null) Interlocked.Increment(ref dropped);
    }
    public VideoFrame? Take() => Interlocked.Exchange(ref frame, null);
}
