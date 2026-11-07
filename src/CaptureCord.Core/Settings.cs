// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;

namespace CaptureCord.Core;

public sealed class Settings
{
    public string? VideoId { get; set; }
    public string? InputId { get; set; }
    public string? OutputId { get; set; }
    public string? Mode { get; set; }
    public double Volume { get; set; } = 0.8;
    public bool Muted { get; set; }
    public double Width { get; set; } = 1180;
    public double Height { get; set; } = 780;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Fullscreen { get; set; }
}

public static class SettingsStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CaptureCord");
    public static Settings Load(string? path = null)
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path ?? Path.Combine(DirectoryPath, "settings.json"))) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public static void Save(Settings settings, string? path = null)
    {
        path ??= Path.Combine(DirectoryPath, "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}

public static class Log
{
    private static readonly object Gate = new();
    // Deliberately omit device identifiers, device names, paths and exception messages.
    public static void Write(string operation, Exception? error = null)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.DirectoryPath);
                var path = Path.Combine(SettingsStore.DirectoryPath, "diagnostics.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
                File.AppendAllText(path, JsonSerializer.Serialize(new { time = DateTimeOffset.UtcNow, operation, error = error?.GetType().Name, hresult = error?.HResult }) + Environment.NewLine);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine(e.GetType().Name); }
    }
}
