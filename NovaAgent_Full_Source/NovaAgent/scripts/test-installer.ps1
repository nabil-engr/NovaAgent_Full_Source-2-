param(
    [string]$InstallerFolder = "",
    [string]$TestInstallDirectory = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($InstallerFolder)) {
    $InstallerFolder = Join-Path $ProjectRoot "publish\installer"
}
if ([string]::IsNullOrWhiteSpace($TestInstallDirectory)) {
    $TestInstallDirectory = Join-Path $ProjectRoot ".tmp\installer-smoke"
}

$AllowedRoot = [IO.Path]::GetFullPath((Join-Path $ProjectRoot ".tmp"))
$ResolvedTarget = [IO.Path]::GetFullPath($TestInstallDirectory)
if (-not $ResolvedTarget.StartsWith(
        $AllowedRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Test install directory must stay inside $AllowedRoot"
}
if (Test-Path -LiteralPath $ResolvedTarget) {
    throw "Refusing to overwrite an existing test directory: $ResolvedTarget"
}

$ManifestPath = Join-Path $InstallerFolder "release-manifest.json"
$Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$Setup = Join-Path $InstallerFolder $Manifest.setup.file
if (-not (Test-Path -LiteralPath $Setup -PathType Leaf)) { throw "Missing installer: $Setup" }

$SetupArguments = @(
    "/CURRENTUSER",
    "/VERYSILENT",
    "/SUPPRESSMSGBOXES",
    "/NORESTART",
    "/SP-",
    "/NOICONS",
    "/DIR=`"$ResolvedTarget`""
)

$Uninstaller = Join-Path $ResolvedTarget "unins000.exe"
try {
    $InstallProcess = Start-Process -FilePath $Setup -ArgumentList $SetupArguments `
        -WindowStyle Hidden -Wait -PassThru
    if ($InstallProcess.ExitCode -ne 0) {
        throw "Silent install failed with exit code $($InstallProcess.ExitCode)."
    }

    $Required = @(
        (Join-Path $ResolvedTarget "NovaAgent.exe"),
        (Join-Path $ResolvedTarget "runtime\whisper\whisper-server.exe"),
        (Join-Path $ResolvedTarget "runtime\whisper\ggml-base.bin"),
        $Uninstaller
    )
    foreach ($file in $Required) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Installed payload is incomplete: $file"
        }
    }

    & "$PSScriptRoot\smoke-test.ps1" -PublishedFolder $ResolvedTarget
    Write-Host "Silent installer payload: PASS" -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $Uninstaller -PathType Leaf) {
        $UninstallProcess = Start-Process -FilePath $Uninstaller `
            -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" `
            -WindowStyle Hidden -Wait -PassThru
        if ($UninstallProcess.ExitCode -ne 0) {
            throw "Silent uninstall failed with exit code $($UninstallProcess.ExitCode)."
        }
    }
}

for ($attempt = 0; $attempt -lt 20 -and (Test-Path -LiteralPath $ResolvedTarget); $attempt++) {
    Start-Sleep -Milliseconds 250
}
if (Test-Path -LiteralPath $ResolvedTarget) {
    throw "Uninstall left the test installation directory behind: $ResolvedTarget"
}

Write-Host "Silent uninstall cleanup: PASS" -ForegroundColor Green
