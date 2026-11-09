#!/usr/bin/env bash
# Empaqueta Generador QR como app de macOS, sin cuenta de Apple Developer (firma ad-hoc).
#
# Uso (en una Mac con .NET 10 SDK):   bash build/build-mac.sh [arm64|x64]
#   arm64 = Mac con chip Apple (M1, M2, M3…)   x64 = Mac con Intel
#
# Resultado: dist/Generador QR.app y dist/GeneradorQR-mac-<arch>.zip (incluye instalar.command)
set -euo pipefail

ARCH="${1:-arm64}"
case "$ARCH" in arm64|x64) ;; *) echo "Arquitectura inválida: $ARCH (usa arm64 o x64)"; exit 2 ;; esac

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/GeneradorQr.App/GeneradorQr.App.csproj"
CONFIG="$ROOT/src/GeneradorQr.App/supabase.json"
OUT="$ROOT/dist"
APP="$OUT/Generador QR.app"
PUBLISH="$OUT/publish-osx-$ARCH"

if [ ! -f "$CONFIG" ]; then
  echo "Falta $CONFIG (copia supabase.example.json y llena Url y AnonKey)."
  exit 1
fi

echo "==> Publicando para osx-$ARCH"
rm -rf "$PUBLISH" "$APP"
dotnet publish "$PROJECT" -c Release -r "osx-$ARCH" --self-contained true \
  -p:UseAppHost=true -p:PublishSingleFile=false -o "$PUBLISH"

echo "==> Armando $APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
cp "$ROOT/build/Info.plist" "$APP/Contents/Info.plist"
cp "$CONFIG" "$APP/Contents/Resources/supabase.json"
chmod +x "$APP/Contents/MacOS/GeneradorQr.App"

echo "==> Icono"
ICONSET="$OUT/AppIcon.iconset"
rm -rf "$ICONSET" && mkdir -p "$ICONSET"
LOGO="$ROOT/src/GeneradorQr.App/Assets/rangel-logo.png"
for size in 16 32 128 256 512; do
  sips -z $size $size "$LOGO" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  sips -z $((size * 2)) $((size * 2)) "$LOGO" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
rm -rf "$ICONSET"

echo "==> Firma ad-hoc (sin cuenta de Apple)"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"

echo "==> Comprimiendo"
STAGE="$OUT/GeneradorQR-mac-$ARCH"
rm -rf "$STAGE" && mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
cp "$ROOT/build/instalar.command" "$STAGE/"
chmod +x "$STAGE/instalar.command"
rm -f "$OUT/GeneradorQR-mac-$ARCH.zip"
ditto -c -k --keepParent "$STAGE" "$OUT/GeneradorQR-mac-$ARCH.zip"
rm -rf "$STAGE" "$PUBLISH"

echo "Listo: $OUT/GeneradorQR-mac-$ARCH.zip"
