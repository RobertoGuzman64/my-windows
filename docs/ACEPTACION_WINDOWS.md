# Aceptación Windows y recuperación — G0 a G7

La presencia de código, pruebas o scripts de ensayo **no equivale a aceptación**. Cada gate exige evidencias del entorno exacto y del código exacto. Es obligatorio registrar `PASS`, `FAIL`, `NOT RUN`, `BLOCKED` o `NOT APPLICABLE` con motivo. Los ensayos que modifican Windows solo se realizan en una **VM desechable con snapshot**, no en el equipo principal.

## Estado actual de la aceptación

| Gate | Objetivo | Estado conocido al integrar documentación | Falta |
| --- | --- | --- | --- |
| G0 | Inventarios, límites de soporte, VM y primer recurso | `BLOCKED` | Inventarios y matriz real |
| G1 | Código, arquitectura, formato, tests y CI | `PARTIAL` | Se recibió 74/74 tests locales de la revisión anterior; falta CI del mismo commit y comprobar cambio actual |
| G2 | Perfiles y plan determinista sin escrituras | `PARTIAL` | Tests locales verdes; falta ensayo real de observación/colisiones y evidencia G0 |
| G3 | Ciclo del primer acceso, ownership, idempotencia | `PARTIAL` | Tests locales verdes; falta integración y regresiones de permisos en VM |
| G4 | Reconstrucción y retirada en VM limpia | `NOT RUN / BLOCKED` | Ejecutar recorrido, pruebas negativas y aceptación humana |
| G5 | Acción Git en Command Palette empaquetada | `BLOCKED` | No existe host/paquete; requiere G4 |
| G6 | Integración de hash en Explorer moderno | `BLOCKED` | No existe host/paquete; requiere G4 |
| G7 | Automatización Git útil, registrable y reversible | `BLOCKED` | No se ha acreditado uso repetido ni ensayado Task Scheduler |

`PARTIAL` no es un PASS oficial. Los resultados de la revisión anterior no certifican este repositorio modificado, la VM ni CI.

## Evidencia mínima de cada aceptación

Identificar commit limpio (`git rev-parse HEAD`), versión de SDK (`dotnet --version`), build/arquitectura Windows, usuario/privilegios, hashes del binario y scripts relevantes, nombres de perfiles, resultado de `all only` y logs. Los informes pueden contener rutas personales: almacenar fuera de Git y compartir solo tras revisión. Correlacionar con el job CI **del mismo commit** y con las operaciones observables. No reutilizar logs de otra compilación.

## G0–G3: prerrequisitos y primera acción

1. **G0:** ejecutar [inventarios](COMPATIBILIDAD.md) en sobremesa/portátil; completar versión, permisos, restricciones y VM de prueba. Confirmar el recurso Inicio de usuario y las garantías de no sobrescritura.
2. **G1:** en checkout limpio de Windows, ejecutar `./mw.cmd all only`, revisar el diff y la ejecución CI. Comprobar en branch desechable que una referencia de Domain a otra capa provoca `ARCH001`, después revertirla. No aceptar únicamente una captura de salida.
3. **G2:** ejecutar `dotnet run --project src/MyWindows.Cli -- plan profiles . --role repository`, registrar JSON y comprobar que dos planes sobre el mismo estado coinciden. Probar ruta ausente y recurso extranjero **solo** en VM; el plan no debe escribir.
4. **G3:** probar `plan → apply → verify → apply → status`, comprobar que la segunda aplicación no cambia bytes del `.lnk` ni del recibo. Simular ausencia controlada y probar `status → repair → verify → remove`; bloquear acceso extranjero, modificado o recibo corrupto. Exigir postcondición real en menú Inicio, ausencia de residuos y usuario sin admin.

## G4: ensayo automatizado y revisión humana

