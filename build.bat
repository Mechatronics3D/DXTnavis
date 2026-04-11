@echo off
setlocal

echo === DXTnavis Build Script ===
echo.

:: ──── Find MSBuild via vswhere ────
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo [ERROR] vswhere.exe not found. Install Visual Studio or Build Tools.
    exit /b 1
)

"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" > "%TEMP%\msbuild_path.txt" 2>nul
set /p MSBUILD=<"%TEMP%\msbuild_path.txt"
del "%TEMP%\msbuild_path.txt" 2>nul

if not defined MSBUILD (
    echo [ERROR] MSBuild not found. Install Visual Studio or Build Tools with .NET desktop workload.
    exit /b 1
)

echo [OK] MSBuild: %MSBUILD%

:: ──── Parse arguments ────
set CONFIG=Release
set DEPLOY=0

:parse_args
if "%~1"=="" goto done_args
if /i "%~1"=="debug"   set CONFIG=Debug& shift & goto parse_args
if /i "%~1"=="release" set CONFIG=Release& shift & goto parse_args
if /i "%~1"=="--deploy" set DEPLOY=1& shift & goto parse_args
if /i "%~1"=="-d"       set DEPLOY=1& shift & goto parse_args
shift
goto parse_args
:done_args

echo [OK] Configuration: %CONFIG%
echo.

:: ──── NuGet restore ────
if exist ".nuget\NuGet.exe" (
    echo Restoring NuGet packages...
    .nuget\NuGet.exe restore DXTnavis.sln -NonInteractive
    echo.
)

:: ──── Build ────
echo Building DXTnavis (%CONFIG%)...
echo.
"%MSBUILD%" DXTnavis.csproj -p:Configuration=%CONFIG% -p:Platform=AnyCPU -verbosity:minimal -nologo
if errorlevel 1 (
    echo.
    echo [FAILED] Build failed.
    exit /b 1
)

echo.
echo [OK] Build succeeded: bin\%CONFIG%\DXTnavis.dll
echo.

:: ──── Deploy (optional) ────
if %DEPLOY%==0 goto end

echo.
call "%~dp0deploy.bat"

:end
endlocal
