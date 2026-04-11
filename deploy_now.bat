@echo off
echo === DXTnavis Manual Deploy ===

set DEST=C:\Program Files\Autodesk\Navisworks Manage 2026\Plugins\DXTnavis
set SRC=%~dp0

:: Clean existing plugin before deploy
if exist "%DEST%" (
    echo Cleaning existing plugin folder...
    del /Q "%DEST%\*.dll" 2>nul
    del /Q "%DEST%\*.pdb" 2>nul
    echo Cleaned.
    echo.
) else (
    mkdir "%DEST%"
)

echo Copying DXTnavis.dll...
copy /Y "%SRC%\bin\Release\DXTnavis.dll" "%DEST%\"

echo Copying Newtonsoft.Json.dll...
copy /Y "%SRC%\packages\Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll" "%DEST%\"

echo Copying System.Text.Json.dll...
copy /Y "%SRC%\packages\System.Text.Json.7.0.0\lib\net462\System.Text.Json.dll" "%DEST%\"

echo Copying System.Text.Encodings.Web.dll...
copy /Y "%SRC%\packages\System.Text.Encodings.Web.7.0.0\lib\net462\System.Text.Encodings.Web.dll" "%DEST%\"

echo Copying Microsoft.Bcl.AsyncInterfaces.dll...
copy /Y "%SRC%\packages\Microsoft.Bcl.AsyncInterfaces.7.0.0\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll" "%DEST%\"

echo Copying System.Buffers.dll...
copy /Y "%SRC%\packages\System.Buffers.4.5.1\lib\net461\System.Buffers.dll" "%DEST%\"

echo Copying System.Memory.dll...
copy /Y "%SRC%\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll" "%DEST%\"

echo Copying System.Numerics.Vectors.dll...
copy /Y "%SRC%\packages\System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll" "%DEST%\"

echo Copying System.Runtime.CompilerServices.Unsafe.dll...
copy /Y "%SRC%\packages\System.Runtime.CompilerServices.Unsafe.6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll" "%DEST%\"

echo Copying System.Threading.Tasks.Extensions.dll...
copy /Y "%SRC%\packages\System.Threading.Tasks.Extensions.4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll" "%DEST%\"

echo Copying System.ValueTuple.dll...
copy /Y "%SRC%\packages\System.ValueTuple.4.5.0\lib\net47\System.ValueTuple.dll" "%DEST%\"

echo Copying ClosedXML.dll...
copy /Y "%SRC%\bin\Release\ClosedXML.dll" "%DEST%\"

echo Copying ClosedXML.Parser.dll...
copy /Y "%SRC%\packages\ClosedXML.Parser.2.0.0\lib\netstandard2.0\ClosedXML.Parser.dll" "%DEST%\"

echo Copying DocumentFormat.OpenXml.dll...
copy /Y "%SRC%\bin\Release\DocumentFormat.OpenXml.dll" "%DEST%\"

echo Copying SixLabors.Fonts.dll...
copy /Y "%SRC%\bin\Release\SixLabors.Fonts.dll" "%DEST%\"

echo Copying ExcelNumberFormat.dll...
copy /Y "%SRC%\packages\ExcelNumberFormat.1.1.0\lib\netstandard2.0\ExcelNumberFormat.dll" "%DEST%\"

echo Copying System.IO.Packaging.dll...
copy /Y "%SRC%\bin\Release\System.IO.Packaging.dll" "%DEST%\"

echo Copying Irony.dll...
copy /Y "%SRC%\bin\Release\Irony.dll" "%DEST%\"

echo Copying XLParser.dll...
copy /Y "%SRC%\bin\Release\XLParser.dll" "%DEST%\"

echo.
echo === Deploy Complete ===
echo.
dir "%DEST%"
pause
