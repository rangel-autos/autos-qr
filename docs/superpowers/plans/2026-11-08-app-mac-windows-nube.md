# Generador QR Mac + Windows con nube — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar la app WinForms por una app Avalonia (macOS + Windows) que guarda el inventario en Supabase, compartido entre equipos, con las mismas funciones que la versión actual.

**Architecture:** `GeneradorQr.Core` (lógica pura y render con SkiaSharp) ← `GeneradorQr.Cloud` (Supabase vía HttpClient: auth + repositorio REST) ← `GeneradorQr.App` (Avalonia MVVM). `GeneradorQr.Migrate` sube el SQLite actual una vez. La seguridad es Row Level Security con una cuenta compartida; la concurrencia es optimista con una columna `version`.

**Tech Stack:** .NET 10, Avalonia 11.3.x, CommunityToolkit.Mvvm 8.4.x, SkiaSharp 2.88.x (la misma que trae Avalonia), QRCoder 1.6.0, Microsoft.Data.Sqlite 8.0.11 (solo Migrate), xUnit, ZXing.Net + ZXing.Net.Bindings.SkiaSharp (solo tests), fuente Inter (OFL) embebida.

**Spec:** `docs/superpowers/specs/2026-11-08-app-mac-windows-nube-design.md`

## Global Constraints

- TargetFramework `net10.0` en todos los proyectos. Fijar versiones NuGet exactas en cada `.csproj` (sin `*`).
- Todo texto visible en español de México. Moneda y números con `CultureInfo.GetCultureInfo("es-MX")`.
- La app solo incluye `Url` y `AnonKey` de Supabase. Nunca la contraseña de la base ni la service key.
- Se persiste el refresh token, nunca la contraseña.
- Procedencias exactas: `Nacional`, `Americano regularizado`, `Americano no regularizado`.
- El texto del QR debe ser **idéntico** al que genera hoy `GeneradorQrAutos/Services/VehicleQrService.cs` `BuildPayload` (la marca y la ubicación salen de `Brand.cs`).
- Respaldos en `Documentos/GeneradorQR/respaldos/inventario-yyyy-MM-dd-HHmm.json`; se conservan los 7 más recientes.
- Recarga del inventario: al activar la ventana, cada 60 s y con el botón "Actualizar". Sin Realtime.
- Mensajes exactos:
  - Sin conexión: "Sin conexión — reintentando…"
  - Conflicto: "Otro equipo modificó este auto. Se recargaron los datos más recientes."
  - Login fallido: "Correo o contraseña incorrectos."
  - Servidor: "No se pudo conectar con el servidor."
- `GeneradorQrAutos/` (WinForms) no se modifica.

**Desviación respecto al spec (registrada):** la concurrencia usa una columna `version int` que incrementa un trigger, en lugar de comparar `actualizado_en`. El timestamp tiene microsegundos en Postgres y no hace round-trip exacto en el filtro de PostgREST. El spec se actualiza en la Task 5.

## Review Focus

1. **Fechas UTC del servidor:** `creado_en` llega en UTC. La línea "Generado:" del QR y la ficha deben mostrar hora local, no UTC → test en Task 6 (`GetAll_ConvierteFechasAHoraLocal`).
2. **Precio con centavos:** `numeric` en JSON no debe perder precisión (`495000.50` → `495000.50m`) y el texto del QR debe quedar igual que en Windows → test en Task 6 (`GetAll_PrecioDecimalExacto`) y golden test en Task 1.
3. **Se cae la red al guardar:** el formulario no se limpia, el botón vuelve a habilitarse y aparece la barra roja → test en Task 9 (`Guardar_SinRed_ConservaFormulario`).
4. **Correr la migración dos veces:** no debe duplicar autos → test en Task 7 (`Migrar_TablaRemotaConDatos_SeNiegaSinForce`).
5. **Acentos, ñ y emojis en observaciones:** el QR los decodifica igual y la ficha no truena → tests en Task 2 (`Qr_ConAcentosYEmoji_Decodifica`) y Task 3 (`Pdf_ConTextoUnicode_NoFalla`).

---

## Estructura de archivos

