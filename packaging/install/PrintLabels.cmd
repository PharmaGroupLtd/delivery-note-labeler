@echo off
setlocal EnableExtensions
powershell.exe -NoProfile -NoLogo -ExecutionPolicy Bypass -File "%~dp0PrintLabels.ps1" %*
exit /b %ERRORLEVEL%
