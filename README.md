# OneNote Analyze COM Add-In

OneNote Desktop COM add-in with:
- Ribbon button: `Analyze Selection`
- Global hotkey: `Ctrl+Shift+Q`
- WPF companion sidebar (modern glassmorphism-lite look)
- Overlay region capture
- Gemini analysis request with optional page metadata

## Project layout
- `src/OneNoteAnalyzeAddIn`: COM add-in + WPF UI + Gemini client
- `tests/OneNoteAnalyzeAddIn.Tests`: unit tests for prompt/composition/settings behavior
- `scripts/Register-OneNoteAddIn.ps1`: register COM host + OneNote add-in registry keys
- `scripts/Unregister-OneNoteAddIn.ps1`: unregister and cleanup

## Build
```powershell
dotnet restore
dotnet build OneNoteAnalyzeAddIn.slnx
```

## Register in OneNote
1. Build the add-in in `Debug` or `Release`.
2. Run:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Register-OneNoteAddIn.ps1 -Configuration Debug
```
3. Restart OneNote Desktop.

## First run
- Trigger `Analyze Selection` from Ribbon or `Ctrl+Shift+Q`.
- Enter Gemini API key when prompted.
- Key is stored encrypted using Windows DPAPI in:
  `%AppData%\OneNoteAnalyzeAddIn\settings.json`

## Gemini model settings
Defaults:
- Model: `gemini-3.1-pro-preview`
- API endpoint: `v1beta/models/{model}:generateContent`
- Auth header: `x-goog-api-key`
- Thinking level: `low`

## Known notes
- This implementation targets OneNote Desktop (2016/365) COM host.
- OneNote page metadata extraction is best-effort and falls back gracefully.
