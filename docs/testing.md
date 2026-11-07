# Verification and hardware acceptance

## Automated checks

Run `./scripts/verify.ps1` on Windows with .NET 10 SDK. Tests cover mode choice, rational frame rates, empty capabilities, pixel-format labels, newest-frame replacement, concurrent producer/consumer accounting and settings recovery. Synthetic native tests exercise IMFAttributes, padded/negative-stride pixel copies and short-buffer rejection/unlock against actual Media Foundation memory buffers. The WPF smoke test instantiates the real XAML, enumerates video devices without activating them, and exercises asynchronous close. It does not test image rendering or actual WASAPI playback. CI runs the same script.

Optional: `dotnet run --project tests/CaptureCord.Tests -c Release -- --probe` checks Media Foundation startup/discovery and WASAPI endpoint enumeration without recording media.

`--probe-modes` additionally opens each video source temporarily to enumerate capabilities twice, verifying release/reopen behavior. It does not read video frames or capture audio. Unlike `--probe`, this can briefly activate device hardware and requires camera permission. Device names/IDs are not printed. A native regression test uses a generated WAV source (no physical camera) to verify that releasing a source reader preserves its caller-owned source and that repeated shutdown cannot discard successful results.

## Required hardware tests

- Test a standard UVC/UAC card and, separately, the UGREEN B0DGXJS6BF reference device; record actual enumerated modes rather than marketing specifications.
- Verify unprotected HDMI color bars at several resolutions/rates and at least the available MJPEG/YUY2/NV12 modes. Check orientation, row padding, colors, aspect ratio, resizing and fractional rates.
- Confirm audio from the explicitly selected HDMI input reaches each selected output (headphones, speakers, differing sample-rate devices). Verify volume/mute, format conversion and channel mapping.
- Start/stop at least 50 times and reopen the capture card in a second application after stopping. Monitor handles and managed/native memory during a 30-minute session.
- Unplug video, unplug audio input, unplug output, and reconnect. Confirm useful errors, responsive UI, released resources and successful refresh/restart. Test another app holding the device and disabled camera/microphone privacy permissions.
- Test close during discovery, capability enumeration, startup, capture and disconnection. A stalled driver may need separate investigation.
- Test F11/F10/Escape, restored window bounds, multiple monitor DPI, missing remembered devices and damaged settings files.
- Leave capture running behind another application and while using HDMI passthrough. Test minimize/restore; background video presentation can pause while audio continues, so render/drop counts can differ substantially.
- Measure actual video latency with a high-speed camera and a reference display. Use a flash/beep HDMI test signal to measure lip-sync and clock drift. The app's delivery metric is not this measurement.
- Complete [Discord checks](discord.md) with a remote listener and the normal microphone active.

No live HDMI capture, listening test, physical reconnect test, latency measurement or Discord audio stream has been validated by automated tests. Do not infer hardware support from a successful build or device list.

## Permissions, GPU and release regression checks

Permission tests distinguish explicit denial from unknown desktop-app status and check the native status API without requesting consent or activating media. The WPF smoke test instantiates and closes the real permission panel. Manually test both enabled/disabled Windows camera and microphone privacy switches, cancellation, return from Settings and administrator policy denial. Do not interpret an unknown capability status as permission granted.

GPU smoke checks upload synthetic frames through D3D11, copy them back to staging memory, compare all bytes, present and recreate the surface at multiple sizes. Hardware absence produces a reported skip; `--require-gpu` makes it an error. Verify live GPU/WPF switching, device-removal fallback, repeated start/stop, resizing/fullscreen, mixed-DPI monitors, minimize/restore, double-click handling and native-child Discord capture on physical hardware. Shader decoding/color conversion and end-to-end latency are outside this smoke test.

The release script smoke-tests the actual self-contained single-file EXE before zipping it. Test ZIP extraction and startup on a separate Windows 10/11 x64 machine with no .NET runtime installed before calling that environment supported.

Clean-feed regression checks instantiate the real borderless window, verify that it contains only an Image, compare synthetic frame pixels, check that minimizing the controls does not minimize the sharing target, and verify clearing on stop. Manually verify resizing, dragging, fullscreen, stop/disconnect, close/reopen and long-running updates while controls are minimized. A remote Discord listener must confirm that the chosen clean-feed window contains no app controls or visible software borders and still includes game audio.