```
GeneradorQr.sln
src/GeneradorQr.Core/
  Models/VehicleRecord.cs           Models/VehicleInput.cs
  Brand.cs                          (nombre, ubicación, colores SKColor, listas de procedencias, marcas y configuraciones)
  Formatting/VehicleText.cs         (payload, parse de precio, normalización de motor)
  Formatting/VehicleValidator.cs
  Rendering/QrRenderer.cs           Rendering/SheetRenderer.cs   Rendering/BrandAssets.cs
  Assets/rangel-logo.png            Assets/Fonts/Inter-{Regular,Bold,Black}.ttf
  Inventory/InventoryQuery.cs       Inventory/InventoryStats.cs
  Data/IVehicleRepository.cs        Data/RepositoryExceptions.cs
  Backup/BackupService.cs
src/GeneradorQr.Cloud/
  SupabaseOptions.cs  SessionStore.cs  SupabaseAuth.cs  SupabaseVehicleRepository.cs  Dto/VehicleRow.cs
src/GeneradorQr.Migrate/Program.cs  src/GeneradorQr.Migrate/SqliteReader.cs
src/GeneradorQr.App/
  App.axaml(.cs)  Program.cs  AppPaths.cs  Log.cs  supabase.example.json
  ViewModels/{LoginViewModel,MainViewModel,VehicleFormViewModel,ConnectionMonitor}.cs
  Views/{LoginWindow,MainWindow}.axaml(.cs)
  Services/{IFileActions,FileActions}.cs
tests/GeneradorQr.Tests/   (un archivo de tests por clase probada; Fakes/FakeHttpHandler.cs, Fakes/FakeRepository.cs)
supabase/schema.sql
build/build-mac.sh  build/Info.plist  build/instalar.command
```

---

### Task 1: Solución, Core: modelos, texto y validación

**Files:**
- Create: `GeneradorQr.sln`, `src/GeneradorQr.Core/GeneradorQr.Core.csproj`, `src/GeneradorQr.Core/Models/*.cs`, `src/GeneradorQr.Core/Brand.cs`, `src/GeneradorQr.Core/Formatting/*.cs`, `tests/GeneradorQr.Tests/GeneradorQr.Tests.csproj`, `tests/GeneradorQr.Tests/VehicleTextTests.cs`, `tests/GeneradorQr.Tests/VehicleValidatorTests.cs`
- Modify: `.gitignore` (agregar `bin/`, `obj/`, `supabase.json`)

**Interfaces:**
- Produces:
  - `VehicleRecord`: las mismas propiedades que `GeneradorQrAutos/Models/VehicleRecord.cs`, más `int Version`, y `Clone()`.
  - `VehicleInput(string Marca, string Modelo, int Anio, long Kilometraje, string PrecioTexto, string Procedencia, string MotorLitros, string ConfiguracionMotor, string Observaciones)`.
  - `Brand.Nombre`, `Brand.Ubicacion`, `Brand.Procedencias`, `Brand.MarcasComunes`, `Brand.Configuraciones` (copiar de `Form1.cs:20-31`) y colores `SKColor` (copiar valores de `Brand.cs`).
  - `static class VehicleText`: `string BuildPayload(VehicleRecord v)`, `bool TryParsePrecio(string text, out decimal precio)`, `string NormalizeMotor(string text)`.
  - `record ValidationError(string Field, string Message)`.
  - `static class VehicleValidator`: `IReadOnlyList<ValidationError> Validate(VehicleInput input, out VehicleRecord? record)`. `Field` usa `nameof(VehicleInput.X)`.

- [ ] **Step 1:** Crear la solución, Core (con el paquete `SkiaSharp`, la versión que use Avalonia 11.3.x, y `QRCoder` 1.6.0) y Tests (xUnit, referencia a Core).
- [ ] **Step 2: Write failing tests**

