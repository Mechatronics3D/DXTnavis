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

set DEST=C:\Program Files\Autodesk\Navisworks Manage 2026\Plugins\DXTnavis
echo === Deploying to %DEST% ===

if not exist "%DEST%" mkdir "%DEST%"

:: Main assembly
copy /Y "bin\%CONFIG%\DXTnavis.dll" "%DEST%\" >nul && echo   DXTnavis.dll

:: Dependencies from bin output
for %%f in (
    ClosedXML.dll
    DocumentFormat.OpenXml.dll
    SixLabors.Fonts.dll
    System.IO.Packaging.dll
    Irony.dll
    XLParser.dll
) do (
    if exist "bin\%CONFIG%\%%f" copy /Y "bin\%CONFIG%\%%f" "%DEST%\" >nul && echo   %%f
)

:: Dependencies from packages
call :deploy_pkg "Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll"
call :deploy_pkg "System.Text.Json.7.0.0\lib\net462\System.Text.Json.dll"
call :deploy_pkg "System.Text.Encodings.Web.7.0.0\lib\net462\System.Text.Encodings.Web.dll"
call :deploy_pkg "Microsoft.Bcl.AsyncInterfaces.7.0.0\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll"
call :deploy_pkg "System.Buffers.4.5.1\lib\net461\System.Buffers.dll"
call :deploy_pkg "System.Memory.4.5.5\lib\net461\System.Memory.dll"
call :deploy_pkg "System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll"
call :deploy_pkg "System.Runtime.CompilerServices.Unsafe.6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll"
call :deploy_pkg "System.Threading.Tasks.Extensions.4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll"
call :deploy_pkg "System.ValueTuple.4.5.0\lib\net47\System.ValueTuple.dll"
call :deploy_pkg "ClosedXML.Parser.2.0.0\lib\netstandard2.0\ClosedXML.Parser.dll"
call :deploy_pkg "ExcelNumberFormat.1.1.0\lib\netstandard2.0\ExcelNumberFormat.dll"

echo.
echo [OK] Deploy complete.
goto end

:deploy_pkg
if exist "packages\%~1" (
    copy /Y "packages\%~1" "%DEST%\" >nul
    for %%n in ("%~1") do echo   %%~nxn
)
exit /b

:end
endlocal
