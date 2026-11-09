# Generador de QR para vehículos

Aplicación de escritorio para Windows que captura información de vehículos y crea un código QR con todos los datos. El QR contiene texto, no una dirección web, por lo que puede leerse sin conexión a internet.

La cabecera incluye el logotipo de **Rangel Venta de Autos** proporcionado para el proyecto.

## Datos incluidos

- Marca y modelo.
- Año.
- Kilometraje.
- Precio en MXN.
- Procedencia: nacional, americano regularizado o americano no regularizado.
- Motor/litraje, por ejemplo `5.0 L`.
- Configuración del motor, por ejemplo `V8`.
- Observaciones opcionales.

El folio fue omitido intencionalmente.

## Almacenamiento local

Los registros se guardan en SQLite en:

`%LOCALAPPDATA%\GeneradorQrAutos\vehiculos.db`

No se usa servidor, cuenta en la nube ni conexión a internet durante el uso normal.

## Compilar

Requiere .NET 10 SDK y una máquina Windows. La primera compilación necesita restaurar los paquetes NuGet `Microsoft.Data.Sqlite` y `QRCoder`; después se puede publicar como aplicación autónoma.

```powershell
dotnet restore .\GeneradorQrAutos\GeneradorQrAutos.csproj
dotnet build .\GeneradorQrAutos\GeneradorQrAutos.csproj -c Release
```

Para entregar una carpeta que no dependa de que .NET esté instalado:

```powershell
dotnet publish .\GeneradorQrAutos\GeneradorQrAutos.csproj -c Release -r win-x64 --self-contained true
```

La carpeta publicada se puede copiar a otra computadora Windows y ejecutar sin conexión.

## Funciones

- **Vista previa en vivo:** el QR se actualiza mientras escribes; no hace falta guardar para verlo.
- **QR con el logotipo** de Rangel al centro, con corrección de errores alta para que se siga leyendo.
- **Ficha para imprimir** tamaño carta: logo, marca y modelo, precio destacado, datos principales y QR grande. Se puede guardar como PNG o imprimir (para PDF elige la impresora «Microsoft Print to PDF»).
- **Copiar QR** al portapapeles para pegarlo en WhatsApp, Word, etc.
- **Inventario:** búsqueda instantánea, editar, marcar como vendido o disponible y eliminar.
- **Resumen** en el encabezado: autos disponibles, valor del inventario y autos vendidos.
- Validación en el mismo formulario, autocompletado de marcas y modelos, y litraje normalizado (`5` → `5.0 L`).

Las bases de datos de versiones anteriores se actualizan solas al abrir la aplicación (se agregan las columnas `vendido` y `actualizado_en`).

## Uso

1. Captura los datos del vehículo; la vista previa del QR aparece en cuanto los campos con * están completos.
2. Pulsa **Generar QR y guardar** (Ctrl+S).
3. Usa **Ficha PNG**, **Imprimir ficha**, **Guardar QR** o **Copiar QR** según lo que necesites.
4. Para modificar un registro, haz doble clic sobre él en el inventario (o selecciónalo y pulsa Enter), cambia los datos y pulsa **Guardar cambios**.
5. Haz clic derecho sobre un vehículo para marcarlo como vendido o eliminarlo.

### Atajos de teclado

| Atajo | Acción |
| --- | --- |
| Ctrl+S | Guardar |
| Ctrl+N | Nuevo vehículo |
| Ctrl+P | Imprimir ficha |
| Ctrl+E | Guardar ficha como PNG |
| Ctrl+Shift+C | Copiar QR |
| Ctrl+F | Buscar en el inventario |
| Enter / Supr | Abrir / eliminar el vehículo seleccionado |
