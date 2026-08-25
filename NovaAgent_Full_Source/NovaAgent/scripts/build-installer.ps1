param(
    [string]$Version = "",
    [string]$InnoCompiler = "",
    [switch]$SkipPublish,
    [switch]$AllowIncompleteVoiceRuntime,
    [string]$CertificateThumbprint = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "src\NovaAgent\NovaAgent.csproj"
$PublishFolder = Join-Path $ProjectRoot "publish\win-x64"
$InstallerOutput = Join-Path $ProjectRoot "publish\installer"
$InstallerScript = Join-Path $ProjectRoot "installer\NovaAgent.iss"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$ProjectXml = Get-Content -LiteralPath $ProjectFile
    $Version = [string]$ProjectXml.Project.PropertyGroup.Version
}

if ($Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "Version must contain three or four numeric parts (for example 2.1.0)."
}

if (-not $SkipPublish) {
    & "$PSScriptRoot\publish-win64.ps1"
}

$RequiredFiles = @(
    (Join-Path $PublishFolder "NovaAgent.exe"),
    (Join-Path $PublishFolder "runtime\whisper\whisper-server.exe"),
    (Join-Path $PublishFolder "runtime\whisper\ggml-base.bin")
)
$MissingFiles = @($RequiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
if ($MissingFiles.Count -gt 0 -and -not $AllowIncompleteVoiceRuntime) {
    $list = $MissingFiles -join [Environment]::NewLine
    throw "A complete voice installer cannot be built because these files are missing:`n$list`nRun scripts\setup-whisper.ps1 first."
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $Candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $InnoCompiler = $Candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or
    -not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw "Inno Setup 6 compiler was not found. Install Inno Setup 6 from https://jrsoftware.org/isinfo.php, then rerun this script."
}

New-Item -ItemType Directory -Force -Path $InstallerOutput | Out-Null

function Find-SignTool {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (-not (Test-Path -LiteralPath $kits)) { return $null }
    return Get-ChildItem -LiteralPath $kits -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -ExpandProperty FullName -First 1
}

$SignTool = $null
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $SignTool = Find-SignTool
    if (-not $SignTool) { throw "signtool.exe is required when CertificateThumbprint is supplied." }
    & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /td SHA256 /tr $TimestampUrl `
        (Join-Path $PublishFolder "NovaAgent.exe")
    if ($LASTEXITCODE -ne 0) { throw "Application code signing failed." }
}

& $InnoCompiler `
    "/DAppVersion=$Version" `
    "/DSourceDir=$PublishFolder" `
    "/DOutputDir=$InstallerOutput" `
    $InstallerScript
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE." }

$Setup = Join-Path $InstallerOutput "NovaAgent-Setup-$Version-win-x64.exe"
if (-not (Test-Path -LiteralPath $Setup -PathType Leaf)) {
    throw "Installer compiler finished, but the expected Setup.exe was not created."
}

if ($SignTool) {
    & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /td SHA256 /tr $TimestampUrl $Setup
    if ($LASTEXITCODE -ne 0) { throw "Installer code signing failed." }
}

$Hash = Get-FileHash -LiteralPath $Setup -Algorithm SHA256
$ChecksumPath = "$Setup.sha256"
[IO.File]::WriteAllText($ChecksumPath, "$($Hash.Hash.ToLowerInvariant()) *$([IO.Path]::GetFileName($Setup))`n")

$Manifest = [ordered]@{
    product = "Nova Agent"
    version = $Version
    architecture = "win-x64"
    generatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    setup = [ordered]@{
        file = [IO.Path]::GetFileName($Setup)
        bytes = (Get-Item -LiteralPath $Setup).Length
        sha256 = $Hash.Hash.ToLowerInvariant()
        signed = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)
    }
    voiceRuntimeComplete = ($MissingFiles.Count -eq 0)
}
$ManifestPath = Join-Path $InstallerOutput "release-manifest.json"
[IO.File]::WriteAllText(
    $ManifestPath,
    ($Manifest | ConvertTo-Json -Depth 5) + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host "Professional installer created:" -ForegroundColor Green
Write-Host $Setup
Write-Host "SHA-256: $($Hash.Hash)"
Write-Host "Manifest: $ManifestPath"
