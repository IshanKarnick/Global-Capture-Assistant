param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$targetFramework = "net10.0-windows10.0.19041.0"
$comHostPath = Join-Path $root "src\OneNoteAnalyzeAddIn\bin\$Configuration\$targetFramework\OneNoteAnalyzeAddIn.comhost.dll"

if (-not (Test-Path $comHostPath)) {
    throw "COM host not found: $comHostPath. Build the project first."
}

Write-Host "Registering COM host: $comHostPath"
& regsvr32.exe /s $comHostPath

$addinKey = "HKCU:\Software\Microsoft\Office\OneNote\Addins\OneNoteAnalyzeAddIn.Connect"
if (-not (Test-Path $addinKey)) {
    New-Item -Path $addinKey | Out-Null
}

Set-ItemProperty -Path $addinKey -Name "FriendlyName" -Value "OneNote Analyze Add-In"
Set-ItemProperty -Path $addinKey -Name "Description" -Value "Analyze a captured region with Gemini from OneNote."
Set-ItemProperty -Path $addinKey -Name "LoadBehavior" -Type DWord -Value 3
Set-ItemProperty -Path $addinKey -Name "CommandLineSafe" -Type DWord -Value 0

Write-Host "OneNote COM add-in registration complete."
