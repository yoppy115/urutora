@echo off
chcp 65001 >nul
title DinoSpawnGuard 緊急無効化

fltmc >nul 2>&1
if errorlevel 1 (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

tasklist /FI "IMAGENAME eq ShooterGameServer.exe" /NH | find /I "ShooterGameServer.exe" >nul
if not errorlevel 1 (
  echo.
  echo ARKサーバーが起動中です。管理アプリから安全に停止してから、もう一度実行してください。
  echo 稼働中に止めるだけならRCONで DinoSpawnGuard.Pause を実行できます。
  echo.
  pause
  exit /b 2
)

set "PLUGIN=D:\arkserver\ShooterGame\Binaries\Win64\ArkApi\Plugins\DinoSpawnGuard"
set "BACKUP=%PLUGIN%\EmergencyBackup"

if exist "%PLUGIN%\DinoSpawnGuard.dll.disabled" (
  echo.
  echo DinoSpawnGuardはすでに無効です。
  echo.
  pause
  exit /b 0
)

if not exist "%PLUGIN%\DinoSpawnGuard.dll" (
  echo.
  echo DinoSpawnGuard.dllが見つかりません。
  echo %PLUGIN%
  echo.
  pause
  exit /b 1
)

if not exist "%BACKUP%" mkdir "%BACKUP%"
copy /Y "%PLUGIN%\DinoSpawnGuard.dll" "%BACKUP%\DinoSpawnGuard.dll" >nul
if exist "%PLUGIN%\PluginInfo.json" copy /Y "%PLUGIN%\PluginInfo.json" "%BACKUP%\PluginInfo.json" >nul
if exist "%PLUGIN%\config.json" copy /Y "%PLUGIN%\config.json" "%BACKUP%\config.json" >nul
ren "%PLUGIN%\DinoSpawnGuard.dll" "DinoSpawnGuard.dll.disabled"

echo.
echo DinoSpawnGuardを無効化しました。次回のARKサーバー起動時から読み込まれません。
echo バックアップ: %BACKUP%
echo.
pause
