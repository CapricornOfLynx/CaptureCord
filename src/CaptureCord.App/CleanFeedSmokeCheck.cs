// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CaptureCord.Core;

namespace CaptureCord.App;

internal static class CleanFeedSmokeCheck
{
    public static void Verify(Window controls)
    {
        var feed = new CleanFeedWindow { Width = 320, Height = 180, Opacity = 0, ShowInTaskbar = false };
        try
        {
            feed.Show(); feed.UpdateLayout();
            if (feed.Owner is not null || feed.WindowStyle != WindowStyle.None || feed.BorderThickness != new Thickness(0) || feed.Content is not Image image)
                throw new InvalidOperationException("Clean feed must be independent and contain only an image without window chrome.");
            var pixels = new byte[320 * 180 * 4];
            for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 50; pixels[i + 1] = 120; pixels[i + 2] = 230; pixels[i + 3] = 255; }
            feed.Present(new VideoFrame(pixels, 320, 180, 0, 0));
            if (image.Source is not WriteableBitmap bitmap) throw new InvalidOperationException("Clean feed image was not presented.");
            var actual = new byte[pixels.Length];
            bitmap.CopyPixels(actual, 320 * 4, 0);
            if (!actual.SequenceEqual(pixels)) throw new InvalidOperationException("Clean feed image differs from the captured frame.");
            var state = controls.WindowState;
            controls.WindowState = WindowState.Minimized;
            if (feed.WindowState != WindowState.Normal) throw new InvalidOperationException("Minimizing controls must not minimize the clean feed.");
            controls.WindowState = state;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(AppContext.BaseDirectory, "ui-smoke", "clean-feed.png"))) encoder.Save(file);
            feed.Clear();
            if (image.Source is not null) throw new InvalidOperationException("Stopping capture must clear the clean feed.");
        }
        finally { feed.Close(); }
    }
}
