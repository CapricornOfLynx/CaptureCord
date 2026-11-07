CaptureCord alpha for Windows 10/11 x64: live Media Foundation video preview, independent WASAPI audio routing, dark themed device selectors, fullscreen, settings and diagnostics.

**New in alpha.3: clean Discord sharing target.** Press **Clean feed · Discord**, then select **CaptureCord — Clean feed** in Discord. That separate borderless window shows only the capture image: no software controls, title bar, status or diagnostics. Keep it visible while minimizing the controls. Drag to move, resize at the edges, double-click/F11 for fullscreen, Escape to leave fullscreen and Alt+F4 to close. It shares the existing capture/audio session; stop/disconnect clears the picture. The clean feed uses WPF image presentation; the main window retains its GPU preview. Remote Discord video/audio still needs a listener test.

**Fixed in alpha.2: empty Capture mode list.** Reader disposal previously shut down its media source, and a second shutdown raised MF_E_SHUTDOWN and discarded already-enumerated modes. Source lifetime is now explicitly owned by the caller; repeated shutdown is safe. Native regression tests and repeated mode enumeration on both connected video devices passed (24 and 159 modes). Loading/empty/error feedback is preserved instead of being overwritten by the general ready message.

New: **Permissions** opens a camera/HDMI-audio permission panel. It checks Windows capability access, requests consent when Windows exposes a prompt, and otherwise opens the corresponding Windows privacy page. Return to the app to recheck and refresh devices. It does not alter privacy settings or bypass administrator policies.

New: **GPU preview** uses a hardware D3D11 device and DXGI swapchain, preserves aspect ratio and falls back automatically to WPF if the GPU path fails. Diagnostics show the adapter and renderer. CPU decoding/color conversion and the CPU-to-GPU upload remain; this is not a zero-copy or hardware-decoding claim. Synthetic hardware upload/readback, presentation and format recreation passed locally on an NVIDIA GeForce RTX 4070 Ti SUPER. Other adapters still need validation.

### Download and run

Download **CaptureCord-0.1.0-alpha.3-win-x64.zip**, extract it, then run **CaptureCord.App.exe**. The EXE includes the .NET 10 runtime; no separate .NET installation or administrator rights are required. Keep the license and third-party notices alongside the executable. The first launch may take longer while native runtime components are extracted.

The build is unsigned, so Windows may show an unknown-publisher/SmartScreen notice. This alpha has passed automated builds, unit checks, native discovery, dropdown/permission-window smoke checks and a smoke test of the actual bundled EXE. Physical HDMI video/audio, disconnect recovery and Discord application audio have **not** been validated. A shared A/V clock, GPU decoding and GPU color conversion are not implemented yet. Do not treat this as a hardware-certified release.

SHA-256 checksums are attached. Corresponding source, build scripts and dependency lockfiles are available in this tag's source archives. GPL-3.0-or-later; bundled dependency notices are included. No telemetry, accounts, subscriptions or virtual audio driver.

CaptureCord is an independent, unofficial community project and is not affiliated with, endorsed by, or sponsored by Discord Inc.
