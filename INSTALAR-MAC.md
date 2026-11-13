# Instalar Generador QR en Mac

## 1. Elige el archivo correcto

Menú **Apple → Acerca de esta Mac** y mira la línea *Chip* o *Procesador*:

| Tu Mac dice… | Usa este archivo |
| --- | --- |
| **Chip Apple** (M1, M2, M3, M4…) | `GeneradorQR-mac-arm64.zip` |
| **Procesador Intel** | `GeneradorQR-mac-x64.zip` |

Si instalas el equivocado, la app no abre (o va lenta). Solo borra y repite con el otro.

## 2. Instalar

1. Copia el `.zip` a la Mac **tal cual** (AirDrop, USB, Drive…). No lo descomprimas en Windows.
2. Haz doble clic en el `.zip`. Sale otro `.zip` adentro: doble clic también en ese. Queda una carpeta `GeneradorQR-mac-arm64` (o `-x64`) con:
   - `Generador QR.app`
   - `instalar.command`
3. Doble clic en **`instalar.command`**. Copia la app a *Aplicaciones*, le quita el bloqueo de seguridad y la abre. Si pide la contraseña de la Mac, escríbela (no se ve mientras tecleas) y pulsa Enter.
4. Inicia sesión con el correo y la contraseña de la agencia.

Desde entonces la app se abre desde **Aplicaciones** o Launchpad como cualquier otra. La carpeta descomprimida y los `.zip` ya se pueden borrar.

## Si macOS no deja abrir `instalar.command`

Es normal: la app no está firmada con una cuenta de Apple. Usa cualquiera de estas dos opciones:

**Opción A — Ajustes.** Cierra el aviso, abre **Ajustes del Sistema → Privacidad y seguridad**, baja hasta el mensaje sobre `instalar.command` y pulsa **Abrir de todos modos**.

**Opción B — Terminal.** Abre **Terminal**, escribe `bash ` (con un espacio al final), arrastra `instalar.command` a la ventana y pulsa Enter.

## Actualizar

Repite los mismos pasos con el `.zip` nuevo. El instalador reemplaza la versión anterior; el inventario no se pierde porque vive en la nube.

## Problemas comunes

| Qué pasa | Qué hacer |
| --- | --- |
| *"Generador QR" está dañada y no puede abrirse* | Abriste la app directo desde la carpeta. Ejecuta `instalar.command`. |
| *No encuentro 'Generador QR.app' junto a este archivo* | Moviste `instalar.command` fuera de su carpeta. Regrésalo junto a la app. |
| *No se pudo conectar con el servidor* | Revisa el internet. Si sigue igual, avisa al encargado: el servidor puede estar en pausa. |
| Otro error | Envía el archivo `errores.log` que está en `~/Library/Application Support/GeneradorQR/` (en Finder: **Ir → Ir a la carpeta…** y pega esa ruta). |
