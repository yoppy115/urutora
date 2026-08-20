@echo off
chcp 65001 >nul
title ResourceProbe v1.3へロールバック

fltmc >nul 2>&1
if errorlevel 1 (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

tasklist /FI "IMAGENAME eq ShooterGameServer.exe" /NH | find /I "ShooterGameServer.exe" >nul
if not errorlevel 1 (
  echo.
  echo ARKサーバーが起動中です。安全に停止してから、もう一度実行してください。
  echo.
  pause
  exit /b 2
)

tasklist /FI "IMAGENAME eq ARK Server Manager.exe" /NH | find /I "ARK Server Manager.exe" >nul
if not errorlevel 1 (
  echo.
  echo ARK Server Managerが起動中です。アプリを閉じてから、もう一度実行してください。
  echo.
  pause
  exit /b 2
)

set "BACKUP=%~dp0Backups\2026-08-11_ResourceProbe-v1.3"
set "PLUGIN=D:\arkserver\ShooterGame\Binaries\Win64\ArkApi\Plugins\ResourceProbe"

if not exist "%BACKUP%\ResourceProbe v1.3.dll" (
  echo.
  echo v1.3バックアップが見つかりません。
  echo %BACKUP%
  echo.
  pause
  exit /b 1
)

copy /Y "%BACKUP%\ARK Server Manager v1.13.0.exe" "%~dp0..\ARK Server Manager.exe" >nul
copy /Y "%BACKUP%\ResourceProbe v1.3.dll" "%PLUGIN%\ResourceProbe.dll" >nul
copy /Y "%BACKUP%\PluginInfo v1.3.json" "%PLUGIN%\PluginInfo.json" >nul
if exist "%PLUGIN%\ResourceProbe.dll.disabled" del /Q "%PLUGIN%\ResourceProbe.dll.disabled"

echo.
echo ARK Server Manager v1.13.0 / ResourceProbe v1.3へ戻しました。
echo.
pause
