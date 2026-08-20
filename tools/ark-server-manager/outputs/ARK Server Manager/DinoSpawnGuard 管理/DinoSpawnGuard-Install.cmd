@echo off
chcp 65001 >nul
title DinoSpawnGuard インストール

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

set "ROOT=%~dp0..\..\.."
set "SOURCE_DLL=%ROOT%\build\DinoSpawnGuard.dll"
set "SOURCE_META=%ROOT%\src\DinoSpawnGuard"
set "PLUGIN=D:\arkserver\ShooterGame\Binaries\Win64\ArkApi\Plugins\DinoSpawnGuard"
set "BACKUP=%~dp0Backups\before-install"

if not exist "%SOURCE_DLL%" (
  echo.
  echo ビルド済みDLLが見つかりません: %SOURCE_DLL%
  echo.
  pause
  exit /b 1
)

if exist "%PLUGIN%\DinoSpawnGuard.dll" (
  if not exist "%BACKUP%" mkdir "%BACKUP%"
  copy /Y "%PLUGIN%\DinoSpawnGuard.dll" "%BACKUP%\DinoSpawnGuard.dll" >nul
  if exist "%PLUGIN%\PluginInfo.json" copy /Y "%PLUGIN%\PluginInfo.json" "%BACKUP%\PluginInfo.json" >nul
  if exist "%PLUGIN%\config.json" copy /Y "%PLUGIN%\config.json" "%BACKUP%\config.json" >nul
)

if not exist "%PLUGIN%" mkdir "%PLUGIN%"
copy /Y "%SOURCE_DLL%" "%PLUGIN%\DinoSpawnGuard.dll" >nul
copy /Y "%SOURCE_META%\PluginInfo.json" "%PLUGIN%\PluginInfo.json" >nul
copy /Y "%SOURCE_META%\config.json" "%PLUGIN%\config.json" >nul
copy /Y "%SOURCE_META%\README.txt" "%PLUGIN%\README.txt" >nul

echo.
echo DinoSpawnGuard v1.0を配置しました。次回のARKサーバー起動時から有効です。
echo 配置先: %PLUGIN%
echo.
pause
