# Compatibilidad e inventario

La compatibilidad Windows es una **afirmación que exige medición**, no una consecuencia de compilar. El 19-10-2026 se ha ejecutado el circuito .NET en un Windows del usuario, con 74 tests correctos en la revisión anterior a esta poda. Siguen sin aportarse inventarios completos del sobremesa y portátil ni aceptación de reconstrucción en VM. Ninguna versión mínima de Windows, integración de Explorer o PowerToys se declara certificada.

## Obtener inventarios (solo lectura)

Desde cada equipo, bajo el usuario habitual y tras revisar los scripts:

```powershell
New-Item -ItemType Directory -Path ./local -Force | Out-Null
./eng/inventory/Get-MyWindowsInventory.ps1 -Role desktop |
    Set-Content -LiteralPath ./local/inventory-desktop.json -Encoding UTF8
```

En el portátil sustituir `desktop` por `laptop` y usar `inventory-laptop.json`. Reunir ambos archivos **de forma privada** en el mismo checkout y comparar:

```powershell
./eng/inventory/Compare-MyWindowsInventories.ps1 `
    -DesktopPath ./local/inventory-desktop.json `
    -LaptopPath ./local/inventory-laptop.json |
    Set-Content -LiteralPath ./local/comparativa.md -Encoding UTF8
```

`local/` se ignora por Git. Los inventarios pueden contener datos identificativos indirectos; revisarlos antes de compartir. No cambiar la política global de ejecución de PowerShell para forzar el procedimiento.

## Matriz de compatibilidad vigente

| Capacidad | Sobremesa | Portátil | VM limpia | Comprobación necesaria |
| --- | --- | --- | --- | --- |
| Windows (edición, build, arquitectura) | Pendiente | Pendiente | Pendiente | Inventario en cada equipo |
| Permisos y carpeta Programas por usuario | Pendiente | Pendiente | Pendiente | Cuenta local sin admin, ruta real y acceso |
| PowerShell, Git y SDK .NET | Parcial: build local comunicado | Pendiente | Pendiente | Versión exacta y acciones reales |
| WinGet y PowerToys | Pendiente | Pendiente | Pendiente | Inventario + prueba interactiva |
| `repo-shortcut`: instalación/retirada real | Pendiente | Pendiente | Pendiente | G4 en VM y después equipo objetivo |
| Extensión Command Palette | No implementada | No implementada | No implementada | Plantilla oficial, MSIX y G5 |
| Extensión Explorer moderno | No implementada | No implementada | No implementada | `IExplorerCommand`, paquete y G6 |
| Automatización Git Task Scheduler | No aceptada | No aceptada | No aceptada | G7 y uso real justificado |

`PASS`, `FAIL`, `BLOCKED`, `NOT RUN` y `NOT APPLICABLE` solo se usan con evidencia por equipo/build. El test de una VM no certifica todos los equipos.

## Mecanismos elegidos y límites

- **Inicio:** `IShellLinkW`/`IPersistFile` y ubicación Programas del usuario, en lugar de escritura en registro global. Ya existe código/recibo, pero falta el ensayo G4 completo. Rechazar colisiones y comprobar eliminación sin residuos.
- **PowerToys Command Palette:** candidato de host fuera de proceso, con plantilla oficial de la versión instalada, extensión C#/WinRT y empaquetado soportado. No fabricar manualmente COM/MSIX ni GUID de ejemplo como si fueran instalables. Evaluar versión PowerToys, Windows App SDK y prueba de descubrimiento. Usará la acción Git existente.
- **Explorer moderno:** comando `IExplorerCommand` con registro en paquete, no una inserción arbitraria. El cálculo SHA-256 debe ejecutarse fuera del proceso Explorer; `GetState`/`GetTitle` no leerán archivos grandes. Validar tiempo, aislamiento, selección, crash y retirada.
- **Automatización:** Task Scheduler por usuario con consentimiento y horario explícito. No se crea tarea al restaurar `base`, no hay servicio residente y las rutas del checkout se seleccionan localmente.
- **WinGet:** puede ayudar a instalar paquetes, pero export/import **no** constituye un backup completo de datos, secretos o herramientas.

## Fuentes técnicas oficiales empleadas en la investigación inicial

- [Windows Shell links](https://learn.microsoft.com/en-us/windows/win32/shell/links) y [KNOWNFOLDERID](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid).
- [Command Palette: extensibilidad](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview) y [crear una extensión](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension).
- [Explorer: comando contextual empaquetado](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer) y [manifiesto FileExplorerContextMenus](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop4-fileexplorercontextmenus).
- [WinGet export](https://learn.microsoft.com/en-us/windows/package-manager/winget/export) / [import](https://learn.microsoft.com/en-us/windows/package-manager/winget/import).
- [Task Scheduler: registro](https://learn.microsoft.com/en-us/powershell/module/scheduledtasks/register-scheduledtask) y [principal](https://learn.microsoft.com/en-us/powershell/module/scheduledtasks/new-scheduledtaskprincipal).

Estas referencias documentan mecanismos; **no** certifican compatibilidad concreta. Revalidar SDK/paquetes/versión Microsoft al implementar cada integración.
