param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$targetFramework = "net10.0-windows10.0.19041.0"
$comHostPath = Join-Path $root "src\OneNoteAnalyzeAddIn\bin\$Configuration\$targetFramework\OneNoteAnalyzeAddIn.comhost.dll"

if (Test-Path $comHostPath) {
    Write-Host "Unregistering COM host: $comHostPath"
    & regsvr32.exe /u /s $comHostPath
}

$addinKey = "HKCU:\Software\Microsoft\Office\OneNote\Addins\OneNoteAnalyzeAddIn.Connect"
if (Test-Path $addinKey) {
    Remove-Item -Path $addinKey -Recurse -Force
}

Write-Host "OneNote COM add-in registration removed."
