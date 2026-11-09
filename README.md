# Generador de QR para vehículos

Aplicación de escritorio para **macOS y Windows** de **Rangel Venta de Autos**. Captura los datos de cada vehículo y crea un código QR con toda la información. El inventario vive **en la nube (Supabase)**, así que varios equipos (por ejemplo, las 2 Macs de la agencia) ven y editan los mismos autos, también fuera de la oficina.

El QR contiene texto, no una dirección web, por lo que se puede leer con cualquier celular **sin internet**.

> La versión anterior, solo para Windows y con base de datos local, sigue en `GeneradorQrAutos/`. Más abajo se explica cómo pasar sus datos a la nube.

## Datos incluidos

Marca y modelo, año, kilometraje, precio en MXN, procedencia (nacional, americano regularizado o americano no regularizado), motor/litraje (`5.0 L`), configuración del motor (`V8`) y observaciones opcionales.

## Funciones

- **Vista previa en vivo** del QR mientras escribes.
- **QR con el logotipo** de Rangel al centro, con corrección de errores alta para que se siga leyendo.
- **Ficha PDF** tamaño carta: se abre en el visor del sistema (Vista Previa en Mac) para imprimir con ⌘P / Ctrl+P o mandarla por WhatsApp o correo.
- **Ficha PNG**, **Guardar QR** y **Copiar QR** (se copia como imagen y como archivo, para pegarlo en WhatsApp, Word, Finder…).
- **Inventario compartido:** búsqueda instantánea, editar, marcar como vendido o disponible y eliminar.
- **Resumen** en el encabezado: autos disponibles, valor del inventario y autos vendidos.
- **Dos equipos a la vez:** si los dos editan el mismo auto, el segundo en guardar recibe el aviso *"Otro equipo modificó este auto. Se recargaron los datos más recientes."* y nada se sobrescribe sin darse cuenta.
- **Sin internet:** aparece la barra roja *"Sin conexión — reintentando…"*, no se puede guardar hasta que vuelva la conexión y lo que estás escribiendo no se pierde.
- **Respaldo local automático:** cada vez que se abre la app guarda una copia del inventario en `Documentos/GeneradorQR/respaldos/` (se conservan las 7 más recientes).

### Atajos de teclado

En Mac se usa ⌘; en Windows, Ctrl.

| Atajo | Acción |
| --- | --- |
| ⌘S / Ctrl+S | Guardar |
| ⌘N / Ctrl+N | Nuevo vehículo |
| ⌘P / Ctrl+P | Abrir ficha PDF |
| ⌘E / Ctrl+E | Guardar ficha como PNG |
| ⌘⇧C / Ctrl+Shift+C | Copiar QR |
| ⌘F / Ctrl+F | Buscar en el inventario |
| Enter / Supr | Editar / eliminar el vehículo seleccionado |

## 1. Configurar Supabase (una sola vez)

1. Crea una cuenta y un proyecto en [supabase.com](https://supabase.com) (plan gratuito).
2. En **SQL Editor**, pega y ejecuta [`supabase/schema.sql`](supabase/schema.sql).
3. En **Authentication → Sign In / Providers → Email**, desactiva *Allow new users to sign up*.
4. En **Authentication → Users → Add user**, crea la cuenta de la agencia (correo y contraseña) marcando *Auto Confirm User*.
5. En **Project Settings → API**, copia *Project URL* y la llave *anon public*. Crea `src/GeneradorQr.App/supabase.json` a partir de `supabase.example.json`:

   ```json
   { "Url": "https://xxxx.supabase.co", "AnonKey": "eyJ..." }
   ```

   La llave *anon* es pública por diseño: sin iniciar sesión con la cuenta de la agencia no deja leer ni escribir nada. **Nunca** pongas ahí la *service_role key*. `supabase.json` está en `.gitignore`.

> **Importante (plan gratuito):** si nadie usa la app durante 7 días, Supabase pausa el proyecto. Se reactiva con un clic desde el panel, sin perder datos. Mientras esté pausado, la app muestra *"No se pudo conectar con el servidor."*

## 2. Compilar y entregar

Requiere **.NET 10 SDK**.

### macOS (sin cuenta de Apple Developer)

En una Mac:

```bash
bash build/build-mac.sh arm64   # Mac con chip Apple (M1, M2…). Usa x64 para Mac con Intel.
```

Se genera `dist/GeneradorQR-mac-arm64.zip` con `Generador QR.app` (firma ad-hoc) e `instalar.command`.

**Instalar en cada Mac:** descomprime el zip y haz doble clic en `instalar.command`. El script copia la app a Aplicaciones y le quita el bloqueo de seguridad. Si macOS no deja abrir el script, ve a *Ajustes del Sistema → Privacidad y seguridad → "Abrir de todos modos"*. Hay que repetirlo con cada actualización.

### Windows

```powershell
powershell -ExecutionPolicy Bypass -File build\build-windows.ps1
```

Se genera `dist\GeneradorQR-windows.zip`, una carpeta autónoma (no necesita .NET instalado) con `GeneradorQr.App.exe` y `supabase.json`.

### Probar sin Supabase (modo demo)

Con la variable de entorno `GENERADORQR_DEMO=1`, la app abre con un inventario de ejemplo en memoria y sin pedir login. Sirve para mostrarla; nada se guarda al cerrar.

```powershell
$env:GENERADORQR_DEMO = "1"; dotnet run --project src/GeneradorQr.App
```

## 3. Pasar los autos de la versión anterior a la nube

Ejecuta en la computadora Windows que tiene la base anterior:

```powershell
dotnet run --project src/GeneradorQr.Migrate -- "$env:LOCALAPPDATA\GeneradorQrAutos\vehiculos.db" src\GeneradorQr.App\supabase.json
```

Pide el correo y la contraseña de la agencia y sube todos los autos, conservando si estaban vendidos y su fecha de captura. Si la nube ya tiene autos, se niega para no duplicarlos. Para forzarlo, agrega `--force`.

## Dónde guarda cosas la app

| Qué | Windows | macOS |
| --- | --- | --- |
| Sesión (token, nunca la contraseña) y log de errores | `%LOCALAPPDATA%\GeneradorQR\` | `~/Library/Application Support/GeneradorQR/` |
| Respaldos del inventario | `Documentos\GeneradorQR\respaldos\` | `~/Documents/GeneradorQR/respaldos/` |

Si algo falla, revisa `errores.log` en la carpeta de la primera fila.

## Para desarrolladores

```
src/GeneradorQr.Core     modelos, texto del QR, validación, QR y ficha (SkiaSharp), búsqueda, respaldos
src/GeneradorQr.Cloud    Supabase: autenticación y repositorio REST (bloqueo optimista con columna version)
src/GeneradorQr.App      interfaz Avalonia (macOS + Windows)
src/GeneradorQr.Migrate  importación única del SQLite de la versión anterior
tests/GeneradorQr.Tests  pruebas xUnit
```

```powershell
dotnet test tests/GeneradorQr.Tests
```

El diseño está en `docs/superpowers/specs/` y el plan de implementación en `docs/superpowers/plans/`.
