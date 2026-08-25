param(
    [string]$PublishedFolder = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PublishedFolder)) {
    $PublishedFolder = Join-Path $ProjectRoot "publish\win-x64"
}

$Exe = Join-Path $PublishedFolder "NovaAgent.exe"
$WhisperServer = Join-Path $PublishedFolder "runtime\whisper\whisper-server.exe"
if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) { throw "Missing application: $Exe" }
if (-not (Test-Path -LiteralPath $WhisperServer -PathType Leaf)) { throw "Missing voice server: $WhisperServer" }

$Existing = @(Get-Process -Name NovaAgent -ErrorAction SilentlyContinue)
if ($Existing.Count -gt 0) {
    throw "Close the running Nova Agent instance before running the smoke test."
}

$WhisperStart = New-Object System.Diagnostics.ProcessStartInfo
$WhisperStart.FileName = $WhisperServer
$WhisperStart.Arguments = "--help"
$WhisperStart.UseShellExecute = $false
$WhisperStart.CreateNoWindow = $true
$WhisperStart.RedirectStandardOutput = $true
$WhisperStart.RedirectStandardError = $true
$WhisperProcess = [Diagnostics.Process]::Start($WhisperStart)
$StandardOutput = $WhisperProcess.StandardOutput.ReadToEndAsync()
$StandardError = $WhisperProcess.StandardError.ReadToEndAsync()
$WhisperProcess.WaitForExit()
$WhisperOutput = $StandardOutput.Result + $StandardError.Result
if ($WhisperProcess.ExitCode -ne 0 -or -not ($WhisperOutput -match "whisper")) {
    throw "whisper-server.exe did not start correctly."
}
$WhisperProcess.Dispose()
Write-Host "Whisper runtime startup: PASS" -ForegroundColor Green

$Primary = Start-Process -FilePath $Exe -ArgumentList "--safe-mode", "--minimized" `
    -WorkingDirectory $PublishedFolder -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 5
    $Primary.Refresh()
    if ($Primary.HasExited) {
        throw "Nova Agent exited unexpectedly with code $($Primary.ExitCode)."
    }

    $Second = Start-Process -FilePath $Exe -ArgumentList "--safe-mode", "--minimized" `
        -WorkingDirectory $PublishedFolder -WindowStyle Hidden -PassThru
    if (-not $Second.WaitForExit(5000)) {
        Stop-Process -Id $Second.Id -Force
        throw "The second-instance guard did not close the duplicate process."
    }
    if ($Second.ExitCode -ne 0) {
        throw "The duplicate process returned exit code $($Second.ExitCode)."
    }

    $Primary.Refresh()
    if ($Primary.HasExited) { throw "The primary process exited during activation testing." }
    Write-Host "Application startup: PASS" -ForegroundColor Green
    Write-Host "Single-instance activation: PASS" -ForegroundColor Green
}
finally {
    $Primary.Refresh()
    if (-not $Primary.HasExited) {
        Stop-Process -Id $Primary.Id -Force
        $Primary.WaitForExit(5000) | Out-Null
    }
}
