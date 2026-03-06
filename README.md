# Global Capture Assistant

Standalone Windows capture assistant 
## What it does
- Always-on-top floating capture button (draggable)
- Global hotkey: `Ctrl + Shift + Q`
- Region capture overlay
- Modern glass sidebar with result + chat-more follow-ups
- Two-step AI flow:
  - Step 1: image analysis (Gemini model from settings)
  - Step 2: suggested follow-up prompts (`gemma-3-27b-it`)

## Project path
- `src/GlobalCaptureAssistant`

## Build
```powershell
dotnet restore
dotnet build src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
```

## Run
```powershell
dotnet run --project src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
```

## Package Local Release
```powershell
.\scripts\package-release.ps1
```

Optional args:
```powershell
.\scripts\package-release.ps1 -Version 1.0.0 -Runtime win-x64
.\scripts\package-release.ps1 -Version 1.0.0 -FrameworkDependent
```

Release output:
- `artifacts\release\GlobalCaptureAssistant-<version>-win-x64.zip`
- `artifacts\release\GlobalCaptureAssistant-<version>-win-x64.zip.sha256`

## First run
- Enter your Gemini API key when prompted.
- Key is stored with Windows DPAPI in:
  `%AppData%\GlobalCaptureAssistant\settings.json`
- Logs are written to:
  `%LocalAppData%\GlobalCaptureAssistant\logs\`

## Repository solution build
```powershell
dotnet build OneNoteAnalyzeAddIn.slnx -c Debug
```
