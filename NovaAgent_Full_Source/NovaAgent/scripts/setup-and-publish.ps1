param(
    [ValidateSet("tiny","base","small")]
    [string]$Model = "base"
)

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "       Nova Agent - Setup & Publish      " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$Missing = @()

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $Missing += ".NET 10 SDK" }
if ($Missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing prerequisites:" -ForegroundColor Yellow
    $Missing | ForEach-Object { Write-Host " - $_" }
    Write-Host ""
    throw "Install the missing prerequisites, reopen PowerShell, then run this script again."
}

Write-Host ""
Write-Host "[1/3] Preparing local Whisper..." -ForegroundColor Cyan
& "$PSScriptRoot\setup-whisper.ps1" -Model $Model

Write-Host ""
Write-Host "[2/3] Building Nova Agent..." -ForegroundColor Cyan
& "$PSScriptRoot\build.ps1"

Write-Host ""
Write-Host "[3/3] Creating self-contained Windows build..." -ForegroundColor Cyan
& "$PSScriptRoot\publish-win64.ps1"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Publish = Join-Path $ProjectRoot "publish\win-x64"

Write-Host ""
Write-Host "Nova Agent is ready." -ForegroundColor Green
Write-Host "Open:" $Publish
Write-Host "Run: NovaAgent.exe"
