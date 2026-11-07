// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using CaptureCord.Capture;
using CaptureCord.Core;

namespace CaptureCord.App;

public partial class PermissionsWindow : Window
{
    private bool requesting;
    public PermissionsWindow() => InitializeComponent();
    private void OnLoaded(object sender, RoutedEventArgs e) { WindowAppearance.Apply(this); Check(); }
    private void OnActivated(object? sender, EventArgs e) { if (IsLoaded && !requesting) Check(); }
    private void Check()
    {
        CameraStatus.Text = DevicePermissions.Check(true).Description;
        AudioStatus.Text = DevicePermissions.Check(false).Description;
    }
    private async Task GrantAsync(bool camera)
    {
        if (requesting) return;
        requesting = true;
        CameraGrant.IsEnabled = AudioGrant.IsEnabled = false;
        Feedback.Text = "Confirm access in the Windows dialog or Settings, then return here.";
        try { await DevicePermissions.RequestAsync(camera); Check(); }
        catch (Exception e) { Log.Write("permissions.ui.request", e); Feedback.Text = $"Could not request access: {e.Message}. Open Windows Settings → Privacy & security → {(camera ? "Camera" : "Microphone")}."; }
        finally { requesting = false; CameraGrant.IsEnabled = AudioGrant.IsEnabled = true; }
    }
    private async void OnCameraGrant(object sender, RoutedEventArgs e) => await GrantAsync(true);
    private async void OnAudioGrant(object sender, RoutedEventArgs e) => await GrantAsync(false);
    private void OnCheck(object sender, RoutedEventArgs e) => Check();
    private void OnDone(object sender, RoutedEventArgs e) => Close();
}
