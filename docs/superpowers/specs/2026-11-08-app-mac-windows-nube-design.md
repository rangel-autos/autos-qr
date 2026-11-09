# Generador de QR — versión Mac + Windows con base de datos en la nube

Fecha: 2026-11-08
Estado: diseño aprobado en conversación, pendiente de revisión escrita

## 1. Objetivo

El cliente (Rangel Venta de Autos) quiere usar el generador de QR en **2 Macs** que compartan **el mismo inventario**, y poder consultarlo también **fuera de la oficina**. La app actual es WinForms (solo Windows) con SQLite local, por lo que cada equipo tiene su propio inventario aislado.

### Requisitos acordados

- Una sola app de escritorio para **macOS y Windows** (mismo código).
- Base de datos **en la nube** (Supabase, plan gratuito), accesible desde cualquier lugar con internet.
- **Una cuenta compartida** de la agencia (correo + contraseña); se inicia sesión una vez por equipo.
- **Sin internet: avisar y esperar.** No hay modo offline ni sincronización diferida.
- **Costo de licencias cero:** sin Apple Developer Program (firma ad-hoc + quitar cuarentena al instalar), Supabase gratis.

### Supuestos confirmados

1. Mismas funciones que la versión actual; nada nuevo salvo lo descrito aquí.
2. El QR sigue conteniendo **texto** (legible sin internet), no una URL.
3. Los vehículos existentes en el SQLite de Windows se migran una sola vez.
4. El desarrollador crea el proyecto de Supabase (titularidad a acordar con el cliente).

### Criterio de éxito

En las 2 Macs: se captura un auto en una y aparece en la otra; se imprime la ficha; el QR se escanea correctamente con un celular.

### Fuera de alcance

Apps móviles, versión web, página pública por vehículo, fotos, usuarios individuales o roles, actualización en tiempo real, modo offline.

## 2. Arquitectura

Enfoque elegido: **la app habla directamente con Supabase**, autenticada con la cuenta compartida y protegida con Row Level Security. Sin servidor propio. Descartados: conexión directa a PostgreSQL (expone la contraseña de la base en la app) y API propia (infraestructura innecesaria para 2 equipos).

```
GeneradorQr.sln
├── GeneradorQr.Core    (net10.0) lógica pura: modelos, texto del QR, validación/normalización,
│                       render de QR con logo y ficha con SkiaSharp, IVehicleRepository
├── GeneradorQr.Cloud   (net10.0) Supabase vía HttpClient: auth (login, refresh, persistencia
│                       de sesión) y SupabaseVehicleRepository
├── GeneradorQr.App     (Avalonia) UI Mac + Windows: login, pantalla principal, estado de conexión
├── GeneradorQr.Migrate (consola) importa el SQLite actual a Supabase una sola vez
└── GeneradorQr.Tests   (xUnit)
```

- Dependencias en una sola dirección: `App → Cloud → Core`, `App → Core`. Core no conoce ni la UI ni la red.
- Se usa `HttpClient` contra la API REST de Supabase (`/auth/v1`, `/rest/v1`) en lugar del cliente C# comunitario: pocas operaciones, menos dependencias, más control.
- `GeneradorQrAutos` (WinForms) se queda en el repositorio hasta que la nueva app la reemplace.

### Código reutilizado y código que cambia

| Actual | Destino |
|---|---|
| `Models/VehicleRecord.cs` | Core, sin cambios relevantes |
| Texto del QR, normalización y validación (en `VehicleQrService.cs` / `Form1.cs`) | Core, extraído a clases puras con pruebas |
| `VehicleQrService.cs` (QR + logo con System.Drawing) | Core, reescrito con QRCoder (matriz) + SkiaSharp |
| `VehicleSheetRenderer.cs` (System.Drawing) | Core, reescrito con SkiaSharp; salida PNG y PDF (`SKDocument`) |
| `Data/VehicleRepository.cs` (SQLite) | Reemplazado por `SupabaseVehicleRepository`; la lectura de SQLite pasa a `GeneradorQr.Migrate` |
| `Form1.cs` (WinForms) | Reemplazado por vistas Avalonia (MVVM) |

## 3. Base de datos (Supabase / PostgreSQL)

```sql
create table public.vehiculos (
  id                  bigint generated always as identity primary key,
  marca               text not null,
  modelo              text not null,
  anio                int not null,
  kilometraje         bigint not null,
  precio              numeric(12,2) not null,
  procedencia         text not null check (procedencia in
                        ('Nacional', 'Americano regularizado', 'Americano no regularizado')),
  motor_litros        text not null,
  configuracion_motor text not null,
  observaciones       text not null default '',
  vendido             boolean not null default false,
  creado_en           timestamptz not null default now(),
  actualizado_en      timestamptz not null default now()
);
-- Trigger: actualizado_en = now() en cada UPDATE.
alter table public.vehiculos enable row level security;
create policy "solo usuarios autenticados" on public.vehiculos
  for all to authenticated using (true) with check (true);
```

Los valores de `procedencia` coinciden con los de la app actual (`Form1.cs:252`).

### Acceso y seguridad

