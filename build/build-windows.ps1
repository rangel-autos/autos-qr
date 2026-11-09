# Empaqueta Generador QR para Windows (carpeta autónoma, no requiere .NET instalado).
# Uso:  powershell -ExecutionPolicy Bypass -File build\build-windows.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\GeneradorQr.App\GeneradorQr.App.csproj'
$config = Join-Path $root 'src\GeneradorQr.App\supabase.json'
$out = Join-Path $root 'dist\GeneradorQR-windows'

if (-not (Test-Path $config)) {
    throw "Falta $config (copia supabase.example.json y llena Url y AnonKey)."
}

if (Test-Path $out) { Remove-Item -Recurse -Force $out }

dotnet publish $project -c Release -r win-x64 --self-contained true -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falló.' }

Copy-Item $config (Join-Path $out 'supabase.json')

$zip = Join-Path $root 'dist\GeneradorQR-windows.zip'
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $out -DestinationPath $zip

Write-Host "Listo: $zip"
