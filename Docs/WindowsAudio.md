# Windows audio recovery

The game keeps Unity's mixer for music, effects, voices and video audio. In Windows desktop
players, startup checks the game's existing Windows render sessions on a background thread.
An existing session keeps Unity's normal output. If the query succeeds and finds no session,
the game attempts a private 16-bit stereo PCM output through Windows waveform audio, using
Unity's current sample rate and DSP buffer dimensions.

The fallback forwards the listener's mixed audio; it does not replace audio sources, change
content, change Windows defaults, or convert the game to mono. Source volume and pause settings
still apply. Scene loads attach the fallback to the new scene's listeners, and audio configuration
changes close the previous stream before checking output again. Mixed blocks are silenced on
Unity's route only after Windows accepts them, preventing duplicate playback.

If session inspection fails, the normal output remains selected. Automatic session detection is
also skipped under Wine: Wine 10's session enumerator returns newly created expired wrappers for
default-session GUIDs, so it cannot reliably establish whether Unity has a working stream.

The Windows player accepts these troubleshooting overrides:

- `-windows-stereo-audio` explicitly selects the private stereo output.
- `-system-audio` keeps Unity's normal output and disables recovery.
- `-nosound` is respected without opening a recovery stream.

These switches are game arguments. Native Linux and macOS players do not use this path.
Initialization and output failures are recorded once in `Player.log`; recovery does not repeatedly
reopen a failing device each frame.

The output implementation preallocates PCM buffers, reuses each only after the driver returns it,
and waits for buffer completions to pace Unity's emulated output. Shutdown wakes waiting writers
and resets playback before releasing prepared memory. Memory that a failing driver refuses to
release is retained rather than freed while the driver may still reference it.
