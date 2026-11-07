# Architecture

## Modules

| Project | Responsibility |
| --- | --- |
| CaptureCord.Core | Device/mode/frame models, default-mode policy, bounded frame mailbox, JSON settings and local logging |
| CaptureCord.Capture | Media Foundation startup/shutdown, dynamic video enumeration, native capability enumeration, source reader and COM lifetime |
| CaptureCord.Audio | Active Windows endpoint discovery, WASAPI capture, bounded audio queue, volume and shared-mode playback |
| CaptureCord.App | WPF dark interface, D3D11/DXGI GPU presentation with WPF fallback, permission UI, session lifecycle, fullscreen and diagnostics |
| CaptureCord.Tests | Dependency-free automated policy/concurrency/settings checks and optional native discovery probe |

## Video

The UI owns a single Media Foundation platform lifetime. Enumeration runs on worker threads; symbolic links are opaque IDs, with no manufacturer matching. Capability discovery temporarily creates a media source and source reader, enumerates native types, and explicitly shuts down/releases them. Starting a session reopens the chosen device, disables other streams, selects the exact native index, then requests RGB32 with source-reader video processing enabled. Windows performs supported decoding/conversion. The read loop blocks on a background thread, never on WPF's dispatcher.

Every sample/buffer/media type is released deterministically via a minimal owned COM-reference wrapper. Worker threads explicitly initialize a COM MTA. The interop definitions map Windows SDK vtable slots and P/Invoke entry points; they do not copy a third-party interop implementation. Converted buffers use IMF2DBuffer.Lock2D when available for actual surface pitch; contiguous buffers are validated and copied by signed default stride into a top-down frame. One atomic mailbox retains only the newest frame and counts replaced frames. WPF pulls frames on its rendering event and writes Bgr32 pixels to a WriteableBitmap using Uniform stretch.

By default a hardware-only D3D11 device and a windowed DXGI swapchain receive RGB32 frames using UpdateSubresource. An HwndHost child window presents the buffer, sized to fit the source aspect ratio; DXGI stretches to that window. The blt-model swapchain is intentionally used for compatibility with the HWND host. Present uses DO_NOT_WAIT; busy/occluded frames count as presentation skips, without building a renderer queue. WPF owns fullscreen and DXGI Alt+Enter is disabled. The UI thread owns the immediate context and native-window lifetime. Video acquisition/conversion remains on the capture worker. GPU upload still performs a CPU-to-GPU transfer; it is not GPU decoding or a zero-copy capture path.

Initialization or presentation errors release GPU objects and switch to WriteableBitmap for the remainder of the session. Users can explicitly choose WPF or retry GPU by toggling GPU preview; the preference is persisted. Adapter names and fallback HRESULTs are shown in diagnostics. CPU frame allocation/conversion still needs profiling at high resolutions. Delivery diagnostics measure time from completion of the frame copy to pixel submission/presentation, excluding driver queues, decoding, display scanout and HDMI input latency. Sample timestamps are retained but are not used as a presentation clock in this iteration.

HWND airspace means WPF overlays are not composited over the live GPU child window. Controls are outside the preview, and the native child is hidden while idle. Discord capture of the native child must be validated independently. The main window forwards preview mouse interaction to WPF, preserving double-click fullscreen behavior.

Stopping cancels the loop and calls source shutdown on a worker to interrupt pending reads, awaits the loop, and releases the reader/source before platform shutdown. A broken driver could still stall a native call; forced termination of native code is not attempted. The UI serializes start/stop/close, tears down both streams on a reported failure, and supports explicit refresh/restart after reconnect. No automatic reactivation of a camera or microphone occurs.

## Audio

Video and audio endpoints are selected independently. No audio input is selected automatically on first launch. NAudio.Wasapi 2.2.1 captures the selected endpoint's shared-mode mix format in 20 ms requested buffers. A BufferedWaveProvider holds at most 120 ms. If accumulated input exceeds 80 ms, or a write would overflow, the queue is cleared and the reset is counted. Output uses event-driven shared WASAPI with a requested 30 ms buffer. These are requests and queue limits, not measured total latency guarantees.

Volume is applied to float samples before playback. NAudio negotiates playback format, using Windows' DMO resampler if necessary. Buffer starvation yields silence; no unbounded queue is created. Input and output callbacks report failures to the session owner. Capture and playback are disposed on a worker outside the UI thread.

Audio and video run independently with bounded queues. There is no shared-clock lip-sync or drift estimator yet; this limitation is visible in the README and roadmap. HDMI test signals and long-session measurements must guide the next synchronization implementation.

## Discord and trust boundaries

The permission service uses the documented Windows AppCapability ABI to check access without activating a camera or microphone. An explicit UI gesture may request access when Windows reports UserPromptRequired. Unavailable/NotDeclaredByApp status on unpackaged or older systems remains unknown, and the user is directed to documented ms-settings privacy URIs. No registry values or administrator policies are modified. Actual capture APIs remain the final access check. The permission panel rechecks after activation and the main window refreshes devices after the panel closes.

Playback belongs to the CaptureCord process, giving a supported application-capture mechanism an opportunity to capture its sound. That is an architectural hypothesis, not a verified Discord contract. There is no Discord SDK integration, client modification, injection, or custom virtual driver. The app neither records media to disk nor sends network traffic. All native Windows API calls run with the user's normal privileges.

## Sources

- [Microsoft source-reader processing](https://learn.microsoft.com/en-us/windows/win32/medfound/processing-media-data-with-the-source-reader)
- [RGB32 conversion negotiation](https://learn.microsoft.com/en-us/windows/win32/api/mfreadwrite/nf-mfreadwrite-imfsourcereader-setcurrentmediatype)
- [Signed stride semantics](https://learn.microsoft.com/en-us/windows/win32/medfound/mf-mt-default-stride-attribute)
- [NAudio 2.2.1 WASAPI playback implementation](https://github.com/naudio/NAudio/blob/v2.2.1/NAudio.Wasapi/WasapiOut.cs)
