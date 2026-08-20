@echo off
chcp 65001 >nul
title DinoSpawnGuard 再有効化

fltmc >nul 2>&1
if errorlevel 1 (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

tasklist /FI "IMAGENAME eq ShooterGameServer.exe" /NH | find /I "ShooterGameServer.exe" >nul
if not errorlevel 1 (
  echo.
  echo ARKサーバーが起動中です。管理アプリから安全に停止してから、もう一度実行してください。
  echo.
  pause
  exit /b 2
)

set "PLUGIN=D:\arkserver\ShooterGame\Binaries\Win64\ArkApi\Plugins\DinoSpawnGuard"

if exist "%PLUGIN%\DinoSpawnGuard.dll" (
  echo.
  echo DinoSpawnGuardはすでに有効です。
  echo.
  pause
  exit /b 0
)

if not exist "%PLUGIN%\DinoSpawnGuard.dll.disabled" (
  echo.
  echo 無効化されたDinoSpawnGuard.dllが見つかりません。
  echo %PLUGIN%
  echo.
  pause
  exit /b 1
)

ren "%PLUGIN%\DinoSpawnGuard.dll.disabled" "DinoSpawnGuard.dll"

echo.
echo DinoSpawnGuardを再有効化しました。次回のARKサーバー起動時から読み込まれます。
echo.
pause
