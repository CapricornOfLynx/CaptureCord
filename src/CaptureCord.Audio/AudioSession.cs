// SPDX-License-Identifier: GPL-3.0-or-later
using CaptureCord.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CaptureCord.Audio;

public sealed class AudioSession : IDisposable
{
    private readonly object gate = new();
    private MMDevice? input;
    private MMDevice? output;
    private WasapiCapture? capture;
    private WasapiOut? playback;
    private BufferedWaveProvider? buffer;
    private VolumeSampleProvider? volume;
    private bool stopping;
    private long resets;
    public event Action<Exception>? Failed;
    public long BufferResets => Interlocked.Read(ref resets);
    public double BufferedMilliseconds { get { lock (gate) return buffer?.BufferedDuration.TotalMilliseconds ?? 0; } }
    public string Format { get; private set; } = "Stopped";

    public static IReadOnlyList<DeviceInfo> Enumerate(bool inputs)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(inputs ? DataFlow.Capture : DataFlow.Render, DeviceState.Active);
        var result = new List<DeviceInfo>();
        foreach (var device in devices)
        {
            using (device) result.Add(new(device.ID, device.FriendlyName));
        }
        return result;
    }

    public void Start(string inputId, string outputId, float level)
    {
        if (capture is not null) throw new InvalidOperationException("Audio is already running.");
        try
        {
            stopping = false;
            using var enumerator = new MMDeviceEnumerator();
            input = enumerator.GetDevice(inputId);
            output = enumerator.GetDevice(outputId);
            capture = new WasapiCapture(input, false, 20);
            buffer = new BufferedWaveProvider(capture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(120),
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };
            volume = new VolumeSampleProvider(buffer.ToSampleProvider()) { Volume = Math.Clamp(level, 0, 1) };
            // Shared-mode WASAPI; NAudio negotiates the endpoint format and uses the
            // Windows DMO resampler only when the format requires it.
            playback = new WasapiOut(output, AudioClientShareMode.Shared, true, 30);
            playback.Init(volume.ToWaveProvider());
            capture.DataAvailable += OnData;
            capture.RecordingStopped += OnCaptureStopped;
            playback.PlaybackStopped += OnPlaybackStopped;
            Format = $"{capture.WaveFormat.SampleRate} Hz · {capture.WaveFormat.Channels} ch → WASAPI shared";
            playback.Play();
            capture.StartRecording();
        }
        catch { Dispose(); throw; }
    }
    private void OnData(object? sender, WaveInEventArgs e)
    {
        lock (gate)
        {
            if (stopping || buffer is null) return;
            if (buffer.BufferedDuration.TotalMilliseconds > 80 || e.BytesRecorded > buffer.BufferLength - buffer.BufferedBytes)
            {
                buffer.ClearBuffer();
                Interlocked.Increment(ref resets);
            }
            buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }
    }
    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (!stopping) Failed?.Invoke(e.Exception ?? new IOException("Audio input stopped. Check the device connection."));
    }
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!stopping) Failed?.Invoke(e.Exception ?? new IOException("Audio output stopped. Check the device connection."));
    }
    public void SetVolume(float level) { if (volume is not null) volume.Volume = Math.Clamp(level, 0, 1); }
    public void Dispose()
    {
        stopping = true;
        if (capture is not null)
        {
            capture.DataAvailable -= OnData;
            capture.RecordingStopped -= OnCaptureStopped;
            capture.Dispose();
            capture = null;
        }
        if (playback is not null)
        {
            playback.PlaybackStopped -= OnPlaybackStopped;
            playback.Dispose();
            playback = null;
        }
        lock (gate) { buffer = null; volume = null; }
        input?.Dispose(); input = null;
        output?.Dispose(); output = null;
        Format = "Stopped";
    }
}
