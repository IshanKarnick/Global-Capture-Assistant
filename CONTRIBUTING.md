# Contributing

Contributions are welcome. This project is open to bug fixes, usability polish, and feature improvements.

## Before You Start

- For small fixes, direct pull requests are fine.
- For larger features, UX changes, or architectural changes, open an issue first so the scope can be discussed before work starts.
- Keep proposals concrete and narrowly scoped.

## Development Setup

The main app project lives at `src/GlobalCaptureAssistant`.

This project currently targets Windows for both development and runtime.

```powershell
dotnet restore
dotnet build src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
dotnet run --project src\GlobalCaptureAssistant\GlobalCaptureAssistant.csproj -c Debug
```

## Pull Request Guidelines

- Keep pull requests focused.
- Avoid bundling unrelated changes into the same PR.
- Match the existing code style and naming patterns.
- Update documentation when behavior or workflow changes.
- Include screenshots for visible UI changes when relevant.
- Explain the user-facing impact in the PR description.
- If you are fixing a bug, describe how to reproduce it and how you validated the fix.

## Issues

Issues are useful for:

- reproducible bugs
- unclear behavior
- usability problems
- feature requests

When opening an issue, include:

- what you expected
- what happened instead
- steps to reproduce
- screenshots when relevant
- environment details if applicable

## What to Avoid

- Do not submit sweeping refactors without prior discussion.
- Do not add unrelated cleanup to a focused PR.
- Do not introduce new dependencies without clear justification.

## License for Contributions

By submitting code or documentation to this project, you agree that your contributions will be licensed under Apache-2.0 for this project.
