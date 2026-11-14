@echo off
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Run.ps1" %*
set "audio_probe_exit=%errorlevel%"
if not "%audio_probe_exit%"=="0" echo Audio check failed. Keep the error shown above with any partial report.
pause
exit /b %audio_probe_exit%