```csharp
[Fact] public void Payload_IgualQueVersionWindows()
{
    var v = new VehicleRecord { Marca="Ford", Modelo="Lobo", Anio=2020, Kilometraje=45000, Precio=495000.5m,
        Procedencia="Nacional", MotorLitros="5.0 L", ConfiguracionMotor="V8", Observaciones="Único dueño",
        CreadoEn=new DateTime(2026,11,8,14,5,0) };
    Assert.Equal(
        "RANGEL VENTA DE AUTOS - Pénjamo, Gto.\nMarca: Ford\nModelo: Lobo\nAño: 2020\nKilometraje: 45,000 km\n" +
        "Precio: $495,000.50 MXN\nProcedencia: Nacional\nMotor: 5.0 L\nConfiguración del motor: V8\n" +
        "Observaciones: Único dueño\nGenerado: 08/11/2026 14:05",
        VehicleText.BuildPayload(v).Replace("\r\n", "\n"));
}
[Fact] public void Payload_SinObservaciones_OmiteLinea() // no contiene "Observaciones:"
[Theory, InlineData("$495,000 MXN", 495000), InlineData("495000.50", 495000.50)]
public void TryParsePrecio_Validos(string t, decimal e)
[Theory, InlineData(""), InlineData("abc"), InlineData("-5")] public void TryParsePrecio_Invalidos(string t)
[Theory, InlineData("5","5.0 L"), InlineData("2,5 lts","2.5 L"), InlineData("1.6l","1.6 L"), InlineData("turbo","turbo")]
public void NormalizeMotor(string i, string e)
[Fact] public void Validate_CamposVacios_DevuelveCincoErrores()
// Marca, Modelo, PrecioTexto, MotorLitros y ConfiguracionMotor con los mensajes de Form1.cs:697-701
[Fact] public void Validate_Valido_ArmaRecordConTrim()
```

Calcular el valor esperado del golden test ejecutando el `BuildPayload` original con los mismos datos: si el formato de es-MX del runtime difiere del literal, manda el original.

- [ ] **Step 3:** Run `dotnet test tests/GeneradorQr.Tests` → FAIL (no compila).
- [ ] **Step 4:** Implementar, portando la lógica de `VehicleQrService.BuildPayload`, `Form1.TryParsePrecio`, `Form1.MotorRegex` y `Form1.TryReadForm`. `Validate` no asigna `Id`, `CreadoEn` ni `Vendido`; eso lo hace quien llama.
- [ ] **Step 5:** Run `dotnet test tests/GeneradorQr.Tests` → PASS.
- [ ] **Step 6:** Commit `feat(core): modelos, texto del QR y validación`.

### Task 2: QR con logo (SkiaSharp)

**Files:** Create `src/GeneradorQr.Core/Rendering/QrRenderer.cs`, `Rendering/BrandAssets.cs`, copiar `GeneradorQrAutos/Assets/rangel-logo.png` → `src/GeneradorQr.Core/Assets/` como EmbeddedResource. Test: `tests/GeneradorQr.Tests/QrRendererTests.cs` (agregar ZXing.Net y ZXing.Net.Bindings.SkiaSharp al proyecto de tests).

**Interfaces:**
- Produces: `static class BrandAssets { SKBitmap LoadLogo(); }`, `static class QrRenderer { SKBitmap Render(string payload, int pixelsPerModule = 10, SKBitmap? logo = null); byte[] EncodePng(SKBitmap bmp); }`.

- [ ] **Step 1: Failing tests**
  - `Qr_SinLogo_Decodifica`: decodificar con ZXing da exactamente el payload.
  - `Qr_ConLogo_Decodifica`: lo mismo con `BrandAssets.LoadLogo()`.
  - `Qr_ConAcentosYEmoji_Decodifica`: con el payload `"Año ñandú 🚗"`.
  - `Qr_ConLogo_UsaEccH`: el ancho con logo es mayor que sin logo para el mismo payload.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implementar con `QRCodeGenerator.CreateQrCode(payload, ecc)`. ECC H con logo, M sin logo, como el original. Pintar `ModuleMatrix` en un `SKCanvas`, con quiet zone, módulos `Brand.Ink` y fondo blanco. Logo: caja blanca redondeada de 22 % del ancho, radio `ppm*1.5`, inset `ppm*0.5` (igual que `DrawCenterLogo`). Forzar UTF-8 (`forceUtf8: true`).
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): QR con logo multiplataforma`.

### Task 3: Ficha PNG y PDF

**Files:** Create `src/GeneradorQr.Core/Rendering/SheetRenderer.cs`. Descargar Inter (Regular, Bold, Black) del release oficial de github.com/rsms/inter a `src/GeneradorQr.Core/Assets/Fonts/` como EmbeddedResource, con `OFL.txt`. Test: `tests/GeneradorQr.Tests/SheetRendererTests.cs`.

**Interfaces:**
- Consumes: `QrRenderer`, `BrandAssets`, `Brand`.
- Produces: `static class SheetRenderer { byte[] RenderPng(VehicleRecord v, SKBitmap qr, SKBitmap? logo, int dpi = 200); byte[] RenderPdf(VehicleRecord v, SKBitmap qr, SKBitmap? logo); }`.

- [ ] **Step 1: Failing tests**
  - `Png_TamanoCarta`: decodificado mide 1700×2200 a 200 dpi.
  - `Png_QrDentroDeLaFicha_Decodifica`: ZXing encuentra el payload en la ficha.
  - `Pdf_EmpiezaConFirmaPdf`: los bytes empiezan con `%PDF`.
  - `Pdf_ConTextoUnicode_NoFalla`: observaciones con `"ñ á 🚗"` y texto de 500 caracteres.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Portar el layout de `GeneradorQrAutos/Services/VehicleSheetRenderer.cs` (mismas medidas relativas `u = ancho/100`) a una sola función `Draw(SKCanvas, SKRect page, …)`. `RenderPng` usa `SKSurface`; `RenderPdf` usa `SKDocument.CreatePdf` con página carta de 612×792 pt. Las fuentes salen de los TTF embebidos (`Segoe UI` → Inter Regular/Bold, `Segoe UI Black` → Inter Black). El ajuste de texto (`FitFont`) se replica midiendo con `SKFont.MeasureText`. Si una fuente no tiene el glifo (emoji), se usa `SKFontManager.Default.MatchCharacter`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): ficha PNG y PDF`.

