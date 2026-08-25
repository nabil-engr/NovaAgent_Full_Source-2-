param(
    [string]$InstallerFolder = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($InstallerFolder)) {
    $InstallerFolder = Join-Path $ProjectRoot "publish\installer"
}

$ManifestPath = Join-Path $InstallerFolder "release-manifest.json"
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Release manifest not found: $ManifestPath"
}

$Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$SetupPath = Join-Path $InstallerFolder $Manifest.setup.file
if (-not (Test-Path -LiteralPath $SetupPath -PathType Leaf)) {
    throw "Installer not found: $SetupPath"
}

$Actual = (Get-FileHash -LiteralPath $SetupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$Expected = ([string]$Manifest.setup.sha256).ToLowerInvariant()
if ($Actual -ne $Expected) {
    throw "Installer integrity check failed. Expected $Expected but found $Actual."
}

$Signature = Get-AuthenticodeSignature -LiteralPath $SetupPath
Write-Host "Release verified." -ForegroundColor Green
Write-Host "File: $SetupPath"
Write-Host "Version: $($Manifest.version)"
Write-Host "SHA-256: $Actual"
Write-Host "Signature: $($Signature.Status)"
