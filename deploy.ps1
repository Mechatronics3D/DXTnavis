$dest = "C:\Program Files\Autodesk\Navisworks Manage 2026\Plugins\DXTnavis"
$src = $PSScriptRoot

Write-Host "=== DXTnavis Deploy ===" -ForegroundColor Green

# Create directory
if (!(Test-Path $dest)) {
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Write-Host "Created: $dest"
}

# Copy files
$files = @(
    @{src="bin\Debug\DXTnavis.dll"; name="DXTnavis.dll"},
    @{src="packages\Newtonsoft.Json.13.0.4\lib\net45\Newtonsoft.Json.dll"; name="Newtonsoft.Json.dll"},
    @{src="packages\System.Text.Json.7.0.0\lib\net462\System.Text.Json.dll"; name="System.Text.Json.dll"},
    @{src="packages\System.Text.Encodings.Web.7.0.0\lib\net462\System.Text.Encodings.Web.dll"; name="System.Text.Encodings.Web.dll"},
    @{src="packages\Microsoft.Bcl.AsyncInterfaces.7.0.0\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll"; name="Microsoft.Bcl.AsyncInterfaces.dll"},
    @{src="packages\System.Buffers.4.5.1\lib\net461\System.Buffers.dll"; name="System.Buffers.dll"},
    @{src="packages\System.Memory.4.5.5\lib\net461\System.Memory.dll"; name="System.Memory.dll"},
    @{src="packages\System.Numerics.Vectors.4.5.0\lib\net46\System.Numerics.Vectors.dll"; name="System.Numerics.Vectors.dll"},
    @{src="packages\System.Runtime.CompilerServices.Unsafe.6.0.0\lib\net461\System.Runtime.CompilerServices.Unsafe.dll"; name="System.Runtime.CompilerServices.Unsafe.dll"},
    @{src="packages\System.Threading.Tasks.Extensions.4.5.4\lib\net461\System.Threading.Tasks.Extensions.dll"; name="System.Threading.Tasks.Extensions.dll"},
    @{src="packages\System.ValueTuple.4.5.0\lib\net47\System.ValueTuple.dll"; name="System.ValueTuple.dll"}
)

foreach ($file in $files) {
    $srcPath = Join-Path $src $file.src
    if (Test-Path $srcPath) {
        Copy-Item -Path $srcPath -Destination $dest -Force
        Write-Host "Copied: $($file.name)" -ForegroundColor Cyan
    } else {
        Write-Host "Not found: $srcPath" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Deploy Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Files in destination:" -ForegroundColor Yellow
Get-ChildItem $dest -Filter "*.dll" | Format-Table Name, Length -AutoSize
