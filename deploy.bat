@echo off
setlocal

echo === DXTnavis Deploy ===
echo Requires Administrator privileges to copy to Program Files
echo.

set SRC=%~dp0
set DEST=C:\Program Files\Autodesk\Navisworks Manage 2026\Plugins\DXTnavis

echo Source: %SRC%bin\Release
echo Target: %DEST%
echo.

:: ──── Verify build output exists ────
if not exist "%SRC%bin\Release\DXTnavis.dll" (
    echo [ERROR] bin\Release\DXTnavis.dll not found. Run build.bat first.
    exit /b 1
)

:: ──── Clean existing plugin folder ────
if exist "%DEST%" (
    echo Cleaning existing plugin folder...
    del /Q "%DEST%\*.dll" 2>nul
    del /Q "%DEST%\*.pdb" 2>nul
    echo Cleaned.
    echo.
) else (
    mkdir "%DEST%"
)

:: ──── Main assembly ────
echo Copying DXTnavis.dll...
copy /Y "%SRC%bin\Release\DXTnavis.dll" "%DEST%\" >nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to copy DXTnavis.dll
    echo Please run this script as Administrator.
    pause
    exit /b 1
)
copy /Y "%SRC%bin\Release\DXTnavis.pdb" "%DEST%\" >nul 2>nul

:: ──── Dependencies from bin\Release ────
for %%f in (
    ClosedXML.dll
    DocumentFormat.OpenXml.dll
    SixLabors.Fonts.dll
    System.IO.Packaging.dll
    Irony.dll
    XLParser.dll
) do (
    if exist "%SRC%bin\Release\%%f" (
        copy /Y "%SRC%bin\Release\%%f" "%DEST%\" >nul && echo   %%f
    )
)

:: ──── Dependencies from packages ────
call :pkg "Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll"
call :pkg "System.Text.Json.8.0.4\lib\net462\System.Text.Json.dll"
call :pkg "System.Text.Encodings.Web.8.0.0\lib\net462\System.Text.Encodings.Web.dll"
call :pkg "Microsoft.Bcl.AsyncInterfaces.7.0.0\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll"
call :pkg "System.Buffers.4.5.1\lib\net461\System.Buffers.dll"
call :pkg "System.Memory.4.5.5\lib\net461\System.Memory.dll"
call :pkg "System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll"
call :pkg "System.Runtime.CompilerServices.Unsafe.6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll"
call :pkg "System.Threading.Tasks.Extensions.4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll"
call :pkg "System.ValueTuple.4.5.0\lib\net47\System.ValueTuple.dll"
call :pkg "ClosedXML.Parser.2.0.0\lib\netstandard2.0\ClosedXML.Parser.dll"
call :pkg "ExcelNumberFormat.1.1.0\lib\netstandard2.0\ExcelNumberFormat.dll"
call :pkg "SharpGLTF.Core.1.0.1\lib\netstandard2.0\SharpGLTF.Core.dll"
call :pkg "SharpGLTF.Toolkit.1.0.1\lib\netstandard2.0\SharpGLTF.Toolkit.dll"

echo.
echo === Deploy Complete ===
echo.
dir "%DEST%\*.dll" | find /c ".dll"
echo DLLs deployed.
echo.
pause
exit /b 0

:pkg
if exist "%SRC%packages\%~1" (
    copy /Y "%SRC%packages\%~1" "%DEST%\" >nul
    for %%n in ("%~1") do echo   %%~nxn
)
exit /b
