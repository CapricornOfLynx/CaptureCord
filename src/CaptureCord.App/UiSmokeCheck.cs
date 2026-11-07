// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CaptureCord.Core;

namespace CaptureCord.App;

/// <summary>Real-template regression checks for the original unreadable dropdowns.</summary>
internal static class UiSmokeCheck
{
    public static async Task VerifyAsync(Window window, ComboBox[] selectors)
    {
        var output = Path.Combine(AppContext.BaseDirectory, "ui-smoke");
        Directory.CreateDirectory(output);
        window.Width = 1180;
        window.Height = 780;
        foreach (var selector in selectors)
        {
            selector.ItemsSource = new[]
            {
                new DeviceInfo("synthetic-1", "HDMI capture device · 1920 × 1080 · 60 FPS"),
                new DeviceInfo("synthetic-2", "Second device with a longer name for text overflow checking"),
                new DeviceInfo("synthetic-3", "USB capture device")
            };
            selector.SelectedIndex = 0;
            selector.ApplyTemplate();
            window.UpdateLayout();
            RequireContrast(selector.Foreground, selector.Background);
            var popup = selector.Template.FindName("PART_Popup", selector) as Popup
                ?? throw new InvalidOperationException("Dropdown popup template is missing.");
            // Keep test popups off-screen; they must not flash over the user's desktop.
            popup.HorizontalOffset = -30000;
            try
            {
                selector.IsDropDownOpen = true;
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                window.UpdateLayout();
                if (popup.Child is not Border surface) throw new InvalidOperationException("Dropdown surface is missing.");
                surface.UpdateLayout();
                if (selector.ItemContainerGenerator.ContainerFromIndex(0) is not ComboBoxItem item)
                    throw new InvalidOperationException("Dropdown items were not realized.");
                RequireContrast(item.Foreground, surface.Background);
                RequireContrast(item.Foreground, item.Background);
                if (selector == selectors[0]) Save(surface, Path.Combine(output, "dropdown.png"));
                selector.SelectedIndex = 1;
                if (selector.SelectedIndex != 1) throw new InvalidOperationException("Dropdown selection failed.");
            }
            finally { selector.IsDropDownOpen = false; popup.HorizontalOffset = 0; }
        }
        window.UpdateLayout();
        Save((FrameworkElement)window.Content, Path.Combine(output, "window.png"));
        // Also exercise wrapping of the playback controls at the minimum width.
        window.Width = window.MinWidth;
        window.UpdateLayout();
        Save((FrameworkElement)window.Content, Path.Combine(output, "window-compact.png"));
    }

    private static void RequireContrast(Brush foreground, Brush background)
    {
        if (foreground is not SolidColorBrush fg || background is not SolidColorBrush bg || bg.Color.A == 0) return;
        var a = Luminance(fg.Color);
        var b = Luminance(bg.Color);
        if ((Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05) < 4.5)
            throw new InvalidOperationException("Dropdown text contrast is below 4.5:1.");
    }
    private static double Luminance(Color color)
    {
        static double Linear(byte value) { var v = value / 255d; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }
    private static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
            context.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
