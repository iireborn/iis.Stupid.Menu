@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1

title ii's Stupid Menu Installer // [#---------] Getting directory
color 0e

set "steamPath1=C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag"
set "steamPath2=D:\SteamLibrary\steamapps\common\Gorilla Tag"
set "steamPath3=C:\Program Files\Oculus\Software\Software\another-axiom-gorilla-tag"
set "steamPath4=D:\Steam\steamapps\common\Gorilla Tag"

if exist "!steamPath1!" (
    set "gamePath=!steamPath1!"
    goto :gotpath
)
if exist "!steamPath2!" (
    set "gamePath=!steamPath2!"
    goto :gotpath
)
if exist "!steamPath3!" (
    set "gamePath=!steamPath3!"
    goto :gotpath
)
if exist "!steamPath4!" (
    set "gamePath=!steamPath4!"
    goto :gotpath
)

color 0c
set /p gamePath=Gorilla Tag directory not found. Enter it manually:
set gamePath=%gamePath:"=%
if not exist "!gamePath!" (
    echo Invalid directory.
    pause
    exit /b
)

:gotpath
color 0e
cls
title ii's Stupid Menu Installer // [###-------] Downloading BepInEx

echo.
echo   ████████ ██  █████  █████████   ███████████   █   ██████ █  █
echo    ██   ██  █ █    █ █    ██    █    ██   █  █  ██ ██ █    ██ █
echo    ██   ██  █ █    █ ████ ██    ████ ██   █  █  █ █ █ ███  █ ██
echo    ██   ██  █ █    █ █    ██    █    ██   █  █  █   █ █    █  █
echo   ████████   █ █████ ████ ██████ ████ ██████  █ █   ██████ █  █
echo.
echo        ii's Stupid Menu - Installer
echo        github.com/iireborn/ii.Stupid.Menu
echo.

curl -L -f -# "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.4/BepInEx_win_x64_5.4.23.4.zip" -o BPNX54234.zip

if errorlevel 1 (
    color 0c
    echo.
    echo Failed to download BepInEx, check your internet connection.
    pause
    exit /b
)

powershell -command "Expand-Archive -Path 'BPNX54234.zip' -DestinationPath '%gamePath%' -Force"

cls
title ii's Stupid Menu Installer // [####------] Creating directories
echo Creating BepInEx directories...
if not exist "%gamePath%\BepInEx\config" mkdir "%gamePath%\BepInEx\config"
if not exist "%gamePath%\BepInEx\plugins" mkdir "%gamePath%\BepInEx\plugins"

cls
title ii's Stupid Menu Installer // [#######---] Downloading menu
echo Downloading latest release of ii's Stupid Menu...

for /f "tokens=*" %%i in ('powershell -Command "(Invoke-RestMethod -Uri 'https://api.github.com/repos/iireborn/ii.Stupid.Menu/releases/latest').assets | Where-Object { $_.name -like '*.dll' } | Select-Object -First 1 -ExpandProperty browser_download_url"') do (
    set pluginUrl=%%i
)

if "%pluginUrl%"=="" (
    color 0c
    echo.
    echo Failed to get latest release of menu, please report to Discord
    pause
    exit /b
)

curl -L -f -# "%pluginUrl%" -o "%gamePath%\BepInEx\plugins\ii.s.Stupid.Menu.dll"

if errorlevel 1 (
    color 0c
    echo.
    echo Failed to download the menu, please report to Discord
    pause
    exit /b
)

cls
title ii's Stupid Menu Installer // [##########] Finished

echo.
echo   ████████ ██  █████  █████████   ███████████   █   ██████ █  █
echo    ██   ██  █ █    █ █    ██    █    ██   █  █  ██ ██ █    ██ █
echo    ██   ██  █ █    █ ████ ██    ████ ██   █  █  █ █ █ ███  █ ██
echo    ██   ██  █ █    █ █    ██    █    ██   █  █  █   █ █    █  █
echo   ████████   █ █████ ████ ██████ ████ ██████  █ █   ██████ █  █
echo.
echo        ii's Stupid Menu - Installer
echo.

echo Congratulations, you now have the menu!
echo.
echo Launch Gorilla Tag and the menu will load automatically.
echo.
del "BPNX54234.zip" >nul 2>&1
pause
