@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Finalize-GitBaseline.Admin.ps1" -Publish %*
exit /b %ERRORLEVEL%
