param(
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PublishFolder = Join-Path $ProjectRoot "publish\win-x64"

& "$PSScriptRoot\publish-win64.ps1"

$WhisperServer = Join-Path $PublishFolder "runtime\whisper\whisper-server.exe"
$WhisperModel = Join-Path $PublishFolder "runtime\whisper\ggml-base.bin"
if (-not (Test-Path -LiteralPath $WhisperServer) -or -not (Test-Path -LiteralPath $WhisperModel)) {
    throw "Portable voice package was not created because the Whisper runtime/model is missing. Run scripts\setup-whisper.ps1 first."
}

if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $ProjectRoot "publish\NovaAgent-win-x64-portable.zip"
}

if (Test-Path -LiteralPath $Destination) {
    Remove-Item -LiteralPath $Destination -Force
}

Compress-Archive -Path (Join-Path $PublishFolder "*") -DestinationPath $Destination -CompressionLevel Optimal
Write-Host "Portable package created:" -ForegroundColor Green
Write-Host $Destination