### Task 4: Inventario en memoria y respaldos

**Files:** Create `src/GeneradorQr.Core/Inventory/InventoryQuery.cs`, `Inventory/InventoryStats.cs`, `Backup/BackupService.cs`. Tests: `InventoryQueryTests.cs`, `BackupServiceTests.cs`.

**Interfaces:**
- Produces:
  - `record InventoryStats(int Disponibles, int Vendidos, decimal ValorDisponible)`.
  - `static class InventoryQuery { IReadOnlyList<VehicleRecord> Search(IEnumerable<VehicleRecord> all, string? text, bool includeSold); InventoryStats Stats(IEnumerable<VehicleRecord> all); IReadOnlyList<string> DistinctMarcas(IEnumerable<VehicleRecord> all); IReadOnlyList<string> DistinctModelos(IEnumerable<VehicleRecord> all); }`.
  - `sealed class BackupService(string directory, TimeProvider clock) { Task WriteAsync(IReadOnlyList<VehicleRecord> all, CancellationToken ct = default); }`.

- [ ] **Step 1: Failing tests**
  - `Search_CadaPalabraEnAlgunCampo`: con "ford 2020" encuentra Ford Lobo 2020 y no Ford Ranger 2018.
  - `Search_NoDistingueMayusculas`: "LOBO" encuentra "Lobo".
  - `Search_OcultaVendidosSiNoSePiden`.
  - `Search_Orden_DisponiblesPrimeroLuegoIdDesc`.
  - `Stats_SumaSoloDisponibles`.
  - `Distinct_OrdenadoSinVaciosSinDuplicadosIgnorandoMayusculas`.
  - `Backup_EscribeJsonConNombreConFecha`: el archivo es `inventario-2026-11-08-1405.json` con `FakeTimeProvider` y el JSON se deserializa a la misma lista.
  - `Backup_ConservaSolo7`: con 9 llamadas quedan los 7 más nuevos.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implementar. Los campos en los que se busca son los mismos que en `VehicleRepository.Search` (marca, modelo, año, procedencia, motor_litros, configuracion_motor, observaciones), con `Contains` y `StringComparison.CurrentCultureIgnoreCase`. El JSON se escribe con `System.Text.Json` indentado.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(core): búsqueda, resumen y respaldos locales`.

### Task 5: Esquema Supabase, contrato del repositorio y autenticación

**Files:**
- Create: `supabase/schema.sql`, `src/GeneradorQr.Core/Data/IVehicleRepository.cs`, `Data/RepositoryExceptions.cs`, `src/GeneradorQr.Cloud/GeneradorQr.Cloud.csproj`, `SupabaseOptions.cs`, `SessionStore.cs`, `SupabaseAuth.cs`
- Create tests: `tests/GeneradorQr.Tests/Fakes/FakeHttpHandler.cs`, `SupabaseAuthTests.cs`, `FileSessionStoreTests.cs`
- Modify: el spec, sección 3 (la tabla agrega `version int not null default 1`; Concurrencia filtra por `version`).

**Interfaces:**
- Produces:
  - `interface IVehicleRepository`, que lanza las excepciones de abajo:
    - `Task<IReadOnlyList<VehicleRecord>> GetAllAsync(CancellationToken ct)`
    - `Task<VehicleRecord> InsertAsync(VehicleRecord v, CancellationToken ct)`
    - `Task<VehicleRecord> UpdateAsync(VehicleRecord v, CancellationToken ct)`: usa `v.Version` como versión esperada.
    - `Task<VehicleRecord> SetVendidoAsync(long id, int expectedVersion, bool vendido, CancellationToken ct)`
    - `Task DeleteAsync(long id, int expectedVersion, CancellationToken ct)`
  - Excepciones: `ConcurrencyConflictException`, `OfflineException`, `ServerUnavailableException`, `AuthRequiredException`, `InvalidCredentialsException` (todas `: Exception`, en `GeneradorQr.Core.Data`).
  - `record SupabaseOptions(string Url, string AnonKey) { static SupabaseOptions Load(string jsonPath); }`.
  - `interface ISessionStore { string? Load(); void Save(string refreshToken); void Clear(); }`, `FileSessionStore(string path)`.
  - `sealed class SupabaseAuth(HttpClient http, SupabaseOptions opt, ISessionStore store, TimeProvider clock)`:
    - `Task SignInAsync(string email, string password, CancellationToken ct)`
    - `Task<bool> TryRestoreAsync(CancellationToken ct)`
    - `Task<string> GetAccessTokenAsync(CancellationToken ct)`
    - `void SignOut()`

- [ ] **Step 1:** Escribir `supabase/schema.sql`: el SQL del spec, más la columna `version`, un trigger `before update` que hace `new.version = old.version + 1; new.actualizado_en = now();`, RLS y la policy. Agregar al inicio un comentario con los pasos manuales: desactivar "Allow new users to sign up" y crear el usuario de la agencia.
- [ ] **Step 2: Failing tests** (con `FakeHttpHandler`, que registra los requests y responde con lo que se programe)
  - `SignIn_Ok_GuardaRefreshToken`: hace POST a `/auth/v1/token?grant_type=password` con el header `apikey`.
  - `SignIn_400_LanzaInvalidCredentials`.
  - `GetAccessToken_FaltanMenosDe60s_Refresca`: hace POST a `grant_type=refresh_token` y guarda el nuevo refresh token.
  - `TryRestore_SinToken_False`.
  - `TryRestore_TokenRevocado_LimpiaYFalse`.
  - `SinRed_LanzaOffline`: un `HttpRequestException` o un timeout se convierten en `OfflineException`.
  - `FileSessionStore_RoundTrip_YClear`.
- [ ] **Step 3:** Run → FAIL.
- [ ] **Step 4:** Implementar. La expiración se calcula con `expires_in` + `clock`. En `FileSessionStore`, en macOS/Linux, se ejecuta `File.SetUnixFileMode(path, UserRead|UserWrite)`.
- [ ] **Step 5:** Run → PASS.
- [ ] **Step 6:** Commit `feat(cloud): esquema, contrato de repositorio y autenticación`.

### Task 6: Repositorio Supabase

**Files:** Create `src/GeneradorQr.Cloud/SupabaseVehicleRepository.cs`, `Dto/VehicleRow.cs` (snake_case con `JsonPropertyName`). Test: `SupabaseVehicleRepositoryTests.cs`.

**Interfaces:**
- Consumes: `SupabaseAuth.GetAccessTokenAsync`, `IVehicleRepository`, excepciones.
- Produces: `sealed class SupabaseVehicleRepository(HttpClient http, SupabaseOptions opt, SupabaseAuth auth) : IVehicleRepository`.

- [ ] **Step 1: Failing tests**
  - `GetAll_PideOrdenYHeaders`: GET `/rest/v1/vehiculos?select=*&order=vendido.asc,id.desc` con `apikey` y `Authorization: Bearer`.
  - `GetAll_ConvierteFechasAHoraLocal`: `"2026-11-08T20:05:00+00:00"` → `DateTimeKind.Local` equivalente.
  - `GetAll_PrecioDecimalExacto`: `495000.50` → `495000.50m`.
  - `Insert_NoEnviaIdNiVersion_DevuelveFilaDelServidor`: POST con `Prefer: return=representation`.
  - `Update_FiltraPorIdYVersion`: PATCH `?id=eq.5&version=eq.3`.
  - `Update_RespuestaVacia_LanzaConflicto`: `[]` → `ConcurrencyConflictException`.
  - `Delete_RespuestaVacia_LanzaConflicto`.
  - `SetVendido_EnviaSoloVendido`.
  - `Http401_LanzaAuthRequired`.
  - `Http5xx_LanzaServerUnavailable`.
  - `HttpRequestException_LanzaOffline`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implementar. Un helper privado `SendAsync` centraliza los headers y el mapeo de errores. Insert/Update envían un `VehicleRow` sin `id`, `version`, `creado_en` ni `actualizado_en`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(cloud): repositorio Supabase con bloqueo optimista`.

