# Discord window and application audio validation

Research date: 2026-11-07. **CaptureCord-specific compatibility is unverified.**

Discord documents Windows application sharing with sound. Its engineering documentation describes OS-specific audio capture and several window-capture techniques. Neither document establishes that this WPF/WASAPI implementation is captured successfully on a given Discord/Windows version.

- [Discord: streaming applications and sound](https://discord.com/blog/how-to-stream-to-discord-from-desktop-or-mobile)
- [Discord: Windows application window capture](https://support.discord.com/hc/en-us/articles/9410427556375)
- [Discord: streaming technology overview](https://discord.com/blog/how-it-all-goes-live-an-overview-of-discords-streaming-technology)

CaptureCord renders through WASAPI within its own process. This is intended to keep game audio associated with the shared application while leaving Discord's microphone unchanged. This design must be tested, not assumed successful.

## Reproducible test

1. Record OS build, Discord desktop version, endpoint formats, capture mode and capture-card model. Keep endpoint identifiers private.
2. Confirm local console video and game audio with Discord closed.
3. Join a voice channel with another listener. Select the usual microphone in Discord.
4. Press **Clean feed · Discord** in CaptureCord. Share **CaptureCord — Clean feed** with sound enabled, not the controls window. Ask the listener to independently confirm an image-only view (no software chrome), game sound and the normal microphone.
5. Repeat with the clean feed resized and in F11 fullscreen. Minimize the controls while keeping the independent clean feed restored. Check foreground/background and minimize/restore behavior. Also compare main-window GPU/WPF capture if relevant. Record failures rather than labeling the device supported.
6. Test mute/volume, audio device changes after stop/restart, and capture reconnect. Confirm stopping removes game audio and releases devices.
7. Run a synchronization test signal for at least 30 minutes. Record listener-observed offset and drift separately from local offset.

If sound is absent, keep CaptureCord's own playback operational. Check the current Discord client's documented sound-sharing controls, update/restart the client, and retry application sharing. Where the current client officially supports sharing another source with sound, evaluate that as a separate workflow and note its privacy implications. Do not change the microphone to the HDMI input as a supposed equivalent: that would replace voice input. No virtual audio driver, undocumented Discord API or injected capture mechanism is supplied.

Publish a successful/failed test report before changing the support statement. No Discord stream has been tested in this environment.
