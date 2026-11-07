# Development roadmap

## Implemented foundation

- .NET 10 WPF solution and independent capture/audio modules.
- Native Media Foundation video discovery, native type enumeration, mode selection, RGB32 capture and actual WPF preview path.
- Independent WASAPI endpoint discovery, input capture, output playback, volume/mute and bounded audio queue.
- Start/stop cleanup, reported-failure teardown, manual reconnect workflow, fullscreen and local settings.
- Basic capture/render/drop/queue diagnostics, unit checks, native discovery probe, WPF smoke test and Windows CI.

## Next: qualify the vertical slice

1. Validate real HDMI capture and playback on reference and unrelated UVC/UAC hardware. Correct driver-specific buffer/format issues discovered by those tests.
2. Validate Discord window sharing with application audio independently. Keep support unconfirmed until an actual result is recorded.
3. Introduce a common timestamp model, adjustable synchronization offset and measured clock-drift correction. Validate long-session lip-sync and discontinuities.
4. Profile CPU/frame allocation and implement D3D11 shared-texture presentation while preserving a tested CPU fallback.

## Reliability and releases

- Device notifications, debounced reconnect and opt-in automatic session recovery.
- More native-buffer layout coverage and hardware-in-loop tests.
- Further accessibility work, per-monitor fullscreen validation and capture-only sharing window. Fully templated dark selectors and focus/selection states are implemented.
- Signed release packaging, reproducible release instructions and source archives with every binary release.
- DirectShow fallback only for demonstrated hardware gaps and after dependency/license review.

Do not advertise fixed latency, broad capture-card compatibility, synchronized playback or Discord audio support before measurements and validation.
