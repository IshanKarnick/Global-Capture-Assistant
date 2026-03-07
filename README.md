# Global Capture Assistant

Always-on-top Windows screenshot assistant with built-in AI analysis, follow-up chat, note generation, and on-screen annotations.

I built this because I got tired of how clumsy the normal screenshot-to-LLM workflow felt. Taking a screenshot, pasting it into ChatGPT, waiting for a response, then constantly switching back and forth to compare the answer with what I was actually doing kept breaking my concentration. Even split-screening everything felt awkward because I was rearranging my desktop just to make the tool usable. I wanted the answer to stay visible beside my work instead of disappearing into another tab or window. So I built this as a selfish tool first, mainly to remove that friction from my own day-to-day workflow.

- Windows desktop app
- WPF / .NET 8
- Global hotkey: `Ctrl + Shift + Q`
- Gemini and/or Groq API key required depending on the features you use
- WebView2 used for note-card rendering
- Floating button + tray + sidebar workflow
- Switchable Gemini/Groq providers for text analysis and annotations

![Global Capture Assistant sidebar shown beside an active desktop workflow](docs/screenshots/sidebar.png)

## Screenshots

### Capture overlay
Region capture overlay with the selection workflow visible.

![Capture overlay showing an on-screen selection region](docs/screenshots/capture-view.png)

### Follow-up chat
Suggested prompts and the chat-more loop after the first analysis.

![Follow-up suggestions and chat controls in the sidebar](docs/screenshots/follow-up-suggestions.png)

### Generate notes
Rendered note-card preview after using `Generate Notes` on the current capture.

![Generated notes preview shown in the sidebar](docs/screenshots/generate%20notes.png)

### Screen annotations
Side-callout annotation overlay with arrows pointing back into the captured screen.

![Annotation overlay with side callouts and arrows](docs/screenshots/annotations.png)

## Why I Built This

Most screenshot-to-LLM workflows are not really slow because of the screenshot itself. The real drag is the context switching that starts right after it: capture something, paste it somewhere else, wait, compare, switch back, then do it again. I wanted a setup where the capture, the answer, and the next useful prompts all lived in one persistent workspace. This app is built around staying in flow while working, not around generic image upload.

## What It Does

- Draggable always-on-top floating capture button
- Global hotkey: `Ctrl + Shift + Q`
- Tray icon with `Capture Now` and `Show Sidebar` actions
- Region selection overlay for captures
- Active-window context attached to captures when available
- Result sidebar with Markdown rendering
- Suggested follow-up prompts after each analysis
- Manual chat input for continuing analysis on the same capture
- Switchable text-analysis provider: Gemini or Groq
- Switchable annotation provider: Gemini or Groq
- `Annotate Screen` action that opens a visual overlay with side callouts, arrows, highlight boxes, and explanation cards
- `Generate Notes` action that turns the current capture into a styled note card
- HTML/CSS note card rendered in-app and copied to the clipboard as a PNG
- In-app preview of the generated notes card image
- Sidebar automatically hides during region capture and returns after capture completes
- Retry support after failures
- Gemini model selection, Groq model selection, and thinking level settings
- Optional auto-start and focus-sidebar behavior
- API keys stored locally with Windows DPAPI
- Local logging for diagnostics

## How It Works

1. Trigger a capture from the floating button, tray icon, or global hotkey.
2. Select the part of the screen you want to analyze.
3. The capture is sent to your configured text-analysis provider, either Gemini or Groq.
4. The sidebar shows the response, generates suggested follow-up prompts, and lets you continue the conversation without recapturing.
5. Click `Annotate Screen` if you want a visual overlay with side callouts and arrows that point back into the captured image.
6. Click `Generate Notes` if you want Gemini to produce a visual note card that is rendered in-app and copied to your clipboard as a PNG.

## Quick Start

Prerequisites:

- Windows 10/11
- Gemini API key for notes or Gemini-powered analysis/annotations
- Groq API key for Groq-powered analysis/annotations
- Microsoft Edge WebView2 Runtime
- `.NET 8 SDK` if you are building from source

Download a release:

- If you do not want to build from source, download `GlobalCaptureAssistant-1.0.1-win-x64.zip` from the [GitHub releases page](https://github.com/IshanKarnick/Global-Capture-Assistant/releases/).
- Extract the ZIP and run `GlobalCaptureAssistant.exe`.

Run from source:

```powershell
dotnet restore
dotnet build src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
dotnet run --project src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
```

## First Run

- The app prompts for the API key required by the provider you selected.
- Keys are stored locally with Windows DPAPI in `%AppData%\GlobalCaptureAssistant\settings.json`.
- The sidebar settings let you choose Gemini or Groq separately for text analysis and annotations.
- The app supports auto-start and focus-sidebar behavior by default, and both can be changed from the sidebar settings.

## Daily Workflow

1. Start a capture from the floating button, the tray icon, or `Ctrl + Shift + Q`.
2. Drag over the part of the screen you want to analyze.
3. Read the response in the sidebar while keeping your original work visible.
4. Click a suggested prompt or type your own follow-up question.
5. Click `Annotate Screen` if you want a side-callout overlay that visually explains the capture.
6. Click `Generate Notes` if you want the current capture turned into a styled note card image.
7. Paste the generated PNG directly into OneNote or another app, or keep using the same capture conversation without taking a new screenshot unless the context changes.

## Build From Source

The main project lives at `src\GlobalCaptureAssistant`.

```powershell
dotnet restore
dotnet build src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
```

## Package a Local Release

Use the packaging script to produce a local ZIP release from the current source tree.

```powershell
.\scripts\package-release.ps1
```

Optional examples:

```powershell
.\scripts\package-release.ps1 -Version 1.0.1 -Runtime win-x64
.\scripts\package-release.ps1 -Version 1.0.1 -FrameworkDependent
```

Packaging notes:

- Default runtime is `win-x64`
- Self-contained single-file publish is the default
- `-FrameworkDependent` switches packaging to framework-dependent output
- Release artifacts are written to `artifacts\release\`

Expected output:

- `artifacts\release\GlobalCaptureAssistant-<version>-win-x64.zip`
- `artifacts\release\GlobalCaptureAssistant-<version>-win-x64.zip.sha256`

## Configuration and Storage

- Settings file: `%AppData%\GlobalCaptureAssistant\settings.json`
- Logs directory: `%LocalAppData%\GlobalCaptureAssistant\logs\`
- Configurable options currently include:
  - text-analysis provider
  - annotation provider
  - selected Gemini model
  - selected Groq model
  - thinking level
  - auto-start
  - focus sidebar after capture
- Gemini and Groq API keys are stored locally with Windows DPAPI.

## Troubleshooting and Current Limitations

- If the global hotkey is unavailable, use the floating button or tray menu.
- If the API key for the selected provider is missing, the related feature cannot run.
- `Generate Notes` depends on WebView2 being available on the machine.
- Groq vision requests may downscale or recompress large captures to fit Groq's image limits.
- Internet access is required for model calls.
- This app is currently Windows-only.

## Project Structure

- `src/GlobalCaptureAssistant` - app source
- `scripts/package-release.ps1` - local release packaging
- `docs/screenshots` - README image assets

## License

Licensed under Apache-2.0. See [LICENSE](LICENSE).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).
