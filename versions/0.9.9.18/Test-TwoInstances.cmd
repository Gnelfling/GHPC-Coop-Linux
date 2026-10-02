@echo off
rem Double-click: build the patched mod and start a host and a guest copy of GHPC.
rem Steam must be running. Optional: Test-TwoInstances.cmd -AutoDrive
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-TwoInstances.ps1" %*
echo.
pause
