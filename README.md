# Global Capture Assistant (Standalone) + OneNote Add-In

This repo now includes a standalone desktop app that works in all applications.

## Standalone app (recommended)

`src/GlobalCaptureAssistant`

Features:
- Floating capture button (always on top)
- Global hotkey: `Ctrl + Shift + Q`
- Region capture overlay
- Pinned modern glass-style sidebar
- Gemini image analysis
- Active-window context tagging (title/process)
- Option bar in sidebar:
  - model selection
  - thinking level
  - launch-on-sign-in toggle

### Build
```powershell
dotnet restore
dotnet build src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
```

### Run
```powershell
dotnet run --project src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
```

### Publish EXE
```powershell
dotnet publish src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  /p:PublishSingleFile=true
```

Published output:
`src\GlobalCaptureAssistant\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\GlobalCaptureAssistant.exe`

### First run
- Enter your Gemini API key when prompted.
- API key is stored encrypted with Windows DPAPI at:
  `%AppData%\GlobalCaptureAssistant\settings.json`
- Logs are written to:
  `%LocalAppData%\GlobalCaptureAssistant\logs\`

## OneNote COM add-in (legacy path)

`src/OneNoteAnalyzeAddIn` remains in the solution, but the standalone app is the primary path for reliability and cross-application usage.

## Solution build
```powershell
dotnet build OneNoteAnalyzeAddIn.slnx -c Debug
dotnet test OneNoteAnalyzeAddIn.slnx -c Debug --no-build
```
