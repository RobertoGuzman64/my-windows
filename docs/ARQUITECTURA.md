# Arquitectura y contratos vigentes

La fuente normativa del propósito es [el documento fundacional](MYWINDOWS_DOCUMENTO_FUNDACIONAL.md). Este documento describe **lo que realmente implementa el código**, sus contratos y sus límites. Los cambios de API/JSON necesitan compatibilidad explícita y pruebas.

## Capas y ownership

| Capa | Responsabilidad | Dependencias permitidas |
| --- | --- | --- |
| `src/MyWindows.Domain` | Reglas de identificación, perfiles, módulos y resolución determinista | Ningún otro proyecto ni paquete |
| `src/MyWindows.Application` | Casos de uso, planificación, ciclo de vida, Git y hashing | Solo Domain |
| `src/MyWindows.Infrastructure` | Sistema de archivos, COM ShellLink, procesos Git, inspección de Windows | Application y Domain |
| `src/MyWindows.Cli` | Argumentos, composición, salida JSON y códigos de retorno | Componentes necesarios; no reglas de negocio duplicadas |
| `tests/MyWindows.Tests` | Comportamiento, integraciones controladas y límites arquitectónicos | Proyectos de producción |
| `eng/` | Tooling de calidad, inventario y procedimientos específicos Windows | No es un framework de negocio alternativo |

`Directory.Build.targets` impone ARCH001–ARCH004 a Domain y Application; las pruebas de arquitectura completan esos controles. Nunca incorporar efectos Windows en Domain/Application ni copiar reglas Git en automatizaciones o adaptadores futuros.

## Perfiles JSON v1 y resolución

Cada fichero `profiles/<id>.json` contiene **solo** `schemaVersion`, `id`, `modules`:

```json
{
  "schemaVersion": 1,
  "id": "base",
  "modules": []
}
```

- Versión exactamente `1`. `id` ASCII minúsculo: empieza por `a-z` y continúa con `a-z`, dígitos o guiones; longitud 1–64. Nombre de archivo igual al ID. Sin propiedades ni claves duplicadas desconocidas, comentarios o comas finales.
- `modules` es un array obligatorio sin IDs repetidos. No almacena rutas locales, identidades, secretos ni hardware.
- `base` es obligatorio. Los roles se seleccionan con `--role <id>`, se ordenan por identificador ordinal y se añade opcionalmente al final `--machine <id>`. No se infieren identidades de equipos. Roles duplicados/solapados, perfiles inexistentes y referencias inválidas se rechazan.
- La unión de módulos es única y ordenada. `ModuleResolver` exige módulos registrados, dependencias seleccionadas y ausencia de conflictos; **no** activa dependencias por sorpresa.
- Actualmente `base` no instala nada. `repository` selecciona el único módulo instalable declarado, `repo-shortcut` v1.0.0.

## Planificación y operaciones del acceso de Inicio

CLI: `<comando> <directorio-perfiles> <directorio-repositorio> [--role <id>]... [--machine <id>]`, con los verbos `plan`, `apply`, `verify`, `status`, `repair`, `remove`. El resultado JSON presenta `resources`/`isFeasible` para `plan`, además de estados específicos de operación en los otros comandos. Salidas: `0` éxito, `1` fallo de entorno/operación, `2` sintaxis, `3` bloqueo/postcondición no satisfecha.

`plan` solo observa. Puede proponer `create`, `noChange`, `repair` o `blocked`, pero **no acredita permisos efectivos ni reserva el recurso**. `apply` instala solo desde ausencia total o no modifica un recurso propio íntegro; **nunca repara automáticamente**. `verify` exige `.lnk` propio íntegro y destino presente. `status` observa. `repair` es explícito y no sobrescribe archivos ajenos o alterados. `remove` elimina solo recursos cuya propiedad se ha verificado y es idempotente si faltan.

La ubicación se resuelve con `Environment.SpecialFolder.Programs` para el usuario actual: `MyWindows — Abrir repositorio.lnk`. El recibo v1 se guarda en `%LOCALAPPDATA%\MyWindows\ownership\repo-shortcut.json`; contiene `schemaVersion`, `moduleId`, `repositoryPath`, `ownerTag`, `sha256` y `payloadBase64`. Se valida huella SHA-256, destino y etiqueta COM `IShellLinkW`, sin suponer propiedad por nombre. **El recibo no es una firma criptográfica.**

Estados: `absent`, `owned`, `recoverableMissing`, `foreign`, `modified`, `invalid`. Las operaciones dudosas bloquean. La creación prepara un `.lnk` exclusivo, escribe recibo exclusivo y publica sin sobrescribir; una interrupción puede requerir `repair` o intervención. No existe transacción ACID que abarque `.lnk` y recibo, ni garantía frente a todos los cambios concurrentes. Rechazar reparse points y recibos corruptos, sin limpiar indiscriminadamente.

## Acción Git de solo lectura

`repo inspect <ruta>` devuelve `rootPath`, `branch` (o SHA abreviada para detached HEAD), `isDetached` y `hasChanges`. `Application/Repositories/RepositorySummaryService` interpreta Git mediante `IGitCommandRunner`; `Infrastructure/GitCommandRunner` ejecuta Git sin shell y con límites de 10 s y 1 MiB por salida. Se fija `GIT_OPTIONAL_LOCKS=0`, se desactiva el prompt y no se simula árbol limpio ante error. Retorno JSON/0 o error/1, sintaxis/2. La ruta y la rama pueden ser datos sensibles. La fotografía Git puede cambiar inmediatamente después de consultarse.

## SHA-256 de archivos locales

`file hash <ruta>` produce `fullPath`, `lengthBytes` y `sha256` en hexadecimal mayúsculo. `Application/Files/FileHashService` procesa por streaming usando `IFileReadGateway`; `Infrastructure/LocalFileReadGateway` valida y abre solo lectura. No se aceptan directorios, reparse points o UNC en el alcance inicial de Windows. El CLI limita la ejecución a 30 s; no entrega digest si hay cancelación, longitud incorrecta o cambios observables de metadatos. No es una instantánea forense contra modificaciones adversariales de fecha/tamaño.

## Automatización candidata (sin activación implícita)

`eng/automation/Manage-RepoInspection.ps1` gestiona una única tarea Task Scheduler `MyWindows.RepoInspection` por usuario, con horario y repositorio expresos, principal interactivo y privilegio limitado; `Invoke-RepoInspection.ps1` reutiliza **el comando Git existente** y almacena un `latest.json` local. Plan y estado son de consulta; instalar, habilitar, modificar o retirar exigen propiedad válida y consentimiento. El recibo de tarea vincula SID, GUID, acción, horario y huellas del runner/binario. No se adoptan tareas ajenas ni se borran los informes al retirar. Requiere sesión interactiva; no constituye un servicio residente.

## Integraciones deliberadamente pendientes

No existe un host nativo de Command Palette, ni una DLL `IExplorerCommand`, ni paquete MSIX desplegable. Futuras superficies reutilizarán Application; la construcción del menú Explorer debe permanecer rápida y delegar tareas largas fuera del proceso shell. Véase [compatibilidad](COMPATIBILIDAD.md) y [aceptación Windows](ACEPTACION_WINDOWS.md).
