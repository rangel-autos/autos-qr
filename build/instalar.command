#!/usr/bin/env bash
# Instala Generador QR en Aplicaciones y quita el bloqueo de macOS para apps sin firma de Apple.
# Doble clic en este archivo (si macOS no lo deja abrir: clic derecho → Abrir, o
# Ajustes del Sistema → Privacidad y seguridad → "Abrir de todos modos").
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
SOURCE="$HERE/Generador QR.app"
TARGET="/Applications/Generador QR.app"

if [ ! -d "$SOURCE" ]; then
  echo "No encuentro 'Generador QR.app' junto a este archivo."
  read -r -p "Pulsa Enter para cerrar…" _
  exit 1
fi

echo "Instalando en Aplicaciones…"
if [ -d "$TARGET" ]; then
  rm -rf "$TARGET" 2>/dev/null || sudo rm -rf "$TARGET"
fi
cp -R "$SOURCE" "/Applications/" 2>/dev/null || sudo cp -R "$SOURCE" "/Applications/"

echo "Quitando el bloqueo de seguridad (cuarentena)…"
xattr -dr com.apple.quarantine "$TARGET" 2>/dev/null || sudo xattr -dr com.apple.quarantine "$TARGET"

echo "Listo. Abriendo Generador QR…"
open "$TARGET"
