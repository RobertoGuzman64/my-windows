# Tooling y controles

## Contrato del runner

Desde la raíz, en PowerShell o `cmd`:

```powershell
.\mw.cmd all only
.\mw.cmd check only
.\mw.cmd tests only
.\mw.cmd build only
.\mw.cmd validate only
.\mw.cmd audit only
.\mw.cmd format
```

Entradas equivalentes `./eng/mw.ps1 all only` y `./eng/mw.ps1 -Task all`. Se mantienen nombres de tareas existentes (`format`, `check`, `tests`, `build`, `validate`, `all`), y se añade `audit` como comprobación estructural. `only` indica **diagnóstico sin reformatear ni modificar Windows**. `format only` se rechaza expresamente. El runner puede restaurar paquetes, compilar, crear fixtures temporales y escribir logs ignorados.

| Comando | Pasos efectivos | Cambia código fuente |
| --- | --- | --- |
| `format` | restore, `dotnet format` | **Sí**, explícitamente |
| `check only` | restore, formato sin cambios, build Debug, auditoría repo | No |
| `tests only` | restore, build Debug, MSTest (TRX obligatorio) | No |
| `build only` | restore, build Release | No |
| `validate only` | restore, build Release, perfiles validate/resolve, auditoría repo | No |
| `audit only` | auditoría PowerShell/XML/JSON/docs y Git tracked | No |
| `all only` | restore → format-check → build Debug → MSTest → build Release → perfiles validate/resolve → audit | No |

Los comandos parciales resuelven sus prerrequisitos; no dependen de haber ejecutado un comando previo. La CLI de perfiles forma parte de `validate`, pero la aceptación de efectos Windows se ejecuta **aparte y bajo confirmación explícita**.

## UX y diagnóstico

Por defecto, cada paso imprime nombre, resultado, duración y, en tests, contadores obtenidos del informe **TRX real**, no de texto dependiente del idioma. Si no hay tests o faltan contadores, `tests` falla. El primer gate con error detiene el resto, muestra hasta las últimas 100 líneas y la ubicación del log íntegro, sin declarar los pendientes como PASS.

Cada ejecución genera archivos en `artifacts/quality/<UTC-PID-id>/`: un log por paso y, cuando corresponde, `test-results/tests.trx`. Los logs pueden contener rutas de usuario: revisar antes de publicarlos. Se ignoran en Git; no son una caché de validación, y **ninguna ejecución anterior evita repetir un gate**. El runner no firma evidencias ni crea certificaciones de compatibilidad.

## Auditoría estructural

`eng/maintenance/Test-Repository.ps1` valida archivos esenciales, sintaxis de scripts PowerShell, XML de proyectos/solución, JSON de perfiles y SDK, enlaces Markdown relativos y, en un checkout Git, que `bin/`, `obj/`, `TestResults/` y `artifacts/` no estén versionados. Complementa `dotnet format`, analizadores y tests de arquitectura; no inspecciona semánticamente todo C# ni sustituye los ensayos de Windows.

## CI y revisión

`.github/workflows/ci.yml` utiliza Windows runner, SDK definido por `global.json` y `./eng/mw.ps1 all only` con permisos mínimos de repositorio. El primer PASS local no prueba que CI funcione: hay que observar un job real del **mismo commit**. No se publica ni instala software desde el workflow de calidad. Las pruebas manuales G0–G7 y scripts de aceptación permanecen fuera de CI hasta disponer de entorno controlado.

## Diagnóstico de problemas habituales

- SDK incompatible: `dotnet --version`; revisar `global.json` y el SDK realmente instalado.
- Fallo de formato: ejecutar `./mw.cmd format` **de forma consciente**, revisar el diff y repetir `all only`.
- Fallo de build: consultar log de `debug-build`/`release-build` y conservar la primera regla del analizador; no suprimir globalmente.
- Fallo MSTest: abrir el TRX y el log del paso. El runner requiere que todas las pruebas detectadas terminen correctamente.
- Error de auditoría: revisar la ruta exacta o enlace roto y corregir la fuente, no relajar una regla para ocultar el resultado.
- Fallo de G4/G7: detenerse; nunca repetir borrados manuales indiscriminados sobre recursos ajenos. Consultar [aceptación Windows](ACEPTACION_WINDOWS.md).

### Aclaración sobre PHM

De PHM se replica el **contrato operativo** (un comando, gates, fallo temprano, diagnósticos, ownership y evidencia honesta), no caches, parsers de métricas, validadores de UI WPF o release tooling que MyWindows no utiliza.