### Task 7: Herramienta de migración

**Files:** Create `src/GeneradorQr.Migrate/GeneradorQr.Migrate.csproj` (Exe, con Microsoft.Data.Sqlite 8.0.11), `SqliteReader.cs`, `Migrator.cs`, `Program.cs`. Test: `MigratorTests.cs` (con SQLite en un archivo temporal y `FakeRepository`).

**Interfaces:**
- Consumes: `IVehicleRepository`, `SupabaseAuth`.
- Produces:
  - `static class SqliteReader { IReadOnlyList<VehicleRecord> ReadAll(string dbPath); }`.
  - `sealed class Migrator(IVehicleRepository repo) { Task<int> RunAsync(IReadOnlyList<VehicleRecord> local, bool force, CancellationToken ct); }`.
  - CLI: `GeneradorQr.Migrate <ruta vehiculos.db> <supabase.json> [--force]`. Pide el correo y la contraseña por consola.

- [ ] **Step 1: Failing tests**
  - `SqliteReader_LeeEsquemaActual`: crea una base con el DDL de `VehicleRepository.EnsureDatabase` (incluidas las columnas migradas) y lee el precio texto `"495000.50"`.
  - `SqliteReader_BaseAntiguaSinColumnasNuevas`: `vendido=false`, `ActualizadoEn=null`.
  - `Migrar_TablaRemotaConDatos_SeNiegaSinForce`: lanza `InvalidOperationException` con el mensaje "La base en la nube ya tiene N autos. Usa --force para migrar de todos modos." y no inserta nada.
  - `Migrar_ConservaVendidoYCreadoEn`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implementar. `ReadAll` porta `VehicleRepository.ReadVehicle` y verifica qué columnas existen con `PRAGMA table_info`. `RunAsync` inserta en orden de id ascendente. El insert debe aceptar `creado_en` cuando viene de la migración: agregar `bool preserveCreatedAt` interno en `SupabaseVehicleRepository`, expuesto solo como `InsertMigratedAsync` en la clase concreta.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(migrate): importar SQLite existente a Supabase`.

### Task 8: App Avalonia: arranque, configuración y login

**Files:** Create `src/GeneradorQr.App/` (plantilla `avalonia.app` con CommunityToolkit.Mvvm), `AppPaths.cs`, `Log.cs`, `supabase.example.json`, `ViewModels/LoginViewModel.cs`, `Views/LoginWindow.axaml(.cs)`, `App.axaml.cs`. Tests: `LoginViewModelTests.cs` (el proyecto de tests referencia App).

**Interfaces:**
- Consumes: `SupabaseAuth`, `SupabaseOptions.Load`.
- Produces:
  - `static class AppPaths { string DataDir; string SessionFile; string LogFile; string ConfigFile; string BackupDir; }`:
    - `DataDir = LocalApplicationData/GeneradorQR`.
    - `BackupDir = MyDocuments/GeneradorQR/respaldos`.
    - `ConfigFile = supabase.json` junto al ejecutable (en macOS, `Contents/Resources` del `.app`).
  - `static class Log { void Error(Exception ex, string context); }`, que agrega al final de `LogFile`.
  - `LoginViewModel(SupabaseAuth auth)`: `Email`, `Password`, `Error`, `IsBusy`, `IAsyncRelayCommand SignInCommand` y el evento `SignedIn`.

- [ ] **Step 1: Failing tests**
  - `SignIn_CredencialesMalas_MuestraMensaje`: `Error == "Correo o contraseña incorrectos."`.
  - `SignIn_SinRed_MuestraSinConexion`.
  - `SignIn_Ok_DisparaSignedIn`.
  - `SignIn_CamposVacios_NoLlamaServidor`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implementar. Al arrancar, `App` carga la configuración (si falta `supabase.json`, muestra un error y la ruta esperada), intenta `TryRestoreAsync` y abre MainWindow o LoginWindow según el resultado. La ventana de login lleva el logo, Enter envía y el botón se desactiva mientras `IsBusy`. Hay un handler global de excepciones no capturadas que manda a `Log.Error` y muestra un diálogo.
- [ ] **Step 4:** Run tests → PASS. Run `dotnet run --project src/GeneradorQr.App` con un `supabase.json` real → aparece el login y entra.
- [ ] **Step 5:** Commit `feat(app): arranque, configuración y login`.

### Task 9: MainViewModel: formulario, guardado, conexión y recarga

**Files:** Create `ViewModels/VehicleFormViewModel.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/ConnectionMonitor.cs`. Tests: `MainViewModelTests.cs`, `tests/.../Fakes/FakeRepository.cs`.

**Interfaces:**
- Consumes: `IVehicleRepository`, `InventoryQuery`, `VehicleValidator`, `VehicleText`, `QrRenderer`, `BackupService`, `TimeProvider`.
- Produces:
  - `VehicleFormViewModel`:
    - Propiedades de texto de cada campo, `IReadOnlyDictionary<string,string> Errors` y `VehicleRecord? Editing`.
    - `bool IsDirty`, `void Load(VehicleRecord v)`, `void Clear()`, `bool TryBuild(out VehicleRecord v)`.
    - `NormalizeOnBlur(string field)` (precio y motor).
  - `MainViewModel(IVehicleRepository repo, BackupService backup, TimeProvider clock)`:
    - `Form`, `ObservableCollection<VehicleRecord> Visible`, `SearchText`, `IncludeSold`, `Stats`.
    - `IsOnline`, `StatusMessage`, `SKBitmap? Preview`.
    - Comandos: `SaveCommand`, `NewCommand`, `RefreshCommand`, `ToggleVendidoCommand(VehicleRecord)`, `DeleteCommand(VehicleRecord)`.
    - `Task InitializeAsync()`: carga el inventario y escribe el respaldo.
    - `void OnWindowActivated()`.
  - `ConnectionMonitor(TimeProvider clock)`: `bool IsOnline`, `Task RunUntilOnlineAsync(Func<Task> probe, CancellationToken ct)`, con reintentos cada 5 s.

- [ ] **Step 1: Failing tests** (`FakeRepository` en memoria con modos para fallar: offline, conflicto, 401)
  - `Initialize_CargaInventarioYEscribeRespaldo`.
  - `Guardar_Nuevo_InsertaYLimpiaFormulario`.
  - `Guardar_Edicion_ActualizaConVersion`.
  - `Guardar_SinRed_ConservaFormulario`: `IsOnline=false`, el formulario intacto, `StatusMessage == "Sin conexión — reintentando…"` y `SaveCommand.CanExecute == false` hasta que el monitor reporta online; luego vuelve a `true`.
  - `Guardar_Conflicto_RecargaYAvisa`: mensaje exacto del spec y el formulario con la versión del servidor.
  - `AuthRequired_DisparaEventoSesionVencida`.
  - `ServidorCaido_MuestraMensajeServidor`: `ServerUnavailableException` → `StatusMessage` empieza con "No se pudo conectar con el servidor." y menciona reactivar el proyecto en Supabase.
  - `Busqueda_FiltraEnMemoria_SinLlamarRepo`.
  - `Recarga_Cada60s_ConFakeTimeProvider`.
  - `OnWindowActivated_Recarga`.
  - `Preview_SeActualizaConFormularioValido_ConDebounce300ms`.
  - `Delete_Conflicto_RecargaYAvisa`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implementar. `MainViewModel` no conoce Avalonia: usa solo CommunityToolkit.Mvvm y SkiaSharp. El temporizador de 60 s y el debounce usan `clock.CreateTimer`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat(app): lógica de la pantalla principal`.

