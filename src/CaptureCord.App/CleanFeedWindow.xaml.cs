// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CaptureCord.Core;

namespace CaptureCord.App;

/// <summary>Independent top-level sharing target. No controls, labels or diagnostic
/// overlays, and no separate capture/audio session. Closing it never stops capture.</summary>
public partial class CleanFeedWindow : Window
{
    private WriteableBitmap? bitmap;
    private Rect normalBounds;
    private bool fullscreen;
    public CleanFeedWindow() => InitializeComponent();
    public void Present(VideoFrame frame)
    {
        if (bitmap is null || bitmap.PixelWidth != frame.Width || bitmap.PixelHeight != frame.Height)
        {
            bitmap = new(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32, null);
            Feed.Source = bitmap;
        }
        bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Pixels, frame.Width * 4, 0);
    }
    public void Clear() { Feed.Source = null; bitmap = null; }
    private void OnLoaded(object sender, RoutedEventArgs e) => WindowAppearance.ApplyCleanFeed(this);
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleFullscreen();
        else if (!fullscreen && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (e.Key == Key.Escape && fullscreen)) { ToggleFullscreen(); e.Handled = true; }
    }
    private void ToggleFullscreen()
    {
        if (fullscreen)
        {
            WindowState = WindowState.Normal;
            Left = normalBounds.Left; Top = normalBounds.Top; Width = normalBounds.Width; Height = normalBounds.Height;
            ResizeMode = ResizeMode.CanResize;
        }
        else
        {
            normalBounds = new(Left, Top, ActualWidth, ActualHeight);
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        fullscreen = !fullscreen;
    }
    protected override void OnClosed(EventArgs e) { Clear(); base.OnClosed(e); }
}
