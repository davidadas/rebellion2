# Windows audio diagnostics

This standalone check compares the formats Windows accepts on each active audio output.
It runs without Unity, installed game content, a .NET SDK, or a C++ compiler. Windows PowerShell
compiles the small collector in memory using the .NET Framework already included with Windows.

## Sending the check to a player

Create the portable ZIP on a maintainer's checkout with Python 3:

```bash
python3 Tools/WindowsAudio/Package.py Rebellion2-audio-check.zip
```

Send that ZIP to the player. It contains only the collector, two runners and short instructions;
it excludes existing reports, game logs, media and other checkout files. The player does not need
Git, a GitHub account, a repository checkout, Python, Unity, or a compiler installation.

The player closes the game after a silent launch, extracts the ZIP, double-clicks `Run.cmd`, and
sends back `audio-format-report.txt` from the extracted folder. The report automatically includes
the latest saved `Player.log`, its path and its modification time before checking audio devices.
The game log comes from the last saved game run; the format measurements come from the current
diagnostic run. If the game log is unavailable, the report records that and continues.

## Running from a checkout

Use 64-bit Windows 10 or Windows 11. Close the game before running the check.

1. Check out the diagnostic branch, then open `Tools\WindowsAudio` in File Explorer.
2. Double-click `Run.cmd`. A console stays open and displays the report location or an error.
3. Collect `Tools\WindowsAudio\audio-format-report.txt`. Include any console error if the check fails.

From a terminal at the repository root, the equivalent command is:

```powershell
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\Tools\WindowsAudio\Run.ps1
```

The execution policy override applies only to that PowerShell process. No administrator access is
needed. To write the report elsewhere, append `-OutputPath "$env:USERPROFILE\Downloads\audio-format-report.txt"`.
Run the command again in a fresh PowerShell process after changing audio settings for a comparison;
copy the previous report first because the output file is replaced on each run.

For a small clone containing only this tool:

```powershell
git clone --filter=blob:none --sparse --branch audio-check https://github.com/davidadas/rebellion2.git rebellion2-audio-check
Set-Location rebellion2-audio-check
git sparse-checkout set Tools/WindowsAudio
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File .\Tools\WindowsAudio\Run.ps1
```

## Reading the report

The check records default output roles, endpoint names and IDs, exact format bytes, and raw
Windows HRESULTs. `0x00000000` means success; `0x00000001` means a closest supported format was
returned rather than an exact match. `0x88890008` means the requested format was rejected.

For every output, it attempts the unchanged native format on its original audio client and on
fresh clients. It also checks six combinations at 48 kHz: two or eight channels, each with
32-bit floating point, 32-bit integer PCM, or 16-bit integer PCM. Each combination uses the same
extensible format container, endpoint and requested 100 ms shared buffer. Fresh clients test
both ordinary and event-driven initialization. Any closest alternative returned by Windows is
also opened so that a suggestion is distinguished from a successful initialization.

Compare channel counts while holding encoding constant to investigate channel negotiation.
Compare the two integer PCM cases to investigate bit depth. Compare the two 32-bit cases to
investigate floating-point versus integer representation. A successful result here establishes
that an independent client can open that format; it does not establish what Unity requested or
whether the game played audio. Correlate the report with the affected run's `Player.log`.

The collector never starts playback, changes Windows audio settings, installs a driver, starts
the game, or enables the proposed custom output fallback. It reads the game's configured Windows
log location, `%USERPROFILE%\AppData\LocalLow\AdasGames\Rebellion 2\Player.log`, without scanning
other folders. Native driver calls are synchronous;
if the console stops responding, close it and retain the partial report. The file is flushed
as each result is written. Reports contain output-device names, IDs and the saved game log, so
review them before posting them publicly.
