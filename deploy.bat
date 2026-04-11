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
echo Copying bin dependencies...
for %%f in (ClosedXML.dll DocumentFormat.OpenXml.dll SixLabors.Fonts.dll System.IO.Packaging.dll Irony.dll XLParser.dll) do (
    if exist "%SRC%bin\Release\%%f" copy /Y "%SRC%bin\Release\%%f" "%DEST%\" >nul && echo   %%f
)

:: ──── Dependencies from packages (explicit copy, no subroutine) ────
echo Copying package dependencies...

set P=%SRC%packages

copy /Y "%P%\Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll" "%DEST%\" >nul 2>nul && echo   Newtonsoft.Json.dll || echo   [MISSING] Newtonsoft.Json.dll
copy /Y "%P%\System.Text.Json.8.0.4\lib\net462\System.Text.Json.dll" "%DEST%\" >nul 2>nul && echo   System.Text.Json.dll || echo   [MISSING] System.Text.Json.dll
copy /Y "%P%\System.Text.Encodings.Web.8.0.0\lib\net462\System.Text.Encodings.Web.dll" "%DEST%\" >nul 2>nul && echo   System.Text.Encodings.Web.dll || echo   [MISSING] System.Text.Encodings.Web.dll
copy /Y "%P%\Microsoft.Bcl.AsyncInterfaces.8.0.0\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll" "%DEST%\" >nul 2>nul && echo   Microsoft.Bcl.AsyncInterfaces.dll || echo   [MISSING] Microsoft.Bcl.AsyncInterfaces.dll
copy /Y "%P%\System.Buffers.4.5.1\lib\net461\System.Buffers.dll" "%DEST%\" >nul 2>nul && echo   System.Buffers.dll || echo   [MISSING] System.Buffers.dll
copy /Y "%P%\System.Memory.4.5.5\lib\net461\System.Memory.dll" "%DEST%\" >nul 2>nul && echo   System.Memory.dll || echo   [MISSING] System.Memory.dll
copy /Y "%P%\System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll" "%DEST%\" >nul 2>nul && echo   System.Numerics.Vectors.dll || echo   [MISSING] System.Numerics.Vectors.dll
copy /Y "%P%\System.Runtime.CompilerServices.Unsafe.6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll" "%DEST%\" >nul 2>nul && echo   System.Runtime.CompilerServices.Unsafe.dll || echo   [MISSING] System.Runtime.CompilerServices.Unsafe.dll
copy /Y "%P%\System.Threading.Tasks.Extensions.4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll" "%DEST%\" >nul 2>nul && echo   System.Threading.Tasks.Extensions.dll || echo   [MISSING] System.Threading.Tasks.Extensions.dll
copy /Y "%P%\System.ValueTuple.4.5.0\lib\net47\System.ValueTuple.dll" "%DEST%\" >nul 2>nul && echo   System.ValueTuple.dll || echo   [MISSING] System.ValueTuple.dll
copy /Y "%P%\ClosedXML.Parser.2.0.0\lib\netstandard2.0\ClosedXML.Parser.dll" "%DEST%\" >nul 2>nul && echo   ClosedXML.Parser.dll || echo   [MISSING] ClosedXML.Parser.dll
copy /Y "%P%\ExcelNumberFormat.1.1.0\lib\netstandard2.0\ExcelNumberFormat.dll" "%DEST%\" >nul 2>nul && echo   ExcelNumberFormat.dll || echo   [MISSING] ExcelNumberFormat.dll
copy /Y "%P%\SharpGLTF.Core.1.0.1\lib\netstandard2.0\SharpGLTF.Core.dll" "%DEST%\" >nul 2>nul && echo   SharpGLTF.Core.dll || echo   [MISSING] SharpGLTF.Core.dll
copy /Y "%P%\SharpGLTF.Toolkit.1.0.1\lib\netstandard2.0\SharpGLTF.Toolkit.dll" "%DEST%\" >nul 2>nul && echo   SharpGLTF.Toolkit.dll || echo   [MISSING] SharpGLTF.Toolkit.dll

echo.
echo === Deploy Complete ===
echo.
dir "%DEST%\*.dll" | find /c ".dll"
echo DLLs deployed.
echo.
pause
