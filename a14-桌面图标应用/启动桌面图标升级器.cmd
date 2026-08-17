@echo off
setlocal
cd /d "%~dp0"
if exist "publish\DesktopIconUpgrader.exe" (
  start "" "publish\DesktopIconUpgrader.exe"
) else (
  dotnet run --project "src\DesktopIconUpgrader\DesktopIconUpgrader.csproj" -c Release
)
