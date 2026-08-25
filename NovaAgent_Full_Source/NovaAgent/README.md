# Nova Agent 2.0

Nova Agent is a private, local Windows voice assistant for Bangla, English, and mixed commands. Speech recognition runs through a local `whisper.cpp` server; no OpenAI API key or ChatGPT subscription is required for the core application.

## Highlights

- Modern WPF dashboard with Assistant, Settings, and Diagnostics views
- Always-listening mode, configurable wake words, and follow-up conversation window
- Local multilingual transcription through `whisper.cpp`
- Selectable microphone, speech rate/volume, and audio tuning
- Context-aware, bounded asynchronous file search that keeps the UI responsive
- App launcher plus user-defined `alias=path` app shortcuts
- Volume, media, window, browser-tab, and common keyboard controls
- Google/YouTube search, URL opening, typing, folders, screenshots, time/date
- Protected confirmation for shutdown, restart, and sleep
- Local command history with clear and CSV export
- Atomic settings, JSON import/export, daily logs, health checks, and single-instance protection
- Tray operation, Windows auto-start, self-contained publish, local install, and portable ZIP scripts
- Professional Setup.exe with in-place upgrades, uninstall, optional shortcuts/auto-start, release checksums, and signing support
- Second-launch activation, silent tray startup, bounded logs/history, stale audio cleanup, and corrupt-settings recovery
- No unrestricted voice-to-PowerShell or voice-to-CMD execution

## Quick start for a development PC

Open PowerShell in this repository and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\check-prerequisites.ps1
.\scripts\setup-and-publish.ps1
```

The first setup builds `whisper.cpp` and downloads a multilingual model, so it takes longer than later builds. The completed self-contained application is placed in:

```text
publish\win-x64\
```

To install it for the current Windows user and create Desktop/Start Menu shortcuts:

```powershell
.\scripts\install-local.ps1
```

For a full step-by-step Bangla guide, including moving development to another PC, see [docs/SETUP-ANOTHER-PC.md](docs/SETUP-ANOTHER-PC.md).

## Requirements for coding/building

- Windows 10 or Windows 11, 64-bit
- .NET 10 SDK
- Visual Studio with `.NET desktop development` (recommended for IDE development)

The repository includes `global.json`, so compatible .NET 10 feature bands are selected consistently across PCs.

## Daily development

Open `NovaAgent.sln` in Visual Studio, or use:

```powershell
.\scripts\build.ps1
dotnet run --project .\src\NovaAgent\NovaAgent.csproj
```

The pinned official prebuilt Whisper runtime only needs to be prepared once unless the model/runtime is removed; a local C++ toolchain is not required:

```powershell
.\scripts\setup-whisper.ps1 -Model base
```

For troubleshooting, `NovaAgent.exe --safe-mode` opens the app without automatically starting continuous listening.

Available model presets are `tiny`, `base`, and `small`. `base` is the practical default; `small` is more accurate but slower and larger.

## Distribute to a PC that will not edit code

### Normal Windows installer (recommended)

After installing Inno Setup 6 and preparing Whisper, run:

```powershell
.\scripts\build-installer.ps1
```

Give the generated `publish\installer\NovaAgent-Setup-VERSION-win-x64.exe` to the user. It installs like a normal Windows application and includes upgrade/uninstall support. See [docs/INSTALLER.md](docs/INSTALLER.md) for signing and release verification.

### Portable ZIP

After Whisper setup, create a portable self-contained ZIP:

```powershell
.\scripts\package-portable.ps1
```

Copy `publish\NovaAgent-win-x64-portable.zip` to the other 64-bit Windows PC, extract it, and run `NovaAgent.exe`. A separate .NET installation is not required for this self-contained package. If `whisper-server.exe` reports a missing native DLL, install the Microsoft Visual C++ 2015–2022 x64 Redistributable on that PC.

## Settings and local data

- Settings: `%APPDATA%\NovaAgent\settings.json`
- History: `%LOCALAPPDATA%\NovaAgent\history.jsonl`
- Logs: `%LOCALAPPDATA%\NovaAgent\Logs\`
- Temporary microphone chunks: `%LOCALAPPDATA%\NovaAgent\Temp\`

Use **Settings → Export settings** before moving to another PC, then **Import settings** there. Absolute app/model paths can differ between PCs; review them after importing.

Custom app example:

```text
discord=C:\Users\YOUR_NAME\AppData\Local\Discord\Update.exe
obs=C:\Program Files\obs-studio\bin\64bit\obs64.exe
```

## Commands and safety

Say `Nova, Downloads folder open`, then follow with `song.mp4 open koro`. Additional examples are in [docs/COMMANDS.md](docs/COMMANDS.md).

Nova uses an explicit allow-list of actions. Shutdown/restart/sleep require a second confirmation, and arbitrary shell commands are intentionally unavailable. See [docs/SECURITY.md](docs/SECURITY.md).

## Project documentation

- [Other-PC setup and development guide](docs/SETUP-ANOTHER-PC.md)
- [Commands](docs/COMMANDS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Security and privacy](docs/SECURITY.md)
- [Manual test checklist](docs/TEST-CHECKLIST.md)
- [Professional installer and release guide](docs/INSTALLER.md)

## Known limits

- Command understanding is deterministic and safety-first, not a general LLM planner.
- Recognition quality depends on the model, microphone, background noise, and pronunciation.
- Always-listening Whisper uses CPU/GPU resources; use **Listen once**, a smaller model, or a longer audio chunk on slower PCs.
- Browser automation is limited to URLs, searches, and explicit keyboard shortcuts; it does not inspect page content.
- A code-signed installer/MSIX is still recommended before public commercial distribution.
