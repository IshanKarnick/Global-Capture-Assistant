param(
    [string]$Version,
    [string]$Runtime = "win-x64",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj"
$readmePath = Join-Path $repoRoot "README.md"
$releaseRoot = Join-Path $repoRoot "artifacts\release"

if ([string]::IsNullOrWhiteSpace($Version))
{
    [xml]$projectXml = Get-Content -Path $projectPath
    $Version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($Version))
{
    $Version = Get-Date -Format "yyyy.MM.dd.HHmm"
}

$packageName = "GlobalCaptureAssistant-$Version-$Runtime"
$publishDir = Join-Path $releaseRoot $packageName
$zipPath = Join-Path $releaseRoot "$packageName.zip"
$checksumPath = "$zipPath.sha256"
$selfContained = if ($FrameworkDependent.IsPresent) { "false" } else { "true" }

New-Item -Path $releaseRoot -ItemType Directory -Force | Out-Null

if (Test-Path -Path $publishDir)
{
    Remove-Item -Path $publishDir -Recurse -Force
}

if (Test-Path -Path $zipPath)
{
    Remove-Item -Path $zipPath -Force
}

if (Test-Path -Path $checksumPath)
{
    Remove-Item -Path $checksumPath -Force
}

& dotnet restore $projectPath
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet restore failed."
}

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", "Release",
    "-r", $Runtime,
    "--self-contained", $selfContained,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:PublishTrimmed=false",
    "/p:Version=$Version",
    "/p:InformationalVersion=$Version",
    "-o", $publishDir
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed."
}

if (Test-Path -Path $readmePath)
{
    Copy-Item -Path $readmePath -Destination (Join-Path $publishDir "README.md") -Force
}

$licenseCandidate = Get-ChildItem -Path $repoRoot -File | Where-Object { $_.Name -match "^LICENSE(\..+)?$" } | Select-Object -First 1
if ($null -ne $licenseCandidate)
{
    Copy-Item -Path $licenseCandidate.FullName -Destination (Join-Path $publishDir $licenseCandidate.Name) -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
"$($hash.Hash)  $(Split-Path -Leaf $zipPath)" | Set-Content -Path $checksumPath -Encoding Ascii

Write-Host "Release package created:"
Write-Host "  $zipPath"
Write-Host "SHA256:"
Write-Host "  $checksumPath"
