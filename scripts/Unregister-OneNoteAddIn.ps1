param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$targetFramework = "net8.0-windows10.0.19041.0"
$comHostPath = Join-Path $root "src\OneNoteAnalyzeAddIn\bin\$Configuration\$targetFramework\OneNoteAnalyzeAddIn.comhost.dll"
$progId = "OneNoteAnalyzeAddIn.Connect"
$clsid = "{D3D78A72-7DF9-45BE-A7A6-588940F65B0A}"

if (Test-Path $comHostPath) {
    Write-Host "Unregistering COM host: $comHostPath"
    try {
        & regsvr32.exe /u /s $comHostPath
    } catch {
        Write-Host "regsvr32 unregister failed; continuing with explicit cleanup."
    }
}

$addinKey = "HKCU:\Software\Microsoft\Office\OneNote\Addins\$progId"
if (Test-Path $addinKey) {
    Remove-Item -Path $addinKey -Recurse -Force
}

$classesRoot = "HKCU:\Software\Classes"
$progIdKey = Join-Path $classesRoot $progId
$clsidKey = Join-Path $classesRoot "CLSID\$clsid"
if (Test-Path $progIdKey) {
    Remove-Item -Path $progIdKey -Recurse -Force
}
if (Test-Path $clsidKey) {
    Remove-Item -Path $clsidKey -Recurse -Force
}

Write-Host "OneNote COM add-in registration removed."
