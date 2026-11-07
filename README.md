# CaptureCord

**Capture. Play. Share.** A free Windows 10/11 x64 capture-card preview application, licensed under **GPL-3.0-or-later**. Forks, modifications, redistribution, commercial redistribution under GPL terms, and community contributions are welcome. No accounts, advertisements, subscriptions or telemetry.

CaptureCord is an independent, unofficial community project and is not affiliated with, endorsed by, or sponsored by Discord Inc.

## Current implementation

This is an initial implementation, not a hardware-certified release. It contains a real Media Foundation source-reader pipeline and WASAPI input-to-output audio routing. Supported native modes are enumerated from each video device. Windows negotiates decoding/color conversion to RGB32 for the WPF preview. MJPEG, YUY2 and NV12 are usable only when the device and installed Windows transforms can negotiate them. Unsupported modes produce an error instead of a compatibility claim.

The dark interface provides independent video/audio selectors with readable themed dropdowns, a mode selector, start/stop, aspect-preserving preview, volume/mute with percentage display, F11 fullscreen, F10 borderless mode with controls, diagnostics and local settings. Rounded controls, keyboard focus indicators and subtle hover transitions keep interaction clear. Playback controls wrap on narrow windows. Stop capture before changing devices/mode. Double-click the preview to toggle fullscreen; Escape exits it. Audio continues in fullscreen and while the window is in the background.

The original CaptureCord icon combines a capture frame with a play symbol. Its editable vector source is `src/CaptureCord.App/Assets/Icon.xaml`; `powershell -STA -File scripts/build-icon.ps1` regenerates PNG and multi-resolution ICO assets without external tools. The icon is embedded in the executable, window and application header.

**GPU preview:** Enabled by default, Direct3D 11 uploads RGB32 frames to GPU buffers and presents them through a DXGI swapchain on the default hardware adapter. The preview preserves aspect ratio and follows resizing/fullscreen. Diagnostics identify the adapter and active path. Disable **GPU preview** to use WPF, or leave it enabled for automatic fallback if initialization/presentation fails. Hardware decoding, GPU color conversion and a zero-copy capture pipeline are not implemented; the capture stage still produces CPU RGB32 frames. No proprietary GPU SDK is needed.

**Permissions:** Choose **Permissions** to inspect camera and HDMI-audio input access. When supported, **Grant access** requests Windows consent; otherwise it opens Camera or Microphone privacy settings. Enable the device/desktop-app switches there and return to recheck. This portable desktop app cannot change permission switches itself. Unknown/unsupported status is not falsely shown as allowed; actual device opening remains authoritative. HDMI audio input is controlled by the microphone category, independently of the microphone selected in Discord.

**Known gaps:** no shared audio/video presentation clock or automatic drift correction; recovery is manual refresh/restart rather than automatic reconnect; no DirectShow fallback; no measured HDMI latency or confirmed Discord audio capture. The UGREEN B0DGXJS6BF reference device has not been validated. See [roadmap](docs/roadmap.md) and [hardware validation](docs/testing.md).

## Download

The [GitHub releases](https://github.com/CapricornOfLynx/CaptureCord/releases) provide a Windows x64 ZIP containing a ready-to-run, self-contained `CaptureCord.App.exe` with .NET included. Extract the complete ZIP and keep its license/third-party notices. No separate .NET installation is needed for that download. Alpha builds are unsigned and are not hardware-certified releases.

## Build and run

Install the .NET 10 SDK on Windows x64 (the .NET 10 Desktop Runtime is needed only for framework-dependent builds). Open `CaptureCord.sln` in an IDE supporting .NET 10, or run from this directory:

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet run --project src/CaptureCord.App -c Release --no-build
```

The executable is `src/CaptureCord.App/bin/Release/net10.0-windows/CaptureCord.App.exe`. Normal operation needs no administrator rights, vendor software, FFmpeg executable, OBS or virtual audio cable. Windows N editions may require Microsoft's Media Feature Pack. Protected HDMI/HDCP content is not bypassed.

## Connect and preview

1. Connect an unprotected console HDMI source to your capture card and connect its USB cable to the PC. HDMI passthrough can remain connected to a TV; passthrough behavior belongs to the hardware.
2. Launch CaptureCord and select the video device and one of its native modes. The preferred default is the closest available resolution to 1080p, then the closest rate to 60 FPS.
3. Explicitly select the card's audio input and your speakers/headphones. CaptureCord never guesses that a similarly named microphone is the HDMI input.
4. Press **Start capture**. Use **Stop capture** before unplugging or changing modes. On failure, check cables, close conflicting apps, reconnect, refresh and start again.
5. For Discord, share the CaptureCord **application window** with sound enabled and keep your normal microphone selected in Discord. Verify sound with a remote listener; support remains unconfirmed for this implementation. See [Discord validation](docs/discord.md).

Windows camera and microphone privacy settings must permit desktop applications. Audio volume/mute changes CaptureCord's rendered audio and may also affect what a sharing application captures.

## Verification

```powershell
./scripts/verify.ps1
dotnet run --project tests/CaptureCord.Tests -c Release -- --probe
```

`verify.ps1` performs a locked restore, Release build, dependency-free executable unit tests, direct/transitive dependency-license validation, and an invisible WPF startup/shutdown smoke test. The smoke test also opens all four dropdowns off-screen with synthetic entries, verifies text contrast and selection, and renders full/compact window previews under the application's build output `ui-smoke` directory. The unit suite is intentionally an executable; `dotnet test` alone does not run these checks. `--probe` additionally enumerates devices without opening cameras or recording microphones. Hardware and Discord checks are manual and listed in `docs/testing.md`.

GPU smoke checks perform hardware upload/readback comparison, presentation and buffer recreation. A host without a hardware adapter reports a skip instead of claiming a hardware pass. The permission window and status checks are exercised without requesting access or opening privacy settings. See [release packaging](docs/releases.md) for the pinned self-contained build and publication process.

## Settings, diagnostics and licenses

Settings are stored in `%LOCALAPPDATA%/CaptureCord/settings.json`, including locally selected endpoint IDs, window geometry and fullscreen preference. Structured local logs contain operation names, timestamps, exception types and HRESULTs, with a 1 MB rotation limit; device names/identifiers and exception messages are omitted. Nothing is uploaded. Delete the directory to reset settings/logs.

See [architecture](docs/architecture.md), [contributing](CONTRIBUTING.md), [license](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md). Binary redistributors must meet GPL source obligations and preserve dependency notices. Windows OS components are used through their system APIs and are not bundled. The standard GPL v3 license text is accompanied by the project's explicit “or later” grant here and in source headers.
