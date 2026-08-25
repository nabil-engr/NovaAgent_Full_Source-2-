$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
dotnet restore (Join-Path $ProjectRoot "NovaAgent.sln")
dotnet build (Join-Path $ProjectRoot "NovaAgent.sln") -c Release
