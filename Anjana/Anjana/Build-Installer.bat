@echo off
title Building Anjana installer
echo Building Anjana-Setup.exe - this takes a minute or two...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 (
  echo.
  echo Build failed. Copy the messages above and send them over.
  pause
  exit /b 1
)
start "" "%~dp0dist"
echo.
echo Done. Your installer is in the "dist" folder: Anjana-Setup.exe
pause
