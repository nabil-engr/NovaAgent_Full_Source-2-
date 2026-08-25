param(
    [ValidateSet("tiny","base","small")]
    [string]$Model = "base",
    [string]$RuntimeVersion = "v1.9.2",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Runtime = Join-Path $ProjectRoot "src\NovaAgent\runtime\whisper"
$Downloads = Join-Path $ProjectRoot ".tools\downloads"
$Archive = Join-Path $Downloads "whisper-bin-x64-$RuntimeVersion.zip"
$Extracted = Join-Path $ProjectRoot ".tools\whisper-runtime-$RuntimeVersion"
$ServerTarget = Join-Path $Runtime "whisper-server.exe"

New-Item -ItemType Directory -Force -Path $Runtime | Out-Null
New-Item -ItemType Directory -Force -Path $Downloads | Out-Null

Write-Host "Nova Agent - verified local Whisper setup" -ForegroundColor Cyan

if ($Force -or -not (Test-Path -LiteralPath $ServerTarget -PathType Leaf)) {
    $BinaryUrl = "https://github.com/ggml-org/whisper.cpp/releases/download/$RuntimeVersion/whisper-bin-x64.zip"
    if ($Force -or -not (Test-Path -LiteralPath $Archive -PathType Leaf)) {
        Write-Host "Downloading official whisper.cpp $RuntimeVersion Windows x64 runtime..."
        $PartialArchive = "$Archive.partial"
        Invoke-WebRequest -Uri $BinaryUrl -OutFile $PartialArchive
        if ((Get-Item -LiteralPath $PartialArchive).Length -lt 1MB) {
            throw "The whisper.cpp runtime download is unexpectedly small."
        }
        Move-Item -LiteralPath $PartialArchive -Destination $Archive -Force
    }

    if (Test-Path -LiteralPath $Extracted) {
        $ResolvedTools = (Resolve-Path -LiteralPath (Join-Path $ProjectRoot ".tools")).Path
        $ResolvedExtracted = (Resolve-Path -LiteralPath $Extracted).Path
        if (-not $ResolvedExtracted.StartsWith($ResolvedTools + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean an unexpected extraction directory: $ResolvedExtracted"
        }
        Remove-Item -LiteralPath $ResolvedExtracted -Recurse -Force
    }

    Expand-Archive -LiteralPath $Archive -DestinationPath $Extracted -Force
    $Server = Get-ChildItem -LiteralPath $Extracted -Filter "whisper-server.exe" -Recurse |
        Select-Object -First 1
    if (-not $Server) {
        throw "The official runtime archive did not contain whisper-server.exe."
    }

    Copy-Item -LiteralPath $Server.FullName -Destination $ServerTarget -Force
    Get-ChildItem -LiteralPath $Server.Directory.FullName -Filter "*.dll" -File |
        Copy-Item -Destination $Runtime -Force

    $LicenseUrl = "https://raw.githubusercontent.com/ggml-org/whisper.cpp/$RuntimeVersion/LICENSE"
    Invoke-WebRequest -Uri $LicenseUrl -OutFile (Join-Path $Runtime "whisper.cpp-LICENSE.txt")
}

$ModelFile = "ggml-$Model.bin"
$ModelDownload = Join-Path $Runtime $ModelFile
if ($Force -or -not (Test-Path -LiteralPath $ModelDownload -PathType Leaf)) {
    $ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$ModelFile"
    Write-Host "Downloading multilingual Whisper model: $ModelFile"
    $PartialModel = "$ModelDownload.partial"
    Invoke-WebRequest -Uri $ModelUrl -OutFile $PartialModel
    if ((Get-Item -LiteralPath $PartialModel).Length -lt 10MB) {
        throw "The Whisper model download is unexpectedly small."
    }
    Move-Item -LiteralPath $PartialModel -Destination $ModelDownload -Force
}

$StableModelPath = Join-Path $Runtime "ggml-base.bin"
if ($Model -ne "base") {
    Copy-Item -LiteralPath $ModelDownload -Destination $StableModelPath -Force
}

$RequiredRuntimeFiles = @(
    $ServerTarget,
    $StableModelPath,
    (Join-Path $Runtime "whisper.dll")
)
$Missing = @($RequiredRuntimeFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
if ($Missing.Count -gt 0) {
    throw "Whisper setup is incomplete. Missing: $($Missing -join ', ')"
}

Write-Host ""
Write-Host "Whisper setup complete." -ForegroundColor Green
Write-Host "Runtime: $Runtime"
Write-Host "Runtime version: $RuntimeVersion"
Write-Host "Model: $Model"