### Task 10: MainWindow: vista, inventario, exportar, atajos

**Files:** Create `Views/MainWindow.axaml(.cs)`, `Services/IFileActions.cs`, `Services/FileActions.cs`. Test: `FileActionsTests.cs` (solo la parte de archivos temporales y nombres).

**Interfaces:**
- Consumes: `MainViewModel`, `SheetRenderer`, `QrRenderer`.
- Produces: `interface IFileActions { Task OpenPdfAsync(VehicleRecord v); Task SavePngAsync(VehicleRecord v, Window owner); Task SaveQrAsync(VehicleRecord v, Window owner); Task CopyQrAsync(VehicleRecord v, Window owner); }`.

- [ ] **Step 1: Spike del portapapeles en macOS** (el código es desechable). Probar copiar el PNG del QR con la API de portapapeles de Avalonia 11.3 y pegarlo en WhatsApp y en Vista Previa. Anotar el resultado en el commit. Si falla, `CopyQrAsync` escribe el PNG en un archivo temporal y lo copia como archivo (formato de archivos del portapapeles).
- [ ] **Step 2: Failing tests**
  - `NombreArchivo_SinCaracteresInvalidos`: `"Ford Lobo 2020/ñ"` → `"Ford-Lobo-2020-ñ"`.
  - `OpenPdf_EscribeEnTempYLlamaAbrir`: el lanzador se inyecta como `Action<string>`.
