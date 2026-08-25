$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $ProjectRoot "src\NovaAgent\NovaAgent.csproj"
$Out = Join-Path $ProjectRoot "publish\win-x64"

if (Test-Path $Out) {
    Remove-Item $Out -Recurse -Force
}

dotnet restore $Project
dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -o $Out

Write-Host ""
Write-Host "Published Nova Agent to:" -ForegroundColor Green
Write-Host $Out
Write-Host "Run NovaAgent.exe"

$WhisperServer = Join-Path $Out "runtime\whisper\whisper-server.exe"
$WhisperModel = Join-Path $Out "runtime\whisper\ggml-base.bin"
if (-not (Test-Path -LiteralPath $WhisperServer) -or -not (Test-Path -LiteralPath $WhisperModel)) {
    Write-Warning "The app was published, but the Whisper runtime is incomplete. Run scripts\setup-whisper.ps1, then publish again for a fully portable voice build."
}
