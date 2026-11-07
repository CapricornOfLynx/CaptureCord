// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CaptureCord.Core;

namespace CaptureCord.App;

internal static class GpuSmokeCheck
{
    public static async Task VerifyAsync(Window window, D3DPreview preview)
    {
        string result;
        preview.Width = 320; preview.Height = 180;
        preview.Visibility = Visibility.Visible;
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        try
        {
            // Exercise real hardware uploads, readback, presentation and surface recreation.
            foreach (var size in new[] { (320, 180), (256, 144), (320, 180) })
            {
                var pixels = new byte[size.Item1 * size.Item2 * 4];
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = (byte)((i / 4) % 256); pixels[i + 1] = 120; pixels[i + 2] = 230; pixels[i + 3] = 255;
                }
                preview.VerifyUpload(new VideoFrame(pixels, size.Item1, size.Item2, 0, 0));
            }
            result = $"PASS D3D11 hardware GPU upload/readback, presentation and mode recreation: {preview.AdapterName}";
        }
        catch (COMException error) when (!Environment.GetCommandLineArgs().Contains("--require-gpu"))
        {
            // Hosted CI can have no hardware adapter; never substitute WARP and call it hardware.
            result = $"SKIP D3D11 hardware unavailable (0x{error.HResult:X8}); WPF fallback remains available.";
        }
        finally { preview.Reset(); preview.Visibility = Visibility.Collapsed; }
        var output = Path.Combine(AppContext.BaseDirectory, "ui-smoke");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "gpu-result.txt"), result);
    }
}
