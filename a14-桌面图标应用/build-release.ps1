$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

dotnet build .\src\DesktopIconUpgrader\DesktopIconUpgrader.csproj -c Release --configfile .\NuGet.Config
if ($LASTEXITCODE -ne 0) { throw "Release build failed: $LASTEXITCODE" }
dotnet build .\tests\DesktopIconUpgrader.SmokeTests\DesktopIconUpgrader.SmokeTests.csproj -c Release --configfile .\NuGet.Config
if ($LASTEXITCODE -ne 0) { throw "Smoke test build failed: $LASTEXITCODE" }
dotnet run --project .\tests\DesktopIconUpgrader.SmokeTests\DesktopIconUpgrader.SmokeTests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Smoke tests failed: $LASTEXITCODE" }
dotnet publish .\src\DesktopIconUpgrader\DesktopIconUpgrader.csproj -c Release -o .\publish --no-restore --self-contained false
if ($LASTEXITCODE -ne 0) { throw "Publish failed: $LASTEXITCODE" }

Write-Host "Release ready: $root\publish\DesktopIconUpgrader.exe"
