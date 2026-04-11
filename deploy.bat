@echo off
echo === DXTnavis Deployment Script ===
echo Requires Administrator privileges to copy to Program Files
echo.

set SOURCE=%~dp0bin\Release
set TARGET=C:\Program Files\Autodesk\Navisworks Manage 2026\Plugins\DXTnavis

echo Source: %SOURCE%
echo Target: %TARGET%
echo.

:: Clean existing plugin before deploy
if exist "%TARGET%" (
    echo Cleaning existing plugin folder...
    del /Q "%TARGET%\*.dll" 2>nul
    del /Q "%TARGET%\*.pdb" 2>nul
    echo Cleaned.
    echo.
)

if not exist "%TARGET%" mkdir "%TARGET%"

copy /Y "%SOURCE%\DXTnavis.dll" "%TARGET%\"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to copy DXTnavis.dll
    echo Please run this script as Administrator
    pause
    exit /b 1
)

copy /Y "%SOURCE%\DXTnavis.pdb" "%TARGET%\"
if %ERRORLEVEL% NEQ 0 (
    echo WARNING: Failed to copy DXTnavis.pdb (optional)
)

echo.
echo === Deployment Complete ===
echo DXTnavis.dll has been deployed to:
echo %TARGET%
echo.
pause
