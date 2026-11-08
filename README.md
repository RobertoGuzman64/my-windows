# MyWindows

Entorno personal para Windows, reproducible y extensible desde el repositorio. La calidad, el control de cambios y la recuperación son requisitos del producto, aunque inicialmente tenga pocas funciones. PHM es la referencia de disciplina, **no una dependencia**.

## Primer comando

Requisitos: Windows, PowerShell 5.1 o posterior y SDK .NET 10 compatible con `global.json`. Para las pruebas de repositorios Git, disponer de Git en `PATH`.

```powershell
.\mw.cmd all only
```

También se puede ejecutar `./eng/mw.ps1 all only` desde PowerShell. Este circuito **no formatea fuentes ni instala personalizaciones**: verifica formato, compila Debug/Release, ejecuta MSTest, valida los perfiles y audita el repositorio. Crea archivos de build/pruebas y logs ignorados en `artifacts/quality/`. En caso de fallo muestra diagnóstico y ruta del registro íntegro.

```powershell
.\mw.cmd check only
.\mw.cmd tests only
.\mw.cmd build only
.\mw.cmd validate only
.\mw.cmd audit only
.\mw.cmd format
```

`format` es el único comando del tooling que modifica fuentes deliberadamente. El runner también admite la entrada histórica `./eng/mw.ps1 -Task all`. GitHub Actions ejecuta el mismo `all only`.

## Capacidades presentes

```powershell
dotnet run --project src/MyWindows.Cli -- profiles validate profiles
dotnet run --project src/MyWindows.Cli -- profiles resolve profiles
dotnet run --project src/MyWindows.Cli -- repo inspect .
dotnet run --project src/MyWindows.Cli -- file hash 'C:\ruta\archivo.txt'
dotnet run --project src/MyWindows.Cli -- plan profiles . --role repository
```

El módulo `repo-shortcut` puede crear, verificar, reparar y retirar un acceso del menú Inicio **solo para el usuario actual**, usando un recibo de propiedad. Los comandos de escritura (`apply`, `repair`, `remove`) **no deben usarse en el equipo habitual hasta superar la aceptación de recuperación G4 en una VM**. `base.json` está vacío; la selección de `repository` es expresa. La automatización Git se ofrece como candidata y no se registra por defecto. No existen todavía extensiones nativas instalables de Command Palette ni de Explorer.

## Documentación: una fuente de verdad por materia

- [Documento fundacional](docs/MYWINDOWS_DOCUMENTO_FUNDACIONAL.md): propósito, principios y visión de producto.
- [Arquitectura y contratos](docs/ARQUITECTURA.md): capas, perfiles, módulos, comportamientos e invariantes.
- [Desarrollo](docs/DESARROLLO.md): normas, pruebas, revisión y definición de terminado.
- [Tooling](docs/TOOLING.md): comandos, gates, evidencias, diagnóstico y CI.
- [Compatibilidad](docs/COMPATIBILIDAD.md): inventarios, supuestos pendientes e integraciones documentadas.
- [Aceptación en Windows](docs/ACEPTACION_WINDOWS.md): matriz G0–G7, recuperación y pruebas en VM.
- [Decisiones](docs/DECISIONES.md): elecciones vigentes, restricciones y asuntos abiertos.

Los logs y capturas de aceptación son evidencias por ejecución, no documentación normativa. No se declaran gates de integración aprobados por el solo hecho de existir tests o scripts.
