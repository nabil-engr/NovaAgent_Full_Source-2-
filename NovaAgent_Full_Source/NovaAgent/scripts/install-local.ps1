param(
    [string]$PublishedFolder = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($PublishedFolder)) {
    $PublishedFolder = Join-Path $ProjectRoot "publish\win-x64"
}

$Exe = Join-Path $PublishedFolder "NovaAgent.exe"

if (-not (Test-Path $Exe)) {
    throw "NovaAgent.exe not found. Run scripts\setup-and-publish.ps1 first."
}

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\NovaAgent"

if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item (Join-Path $PublishedFolder "*") $InstallDir -Recurse -Force

$InstalledExe = Join-Path $InstallDir "NovaAgent.exe"

$Shell = New-Object -ComObject WScript.Shell

$DesktopShortcut = $Shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "Nova Agent.lnk"))
$DesktopShortcut.TargetPath = $InstalledExe
$DesktopShortcut.WorkingDirectory = $InstallDir
$DesktopShortcut.Description = "Nova local Windows voice agent"
$DesktopShortcut.Save()

$StartMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$StartShortcut = $Shell.CreateShortcut((Join-Path $StartMenuDir "Nova Agent.lnk"))
$StartShortcut.TargetPath = $InstalledExe
$StartShortcut.WorkingDirectory = $InstallDir
$StartShortcut.Description = "Nova local Windows voice agent"
$StartShortcut.Save()

Write-Host "Installed Nova Agent to:" -ForegroundColor Green
Write-Host $InstallDir
Write-Host "Desktop and Start Menu shortcuts created."

Start-Process $InstalledExe
