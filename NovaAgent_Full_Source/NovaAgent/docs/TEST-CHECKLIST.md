# Manual test checklist

## First run
- [ ] A second Nova process activates the existing tray instance instead of opening a duplicate.
- [ ] App launches without Administrator privileges.
- [ ] Settings file is created.
- [ ] First launch does not start continuous microphone capture until the user enables it.
- [ ] Tray icon appears.
- [ ] Start/Stop listening works.
- [ ] `whisper-server.exe` remains bound to localhost only.

## Voice
- [ ] "Nova" wakes assistant.
- [ ] "Nova, Downloads folder open" opens Downloads.
- [ ] Follow-up "song.mp4 open" uses the remembered folder.
- [ ] Voice reply is not re-triggered as a new command.
- [ ] Quiet audio is ignored.
- [ ] The selected microphone is used after Save/restart.

## Media
- [ ] Set volume to 30.
- [ ] Volume up/down.
- [ ] Mute toggle.
- [ ] Play/pause.
- [ ] Next/previous.

## Apps
- [ ] Chrome.
- [ ] Edge.
- [ ] VS Code.
- [ ] Calculator.
- [ ] Notepad.
- [ ] A valid custom `alias=path` app launches.
- [ ] An invalid custom app path reports a safe failure.

## Shortcuts
- [ ] Copy/paste targets the foreground app.
- [ ] New tab/close tab targets the foreground browser.
- [ ] Browser back/forward and refresh work.

## Safety
- [ ] Shutdown asks for confirmation.
- [ ] "cancel" cancels pending action.
- [ ] Confirmation expires after ~30 seconds.
- [ ] No arbitrary shell command is exposed.

## Tray / startup
- [ ] Closing the window sends to tray.
- [ ] Exit from tray actually terminates Nova/Whisper.
- [ ] Start with Windows toggle creates/removes HKCU Run entry.

## Data and diagnostics
- [ ] Settings export/import round-trip preserves values.
- [ ] History exports valid CSV and Clear removes local history.
- [ ] Diagnostics reports OS, runtime, model, microphone, disk, settings, and privacy.
- [ ] An intentional recoverable error is written under `%LOCALAPPDATA%\NovaAgent\Logs`.

## Build and portability
- [ ] `scripts\check-prerequisites.ps1` passes on a configured development PC.
- [ ] `scripts\build.ps1` succeeds from the project root.
- [ ] Published output includes `runtime\whisper\whisper-server.exe` and `ggml-base.bin`.
- [ ] The portable ZIP runs on a clean 64-bit Windows test account without a separate .NET runtime.
- [ ] `scripts\smoke-test.ps1` passes Whisper startup, app startup, and duplicate-instance activation.

## Installer and upgrade
- [ ] `scripts\build-installer.ps1` creates Setup.exe, a SHA-256 file, and `release-manifest.json`.
- [ ] A standard user can install without an Administrator prompt.
- [ ] Desktop shortcut and start-with-Windows choices are respected.
- [ ] Nova appears in Windows Installed apps and uninstalls normally.
- [ ] An in-place version upgrade preserves settings and command history.
- [ ] `scripts\verify-release.ps1` passes, and a modified installer fails verification.
- [ ] Installer install, upgrade, and uninstall pass on clean Windows 10 and Windows 11 VMs.
- [ ] `scripts\test-installer.ps1` passes a local silent install, installed-app smoke test, and uninstall cleanup.
