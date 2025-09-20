@echo off
REM Batch file to regenerate system_image.json
powershell -ExecutionPolicy Bypass -File "%~dp0regenerate-system-image.ps1" %*