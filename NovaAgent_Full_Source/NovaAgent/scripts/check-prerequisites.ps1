$ErrorActionPreference = "Stop"

Write-Host "Nova Agent - development environment check" -ForegroundColor Cyan
$Issues = [System.Collections.Generic.List[string]]::new()

$Dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $Dotnet) {
    $Issues.Add(".NET 10 SDK is not installed or dotnet is not on PATH.")
}
else {
    $Sdks = dotnet --list-sdks
    if (-not ($Sdks -match '^10\.')) {
        $Issues.Add(".NET 10 SDK is required.")
    }
}

if ($Issues.Count -gt 0) {
    Write-Host "Environment needs attention:" -ForegroundColor Yellow
    $Issues | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "Core prerequisites are available." -ForegroundColor Green
Write-Host "Whisper uses the pinned official prebuilt Windows x64 release; CMake is not required."
Write-Host "Next: .\scripts\setup-and-publish.ps1"