Condiciones: VM desechable con snapshot, checkout Git limpio, recursos del módulo ausentes al empezar, usuario sin elevar, SDK .NET 10, Git y carpeta **nueva** de evidencias privada fuera del repositorio. La cadena `ACEPTO_VM_DESECHABLE` **no demuestra técnicamente** que la máquina sea una VM; la responsabilidad de comprobarlo es humana.

```powershell
./eng/acceptance/Invoke-G4.ps1 `
    -EvidenceDirectory "$env:USERPROFILE\mw-g4-evidence-01" `
    -Confirmation ACEPTO_VM_DESECHABLE
./eng/acceptance/Test-G4Evidence.ps1 `
    -EvidenceDirectory "$env:USERPROFILE\mw-g4-evidence-01"
```

`Invoke-G4.ps1` ya está implementado y sigue usando la invocación compatible `eng/mw.ps1 -Task all`. Contempla plan sin efectos, `apply/verify`, segunda instalación idempotente, deriva, reparación, interrupción simulada, retirada, colisión ajena y ruta inexistente; verifica huellas de artefactos propios. **Test-G4Evidence verifica la coherencia de la evidencia, no certifica G4.** Su resultado `PARTIAL` exige revisión.

Antes de declarar `G4 PASS`, ejecutar además: escenario real de permiso denegado sin daños, SDK/dependencia ausente, reconstrucción desde checkout fresco sin ajustes manuales, prueba visible de acceso Inicio, revisión de residuos, CI del mismo commit y segundo equipo/perfil cuando aplique. Una prueba no aplicable requiere justificación. Nunca limpiar manualmente recursos de propiedad dudosa para hacer pasar un ensayo.

## G5: PowerToys Command Palette

Depende de G4 y versiones reales. Generar el proyecto de extensión desde la plantilla oficial del PowerToys instalado; conservar paquete/manifest reales, GUID, versiones, hash del MSIX y evidencias de instalación, segunda instalación, actualización, descubrimiento en UI, fallo/timeout, retirada y recuperación. La acción `repo inspect` debe devolver idéntica semántica desde CLI y Command Palette, sin parseo Git duplicado. La mera existencia de la CLI no aprueba G5.

## G6: Explorer moderno

Depende de G4 y compatibilidad Windows validada. Crear `IExplorerCommand` y paquete siguiendo Microsoft; medir que el menú no se bloquee, que la operación de hash suceda fuera del shell y que las huellas coincidan con CLI. Probar archivos vacíos/grandes, permisos, puntos de reanálisis, cancelación, selección hostil, errores/crash, instalación/retirada limpia y recuperación desde checkout fresco. **No existe actualmente el paquete Explorer.**

## G7: automatización bajo consentimiento

Requiere una necesidad recurrente demostrada, un repositorio y horario escogidos, G4 y pruebas en VM bajo usuario interactivo sin elevar. El script se niega a iniciar si detecta tarea o recibo preexistente y su recorrido **solo da aceptación parcial**:

```powershell
./mw.cmd all only
./eng/automation/Test-G7.ps1 `
    -RepositoryPath . `
    -DailyAt '<HH:mm-validado-por-el-usuario>' `
    -Confirmation ACEPTO_VM_DESECHABLE
```

Sustituir el marcador por una hora real de prueba. Después comprobar tarea y recibo, primera y segunda instalación, informe JSON, `disable/enable`, cambio de horario, bloqueo por deriva, colisiones con tareas **creadas para el ensayo**, concurrencia, fallos de Git/.NET, permisos, retirada de tarea propia e informes conservados. Volver a clonar y reaprovisionar ruta/horario explícitos. No activar nada en el PC habitual hasta revisar evidencias.

## Criterio de cierre

Un gate de Windows se marca `PASS` solo cuando sus escenarios relevantes están probados y revisados, con evidencias consistentes, sin estados fallidos pendientes. Un gate dependiente no se promueve por adelantado. Los binarios, accesos y tareas siguen siendo candidatos hasta su aceptación específica.
