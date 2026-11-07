# Initial implementation verification

Date: 2026-11-07. Environment: Windows x64, .NET SDK 10.0.403.

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Release solution build | Passed, zero warnings and errors |
| Executable automated suite | 11/11 checks passed, including permission policy/API and native memory-buffer tests |
| Direct/transitive license allowlist | Passed: NAudio.Wasapi 2.2.1 and NAudio.Core 2.2.1, MIT |
| Invisible WPF startup and shutdown | Passed after correcting test startup and deferred close |
| Dropdown UI regression checks | Passed: all four custom dropdown templates open/select, text contrast at least 4.5:1, full/compact window and popup snapshots |
| Native discovery probe | Passed: one video device, six audio capture endpoints, ten playback endpoints; identities omitted |
| Framework-dependent Release publish | Passed; local output in artifacts/CaptureCord |
| Physical HDMI video/audio, disconnect and long-session tests | Not performed |
| Discord video/application audio and microphone coexistence | Not performed |
| D3D11 hardware upload/readback, presentation, surface recreation | Passed locally on NVIDIA GeForce RTX 4070 Ti SUPER; other adapters unverified |
| Permission panel/status API | Native check and panel smoke test passed; actual OS consent and settings changes not exercised |
| Shared-clock A/V synchronization, GPU decoding/color conversion | Not implemented |

The initial smoke-test startup attempted to set WPF StartupUri to null, which raised ArgumentNullException and produced a Windows application-error dialog. Startup now explicitly constructs the main window, and the smoke-test path passes. Close also defers the final Close call until after the first Closing callback. These results do not certify the capture pipeline on any physical card.

Source changes span the four src projects, tests/CaptureCord.Tests, build/configuration files, scripts, CI and repository documentation. The next validation step is live HDMI preview and WASAPI listening with explicitly selected hardware, followed by the independent Discord test matrix including native GPU-child window capture. Then implement measured A/V synchronization and profile CPU capture costs before adding GPU decoding/color conversion.

A local Git repository was initialized. The initial iteration lacked a configured Git author identity; publication is now authorized to CapricornOfLynx/CaptureCord using the connected GitHub account.
