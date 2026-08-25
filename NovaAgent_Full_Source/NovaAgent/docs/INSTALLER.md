# Professional Windows installer

Nova Agent has two separate distribution formats:

- `Setup.exe` for a normal Windows install, upgrade, repair, and uninstall experience.
- A portable ZIP for users who do not want to install the app.

## Build the normal installer

1. Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) from its official site.
2. Prepare the local voice runtime once:

   ```powershell
   .\scripts\setup-whisper.ps1 -Model base
   ```

3. Build the signed-ready release:

   ```powershell
   .\scripts\build-installer.ps1
   ```

The result is written to `publish\installer\NovaAgent-Setup-VERSION-win-x64.exe` together with a SHA-256 checksum and `release-manifest.json`.

## Installer behaviour

- Installs per-user by default and does not require Administrator access.
- Supports a custom install directory and optional elevation.
- Creates Start Menu entries and an optional desktop shortcut.
- Can optionally start Nova Agent with Windows, minimized to the tray.
- Detects a running Nova Agent before upgrade or uninstall.
- Uses the same stable application ID for in-place upgrades.
- Registers a normal Windows **Apps > Installed apps** uninstall entry.
- Preserves settings, history, and logs during upgrades and uninstall.
- Removes disposable microphone buffers during uninstall.

## Code signing

For public distribution, use a trusted Windows code-signing certificate:

```powershell
.\scripts\build-installer.ps1 -CertificateThumbprint "YOUR_CERTIFICATE_THUMBPRINT"
```

The script signs both `NovaAgent.exe` and the final installer with SHA-256 and a timestamp. Never publish a release when `verify-release.ps1` reports a hash mismatch.

## Verify a release

```powershell
.\scripts\verify-release.ps1
```

Test the installer on a clean Windows 10 and Windows 11 virtual machine before public release. Test install, upgrade from the previous version, auto-start, uninstall, and preservation of user settings.

For a non-interactive local install/app/uninstall smoke test inside the repository's `.tmp` folder, run:

```powershell
.\scripts\test-installer.ps1
```
