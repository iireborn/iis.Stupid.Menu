@echo off
setlocal enabledelayedexpansion
title ii.menu Installer
color 0e

set "steamPath1=C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag"
set "steamPath2=D:\SteamLibrary\steamapps\common\Gorilla Tag"
set "steamPath3=C:\Program Files\Oculus\Software\Software\another-axiom-gorilla-tag"
set "steamPath4=D:\Steam\steamapps\common\Gorilla Tag"

if exist "%steamPath1%" ( set "gamePath=%steamPath1%" & goto :gotpath )
if exist "%steamPath2%" ( set "gamePath=%steamPath2%" & goto :gotpath )
if exist "%steamPath3%" ( set "gamePath=%steamPath3%" & goto :gotpath )
if exist "%steamPath4%" ( set "gamePath=%steamPath4%" & goto :gotpath )

color 0c
set /p "gamePath=Gorilla Tag not found. Enter path manually: "
set "gamePath=%gamePath:"=%"
if not exist "%gamePath%" (
    echo Invalid path.
    pause
    exit /b
)

:gotpath
cls
title ii.menu Installer -- Downloading BepInEx
color 0e
echo.
echo  ii.menu Installer
echo  github.com/iireborn/iis.Stupid.Menu
echo.
echo  Downloading BepInEx...
echo.

curl -L -f -# "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.4/BepInEx_win_x64_5.4.23.4.zip" -o "BPNX54234.zip"

if errorlevel 1 (
    color 0c
    echo Failed to download BepInEx. Check your internet connection.
    pause
    exit /b
)

echo.
echo  Extracting BepInEx...
powershell -command "$ErrorActionPreference = 'SilentlyContinue'; Expand-Archive -Path 'BPNX54234.zip' -DestinationPath '%gamePath%' -Force 2>$null" >nul 2>&1
del "BPNX54234.zip" >nul 2>&1

if not exist "%gamePath%\BepInEx\config" mkdir "%gamePath%\BepInEx\config"
if not exist "%gamePath%\BepInEx\plugins" mkdir "%gamePath%\BepInEx\plugins"

cls
title ii.menu Installer -- Downloading menu
echo.
echo  ii.menu Installer
echo  github.com/iireborn/iis.Stupid.Menu
echo.
echo  Downloading ii.menu...
echo.

curl -L -f -# "https://github.com/iireborn/iis.Stupid.Menu/releases/latest/download/ii.s.Stupid.Menu.dll" -o "iimenu_temp.dll"

if errorlevel 1 (
    color 0c
    echo Failed to download ii.menu. Check your internet connection.
    pause
    exit /b
)

move /y "iimenu_temp.dll" "%gamePath%\BepInEx\plugins\ii.s.Stupid.Menu.dll" >nul

if errorlevel 1 (
    color 0c
    echo Failed to move dll into plugins folder.
    pause
    exit /b
)

cls
title ii.menu Installer -- Done!
echo.
echo  ii.menu Installer
echo  github.com/iireborn/iis.Stupid.Menu
echo.
echo  Done! ii.menu is installed.
echo.
echo  Launch Gorilla Tag and the menu will load automatically.
echo.
pause