- [ ] **Step 3:** Run → FAIL; implementar `FileActions`. Abrir el PDF con `open` en macOS y `Process.Start(UseShellExecute=true)` en Windows. Los diálogos de guardar usan `StorageProvider`. Run → PASS.
- [ ] **Step 4:** Construir `MainWindow` con la distribución de `Form1`:
  - Encabezado con resumen, indicador de conexión, "Actualizar" y menú "Cerrar sesión".
  - Formulario con `AutoCompleteBox` para marca y modelo (fuente: `Brand.MarcasComunes` ∪ `DistinctMarcas`) y `ComboBox` para procedencia y configuración.
  - Vista previa del QR y botones.
  - `DataGrid` del inventario con doble clic o Enter para editar, Supr para eliminar con confirmación y menú contextual.
  - Barra roja de sin conexión.
  - Atajos con `KeyGesture` usando `PlatformHotkeyConfiguration.CommandModifiers` (⌘ en Mac, Ctrl en Windows): S, N, P, E, Shift+C, F, y Esc para limpiar la búsqueda.
  - Al cerrar con cambios sin guardar, se pide confirmación.
- [ ] **Step 5:** Verificación manual en Windows con `dotnet run`: capturar, editar, buscar, marcar vendido, eliminar, Ficha PDF, PNG, Guardar QR, Copiar QR y atajos.
- [ ] **Step 6:** Commit `feat(app): pantalla principal`.

