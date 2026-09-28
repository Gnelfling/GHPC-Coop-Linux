@echo off
setlocal
title GHPC Co-op Automatic Setup
cd /d "%~dp0"
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Setup.ps1"
if errorlevel 1 echo Setup failed. Read the message above.
pause