- En Supabase: **registro de usuarios desactivado**; se crea manualmente la cuenta de la agencia.
- La app solo incluye la URL del proyecto y la **anon key** (pública por diseño); sin sesión, RLS no deja leer ni escribir.
- Se guarda en el equipo el **refresh token** (no la contraseña) en un archivo dentro de la carpeta de datos de la app, con permisos solo para el usuario. Al abrir, se renueva la sesión; si falla, se muestra el login.

### Concurrencia entre equipos

Bloqueo optimista: al editar, el `UPDATE` incluye el filtro `actualizado_en = <valor cargado>`. Si afecta 0 filas, la app avisa "Otro equipo modificó este auto. Se recargaron los datos más recientes." y recarga el registro. Lo mismo aplica a marcar vendido y a eliminar.

### Lectura, búsqueda y resumen

- Se descarga el inventario completo (se espera del orden de cientos de registros) y la búsqueda y el resumen se calculan en memoria, con la misma semántica actual: cada palabra debe aparecer en algún campo y se ordena por disponibles primero y luego por id descendente.
- Recarga: al activar la ventana, cada 60 s y con el botón "Actualizar". Sin Realtime.

### Respaldo local

Cada vez que la app abre y carga el inventario, guarda una copia JSON en `Documentos/GeneradorQR/respaldos/inventario-AAAA-MM-DD-HHmm.json`, conservando las 7 más recientes. Motivo: el plan gratuito de Supabase no incluye respaldos.

### Limitaciones conocidas del plan gratuito

El proyecto se pausa tras 7 días sin actividad; se reactiva desde el panel de Supabase sin pérdida de datos. Debe documentarse para el cliente.

## 4. Pantallas

**Inicio de sesión:** logo, correo, contraseña y botón "Entrar". Si fallan las credenciales: "Correo o contraseña incorrectos."

**Pantalla principal**, con la misma distribución que la versión WinForms:

- Encabezado: logo, resumen (disponibles, valor del inventario, vendidos), indicador de conexión (Conectado / Sin conexión), botón "Actualizar" y menú con "Cerrar sesión".
- Formulario: los mismos campos, validación, autocompletado de marcas y modelos, y litraje normalizado (`5` → `5.0 L`).
- Vista previa del QR en vivo.
- Acciones: Generar QR y guardar · Ficha PDF · Ficha PNG · Guardar QR · Copiar QR.
- Inventario: búsqueda instantánea, filtro de vendidos, doble clic o Enter para editar, menú contextual para marcar vendido o disponible y para eliminar.
- Atajos: los actuales, con ⌘ en macOS y Ctrl en Windows.

**Imprimir:** Avalonia no tiene impresión integrada. "Ficha PDF" genera el PDF en una carpeta temporal y lo abre con el visor del sistema (Vista Previa en Mac), desde donde se imprime o se comparte.

**Copiar QR:** primero se intenta copiar la imagen al portapapeles con Avalonia. **Se verifica en macOS al inicio de la implementación**; si no funciona bien, el plan B es copiar el PNG como archivo.

## 5. Manejo de errores

| Situación | Comportamiento |
|---|---|
| Sin internet o timeout | Barra roja "Sin conexión — reintentando…", reintento automático; las acciones que guardan se desactivan y el formulario **no se borra** |
| Token vencido | Se renueva solo; si no se puede, se vuelve al login |
| Proyecto pausado o servidor caído | "No se pudo conectar con el servidor." con la indicación de reactivar el proyecto |
| Conflicto de edición | Aviso y recarga (ver Concurrencia) |
| Error inesperado | Mensaje en español y registro en un log local de la carpeta de datos de la app |

## 6. Pruebas

**Automáticas (xUnit):**

- Texto del QR.
- Validación y normalización.
- Que el QR con logo se pueda decodificar (round-trip).
- Que la ficha PNG y PDF se genere.
- `SupabaseVehicleRepository` contra un `HttpMessageHandler` simulado: login, refresh, CRUD, conflicto (0 filas), errores de red y 401.
- Filtro de búsqueda y resumen en memoria.
- Rotación de respaldos.

**Manuales en Mac** (lista de aceptación):

1. Login en las 2 Macs.
2. Capturar en A → aparece en B.
3. Editar el mismo auto en ambas → aviso de conflicto.
4. Desconectar el wifi → barra roja, el formulario se conserva y al reconectar se puede guardar.
5. Ficha PDF → Vista Previa → imprimir.
6. Escanear el QR con un celular.
7. Copiar el QR y pegarlo en WhatsApp.
8. Prueba básica en Windows.

## 7. Distribución

- `dotnet publish -r osx-arm64 --self-contained` (y `osx-x64` si alguna Mac es Intel); se empaqueta como `.app` con firma ad-hoc (`codesign --force --deep --sign -`).
- Compilación en una Mac (del cliente) o en GitHub Actions (runner macOS).
- Instalación: copiar a `/Applications` y quitar la cuarentena una vez con `xattr -dr com.apple.quarantine`, o con "Abrir de todos modos" en Privacidad y seguridad. Se entrega un script `instalar.command` que lo hace.
- Windows: `dotnet publish -r win-x64 --self-contained`, igual que hoy.
