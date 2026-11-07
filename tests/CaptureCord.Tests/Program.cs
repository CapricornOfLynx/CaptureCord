// SPDX-License-Identifier: GPL-3.0-or-later
using CaptureCord.Core;
using CaptureCord.Capture;
using CaptureCord.Audio;

var tests = new (string Name, Action Run)[]
{
    ("Permission policy distinguishes denial from unknown desktop status", () =>
    {
        Check(new DevicePermission("camera", PermissionStatus.DeniedBySystem).IsDenied);
        Check(new DevicePermission("audio", PermissionStatus.DeniedByUser).IsDenied);
        Check(!new DevicePermission("camera", PermissionStatus.Unknown).IsDenied);
        Check(!new DevicePermission("camera", PermissionStatus.NotDeclaredByApp).IsDenied);
        Check(!new DevicePermission("camera", PermissionStatus.Allowed).IsDenied);
        Check(new DevicePermission("camera", PermissionStatus.Unknown).Description.Contains("cannot report"));
    }),
    ("Native permission checks never activate capture or require a prompt", () =>
    {
        var camera = DevicePermissions.Check(true);
        var audio = DevicePermissions.Check(false);
        Check(Enum.IsDefined(camera.Status) && Enum.IsDefined(audio.Status));
        Check(!string.IsNullOrWhiteSpace(camera.Description) && !string.IsNullOrWhiteSpace(audio.Description));
    }),
    ("Native MF ABI, padded/negative-stride copy and invalid-buffer rejection", NativeChecks.Buffers),
    ("Prefer 1080p60 without depending on pixel format", () =>
    {
        var modes = new[] { Mode(0, 1280, 720, 60), Mode(1, 1920, 1080, 30), Mode(2, 1920, 1080, 60) };
        Equal(2, ModeSelection.Preferred(modes)!.NativeIndex);
    }),
    ("Fractional rates remain fractional", () =>
    {
        var mode = new VideoMode(0, 1920, 1080, 60000, 1001, Guid.Empty);
        Check(Math.Abs(mode.Fps - 59.94005994) < 0.000001);
    }),
    ("Empty capability list is safe", () => Check(ModeSelection.Preferred([]) is null)),
    ("Missing frame rate denominator is safe", () => Equal(0d, new VideoMode(0, 1, 1, 60, 0, Guid.Empty).Fps)),
    ("Native pixel format is displayed as FourCC", () => Equal("YUY2", new VideoMode(0, 1, 1, 1, 1, new Guid("32595559-0000-0010-8000-00aa00389b71")).PixelFormat)),
    ("Slow renderer keeps newest frame and counts drops", () =>
    {
        var mailbox = new LatestFrame();
        mailbox.Publish(Frame(1)); mailbox.Publish(Frame(2)); mailbox.Publish(Frame(3));
        Equal(2L, mailbox.Dropped); Equal(3L, mailbox.Take()!.Timestamp); Check(mailbox.Take() is null);
    }),
    ("Concurrent mailbox delivery is monotonic and bounded", () =>
    {
        var mailbox = new LatestFrame();
        var producer = Task.Run(() => { for (int i = 1; i <= 10000; i++) mailbox.Publish(Frame(i)); });
        long previous = 0, received = 0;
        while (!producer.IsCompleted)
        {
            var frame = mailbox.Take();
            if (frame is null) continue;
            Check(frame.Timestamp > previous); previous = frame.Timestamp; received++;
        }
        producer.GetAwaiter().GetResult();
        if (mailbox.Take() is VideoFrame last) { Check(last.Timestamp > previous); received++; }
        Equal(10000L, received + mailbox.Dropped);
    }),
    ("Settings round-trip and corrupt-file recovery", () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "CaptureCord-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "settings.json");
        try
        {
            Equal(0.8, SettingsStore.Load(file).Volume);
            SettingsStore.Save(new Settings { VideoId = "test-device", Volume = 0.25, Muted = true }, file);
            var loaded = SettingsStore.Load(file);
            Equal("test-device", loaded.VideoId!); Equal(0.25, loaded.Volume); Check(loaded.Muted);
            File.WriteAllText(file, "{broken");
            Equal(0.8, SettingsStore.Load(file).Volume);
        }
        finally { File.Delete(file); Directory.Delete(directory); }
    })
};
int failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception e) { failed++; Console.WriteLine("FAIL " + test.Name + ": " + e.Message); }
}
if (args.Contains("--probe"))
{
    try
    {
        using var mf = new MediaFoundation();
        var devices = await Task.Run(VideoDevices.Enumerate);
        Console.WriteLine($"PROBE Media Foundation: {devices.Count} video device(s)");
        Console.WriteLine($"PROBE WASAPI: {AudioSession.Enumerate(true).Count} capture, {AudioSession.Enumerate(false).Count} playback endpoint(s)");
        // Enumeration only: never activate a camera or capture a microphone in a smoke test.
    }
    catch (Exception e) { failed++; Console.WriteLine($"FAIL Native probe: {e.GetType().Name} 0x{e.HResult:X8}: {e.Message}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} unit checks passed; failures (including probe): {failed}");
return failed == 0 ? 0 : 1;

static VideoMode Mode(int index, int width, int height, uint rate) => new(index, width, height, rate, 1, Guid.Empty);
static VideoFrame Frame(long timestamp) => new([], 1, 1, timestamp, 0);
static void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
static void Equal<T>(T expected, T actual) where T : IEquatable<T> { if (!expected.Equals(actual)) throw new Exception($"Expected {expected}, got {actual}"); }
