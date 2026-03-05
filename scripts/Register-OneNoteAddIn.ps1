param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$targetFramework = "net8.0-windows10.0.19041.0"
$comHostPath = Join-Path $root "src\OneNoteAnalyzeAddIn\bin\$Configuration\$targetFramework\OneNoteAnalyzeAddIn.comhost.dll"
$progId = "OneNoteAnalyzeAddIn.Connect"
$clsid = "{D3D78A72-7DF9-45BE-A7A6-588940F65B0A}"

if (-not (Test-Path $comHostPath)) {
    throw "COM host not found: $comHostPath. Build the project first."
}

# Best-effort native registration; on some systems this requires elevation and may silently fail.
Write-Host "Attempting native COM registration: $comHostPath"
try {
    & regsvr32.exe /s $comHostPath
} catch {
    Write-Host "regsvr32 failed; continuing with explicit per-user registration."
}

# Explicit per-user COM registration (no admin required)
$classesRoot = "HKCU:\Software\Classes"
$progIdKey = Join-Path $classesRoot $progId
$progIdClsidKey = Join-Path $progIdKey "CLSID"
$clsidKey = Join-Path $classesRoot "CLSID\$clsid"
$inprocKey = Join-Path $clsidKey "InprocServer32"
$clsidProgIdKey = Join-Path $clsidKey "ProgID"

if (-not (Test-Path $progIdKey)) { New-Item -Path $progIdKey | Out-Null }
if (-not (Test-Path $progIdClsidKey)) { New-Item -Path $progIdClsidKey | Out-Null }
if (-not (Test-Path $clsidKey)) { New-Item -Path $clsidKey | Out-Null }
if (-not (Test-Path $inprocKey)) { New-Item -Path $inprocKey | Out-Null }
if (-not (Test-Path $clsidProgIdKey)) { New-Item -Path $clsidProgIdKey | Out-Null }

Set-Item -Path $progIdKey -Value "OneNote Analyze COM Add-in"
Set-Item -Path $progIdClsidKey -Value $clsid
Set-Item -Path $clsidKey -Value "OneNote Analyze COM Add-in"
Set-Item -Path $clsidProgIdKey -Value $progId
Set-Item -Path $inprocKey -Value $comHostPath
Set-ItemProperty -Path $inprocKey -Name "ThreadingModel" -Value "Both"

$addinKey = "HKCU:\Software\Microsoft\Office\OneNote\Addins\$progId"
if (-not (Test-Path $addinKey)) {
    New-Item -Path $addinKey | Out-Null
}

Set-ItemProperty -Path $addinKey -Name "FriendlyName" -Value "OneNote Analyze Add-In"
Set-ItemProperty -Path $addinKey -Name "Description" -Value "Analyze a captured region with Gemini from OneNote."
Set-ItemProperty -Path $addinKey -Name "LoadBehavior" -Type DWord -Value 3
Set-ItemProperty -Path $addinKey -Name "CommandLineSafe" -Type DWord -Value 0

Write-Host "OneNote COM add-in registration complete."