### Task 11: Empaquetado macOS, documentación y aceptación

**Files:** Create `build/build-mac.sh`, `build/Info.plist`, `build/instalar.command`, `build/build-windows.ps1`. Modify `README.md` (agregar la sección de la versión Mac/nube: configuración de Supabase, compilación e instalación, la pausa de 7 días y dónde están los respaldos y el log).

- [ ] **Step 1:** `build-mac.sh [arm64|x64]`:
  1. `dotnet publish src/GeneradorQr.App -c Release -r osx-$ARCH --self-contained -p:UseAppHost=true`.
  2. Armar `dist/Generador QR.app/Contents/{MacOS,Resources}`, con `Info.plist` (`CFBundleIdentifier com.rangel.generadorqr`, `CFBundleExecutable GeneradorQr.App`, `LSMinimumSystemVersion 12.0`), el icono `.icns` generado del logo con `sips` + `iconutil` y `supabase.json` en Resources.
  3. `codesign --force --deep --sign - "dist/Generador QR.app"`.
  4. Comprimir con `ditto -c -k --keepParent`.
- [ ] **Step 1b:** `build-windows.ps1`: `dotnet publish src/GeneradorQr.App -c Release -r win-x64 --self-contained` y copia `supabase.json` junto al `.exe`. Verificar que la carpeta publicada abre en Windows.
- [ ] **Step 2:** `instalar.command`: copia el `.app` junto al script a `/Applications`, ejecuta `xattr -dr com.apple.quarantine` y lo abre.
- [ ] **Step 3:** Verificación en una Mac. Run `bash build/build-mac.sh arm64` → `codesign -dv "dist/Generador QR.app"` muestra `Signature=adhoc`. Instalar con doble clic en `instalar.command` → la app abre sin bloqueo.
- [ ] **Step 4:** Ejecutar la lista de aceptación manual del spec (sección 6, puntos 1–8) en las 2 Macs y anotar los resultados en el PR.
- [ ] **Step 5:** Commit `build: empaquetado macOS e instalación`.
