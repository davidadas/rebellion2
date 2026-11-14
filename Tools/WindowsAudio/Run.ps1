[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'audio-format-report.txt')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This diagnostic requires Windows.'
}
if (-not [Environment]::Is64BitProcess) {
    throw 'Run this check with 64-bit Windows PowerShell.'
}
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne [Threading.ApartmentState]::STA) {
    throw 'Run powershell.exe with -STA, or double-click Run.cmd.'
}

$reportPath = [IO.Path]::GetFullPath($OutputPath)
Add-Type -Path (Join-Path $PSScriptRoot 'WindowsAudioProbe.cs')
[Rebellion.Diagnostics.WindowsAudioProbe]::Collect($reportPath)
Write-Host "Audio check complete. Report: $reportPath"
