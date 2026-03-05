# Global Capture Assistant

Standalone Windows capture assistant (no OneNote add-in).

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

## Publish EXE
```powershell
dotnet publish src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  /p:PublishSingleFile=true
```

Published output:
`src\GlobalCaptureAssistant\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\GlobalCaptureAssistant.exe`

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
