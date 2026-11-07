// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CaptureCord.Audio;
using CaptureCord.Capture;
using CaptureCord.Core;

namespace CaptureCord.App;

public partial class MainWindow : Window
{
    private readonly Settings settings = SettingsStore.Load();
    private readonly bool smokeTest;
    private readonly SemaphoreSlim sessionGate = new(1, 1);
    private MediaFoundation? foundation;
    private VideoSession? video;
    private AudioSession? audio;
    private WriteableBitmap? bitmap;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool initialized, refreshing, running, closing, closed, fullscreen, borderless;
    private bool recovering;
    private int modeVersion;
    private int pendingModes;
    private readonly SemaphoreSlim discoveryGate = new(1, 1);
    private Rect restoreBounds;
    private WindowState restoreState;
    private long rendered, previousRendered, previousCaptured;
    private double deliveryMs;
    private long lastStats = Stopwatch.GetTimestamp();

    public MainWindow() : this(false) { }
    internal MainWindow(bool smokeTest)
    {
        this.smokeTest = smokeTest;
        InitializeComponent();
        Width = Math.Clamp(settings.Width, 760, SystemParameters.VirtualScreenWidth);
        Height = Math.Clamp(settings.Height, 540, SystemParameters.VirtualScreenHeight);
        if (settings.Left is double x && settings.Top is double y && double.IsFinite(x) && double.IsFinite(y) &&
            x >= SystemParameters.VirtualScreenLeft && x < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100 &&
            y >= SystemParameters.VirtualScreenTop && y < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100)
        { Left = x; Top = y; }
        Volume.Value = double.IsFinite(settings.Volume) ? Math.Clamp(settings.Volume, 0, 1) : 0.8;
        Mute.IsChecked = settings.Muted;
        timer.Tick += UpdateDiagnostics;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            WindowAppearance.Apply(this);
            foundation = new();
            if (smokeTest)
            {
                await Task.Run(VideoDevices.Enumerate);
                if (VideoSelector is null || InputSelector is null || OutputSelector is null || Preview is null)
                    throw new InvalidOperationException("WPF controls were not initialized.");
                await UiSmokeCheck.VerifyAsync(this, [VideoSelector, InputSelector, OutputSelector, ModeSelector]);
                Close();
                return;
            }
            initialized = true;
            CompositionTarget.Rendering += Render;
            timer.Start();
            await RefreshAsync();
            if (settings.Fullscreen) ToggleFullscreen(false);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            if (smokeTest) { Environment.ExitCode = 1; Close(); }
        }
    }
    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        try { await RefreshAsync(); } catch (Exception ex) { ShowError(ex); }
    }
    private async Task RefreshAsync()
    {
        if (running || refreshing || closing) return;
        refreshing = true;
        RefreshButton.IsEnabled = false;
        StartButton.IsEnabled = false;
        Selectors.IsEnabled = false;
        try
        {
            var videoId = (VideoSelector.SelectedItem as DeviceInfo)?.Id ?? settings.VideoId;
            var inputId = (InputSelector.SelectedItem as DeviceInfo)?.Id ?? settings.InputId;
            var outputId = (OutputSelector.SelectedItem as DeviceInfo)?.Id ?? settings.OutputId;
            var devices = await Task.Run(() => (VideoDevices.Enumerate(), AudioSession.Enumerate(true), AudioSession.Enumerate(false)));
            VideoSelector.ItemsSource = devices.Item1;
            InputSelector.ItemsSource = devices.Item2;
            OutputSelector.ItemsSource = devices.Item3;
            // Never select an arbitrary microphone as an HDMI input.
            SelectDevice(VideoSelector, devices.Item1, videoId, true);
            SelectDevice(InputSelector, devices.Item2, inputId, false);
            SelectDevice(OutputSelector, devices.Item3, outputId, true);
            await LoadModesAsync();
            Status.Text = devices.Item1.Count == 0 ? "No video devices found. Connect a UVC capture card and refresh." : "Select an audio input and output, then start capture.";
        }
        finally
        {
            refreshing = false;
            RefreshButton.IsEnabled = true;
            StartButton.IsEnabled = pendingModes == 0;
            Selectors.IsEnabled = true;
        }
    }
    private static void SelectDevice(ComboBox selector, IReadOnlyList<DeviceInfo> devices, string? id, bool fallback)
        => selector.SelectedItem = devices.FirstOrDefault(d => d.Id == id) ?? (fallback ? devices.FirstOrDefault() : null);
    private async void OnVideoChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || refreshing || running || closing) return;
        try { await LoadModesAsync(); } catch (Exception ex) { ShowError(ex); }
    }
    private async Task LoadModesAsync()
    {
        var version = ++modeVersion;
        ModeSelector.ItemsSource = null;
        if (VideoSelector.SelectedItem is not DeviceInfo device) return;
        pendingModes++;
        StartButton.IsEnabled = false;
        await discoveryGate.WaitAsync();
        try
        {
            var modes = await Task.Run(() => VideoDevices.Modes(device.Id));
            if (version != modeVersion || closing) return;
            ModeSelector.ItemsSource = modes;
            ModeSelector.SelectedItem = modes.FirstOrDefault(m => m.ToString() == settings.Mode) ?? ModeSelection.Preferred(modes);
        }
        finally
        {
            discoveryGate.Release();
            pendingModes--;
            StartButton.IsEnabled = pendingModes == 0 && !refreshing;
        }
    }
    private async void OnStartStop(object sender, RoutedEventArgs e)
    {
        await sessionGate.WaitAsync();
        StartButton.IsEnabled = false;
        try { if (running) await StopAsync(); else await StartAsync(); }
        catch (Exception ex) { await StopAsync(); ShowError(ex); }
        finally { StartButton.IsEnabled = true; sessionGate.Release(); }
    }
    private async Task StartAsync()
    {
        if (closing) return;
        if (VideoSelector.SelectedItem is not DeviceInfo vd || ModeSelector.SelectedItem is not VideoMode mode ||
            InputSelector.SelectedItem is not DeviceInfo ai || OutputSelector.SelectedItem is not DeviceInfo ao)
            throw new InvalidOperationException("Select a video device, supported mode, audio input and audio output.");
        Selectors.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        SaveSelections();
        video = new();
        audio = new();
        video.Failed += SessionFailed;
        audio.Failed += SessionFailed;
        Status.Text = "Opening capture devices…";
        await video.StartAsync(vd.Id, mode);
        var level = Mute.IsChecked == true ? 0 : (float)Volume.Value;
        await Task.Run(() => audio.Start(ai.Id, ao.Id, level));
        rendered = previousRendered = previousCaptured = 0;
        lastStats = Stopwatch.GetTimestamp();
        running = true;
        StatusDot.Fill = (Brush)FindResource("Accent");
        StartButton.Content = "Stop capture";
        Placeholder.Visibility = Visibility.Collapsed;
        Status.Text = $"Live · {vd.Name} · {mode} · Audio: {ai.Name} → {ao.Name}";
        Log.Write("session.started");
    }
    private async Task StopAsync()
    {
        running = false;
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(130, 152, 175));
        if (video is not null)
        {
            video.Failed -= SessionFailed;
            await video.StopAsync();
            video = null;
        }
        if (audio is not null)
        {
            audio.Failed -= SessionFailed;
            var oldAudio = audio;
            audio = null;
            await Task.Run(oldAudio.Dispose);
        }
        Preview.Source = null;
        bitmap = null;
        Placeholder.Visibility = Visibility.Visible;
        Selectors.IsEnabled = true;
        RefreshButton.IsEnabled = true;
        StartButton.Content = "Start capture";
        Status.Text = "Stopped · Devices released";
        Diagnostics.Text = "Reconnect a missing device, refresh, then start again.";
        Log.Write("session.stopped");
    }
    private void SessionFailed(Exception error) => Dispatcher.BeginInvoke(async () =>
    {
        if (recovering || closing) return;
        recovering = true;
        await sessionGate.WaitAsync();
        try { await StopAsync(); ShowError(error); }
        finally { sessionGate.Release(); recovering = false; }
    });
    private void Render(object? sender, EventArgs e)
    {
        if (!running || video?.Frames.Take() is not VideoFrame frame) return;
        if (bitmap is null || bitmap.PixelWidth != frame.Width || bitmap.PixelHeight != frame.Height)
        {
            bitmap = new(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32, null);
            Preview.Source = bitmap;
        }
        bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Pixels, frame.Width * 4, 0);
        deliveryMs = Stopwatch.GetElapsedTime(frame.ArrivalTicks).TotalMilliseconds;
        rendered++;
    }
    private void UpdateDiagnostics(object? sender, EventArgs e)
    {
        if (!running || video is null) return;
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(lastStats, now).TotalSeconds;
        var count = video.Captured;
        Diagnostics.Text = $"Capture {(count - previousCaptured) / elapsed:0.0} FPS · Render {(rendered - previousRendered) / elapsed:0.0} FPS · Dropped {video.Frames.Dropped} · CPU RGB32 · Delivery {deliveryMs:0.0} ms (not end-to-end) · Audio {audio?.BufferedMilliseconds:0} ms queued · Resets {audio?.BufferResets} · {audio?.Format}";
        previousCaptured = count;
        previousRendered = rendered;
        lastStats = now;
    }
    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => audio?.SetVolume(Mute?.IsChecked == true ? 0 : (float)e.NewValue);
    private void OnMuteChanged(object sender, RoutedEventArgs e) => audio?.SetVolume(Mute.IsChecked == true ? 0 : (float)Volume.Value);
    private void OnFullscreen(object sender, RoutedEventArgs e) => ToggleFullscreen(false);
    private void OnBorderless(object sender, RoutedEventArgs e) => ToggleFullscreen(true);
    private void OnPreviewClick(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) ToggleFullscreen(false); }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) ToggleFullscreen(false);
        if (e.Key == Key.F10) ToggleFullscreen(true);
        if (e.Key == Key.Escape && fullscreen) ToggleFullscreen(borderless);
    }
    private void ToggleFullscreen(bool keepControls)
    {
        if (fullscreen)
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            Left = restoreBounds.Left; Top = restoreBounds.Top; Width = restoreBounds.Width; Height = restoreBounds.Height;
            WindowState = restoreState;
            Header.Visibility = Controls.Visibility = Visibility.Visible;
            fullscreen = false;
        }
        else
        {
            restoreState = WindowState;
            restoreBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Header.Visibility = Controls.Visibility = keepControls ? Visibility.Visible : Visibility.Collapsed;
            borderless = keepControls;
            fullscreen = true;
        }
    }
    private void SaveSelections()
    {
        settings.VideoId = (VideoSelector.SelectedItem as DeviceInfo)?.Id;
        settings.InputId = (InputSelector.SelectedItem as DeviceInfo)?.Id;
        settings.OutputId = (OutputSelector.SelectedItem as DeviceInfo)?.Id;
        settings.Mode = ModeSelector.SelectedItem?.ToString();
        settings.Volume = Volume.Value;
        settings.Muted = Mute.IsChecked == true;
    }
    private void ShowError(Exception error)
    {
        Log.Write("ui.error", error);
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 165, 142));
        Status.Text = $"{error.Message} (0x{error.HResult:X8})";
        Diagnostics.Text = "Check USB/HDMI connections and Windows camera/microphone privacy permissions. Close other capture apps. Refresh and retry.";
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closed) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        IsEnabled = false;
        timer.Stop();
        CompositionTarget.Rendering -= Render;
        await sessionGate.WaitAsync();
        try
        {
            SaveSelections();
            settings.Fullscreen = fullscreen;
            var bounds = fullscreen ? restoreBounds : WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            settings.Width = bounds.Width; settings.Height = bounds.Height; settings.Left = bounds.Left; settings.Top = bounds.Top;
            await StopAsync();
            // Wait for device enumeration before shutting down Media Foundation.
            while (refreshing || pendingModes > 0) await Task.Delay(25);
            if (!smokeTest) SettingsStore.Save(settings);
        }
        catch (Exception ex) { Log.Write("app.close", ex); }
        finally
        {
            foundation?.Dispose();
            sessionGate.Release();
            closed = true;
            // Defer the second close until WPF has left the first Closing event.
            _ = Dispatcher.BeginInvoke(Close);
        }
    }
}
