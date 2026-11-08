# Desarrollo y gobernanza

## Regla principal

Un cambio no termina porque «funcione aquí». Debe conservar los contratos, aportar pruebas proporcionales al riesgo, superar `./mw.cmd all only` y, si afecta al sistema operativo, superar **además** su ensayo de aceptación en Windows. Resultado no ejecutado es `NOT RUN`, no `PASS`.

## Prácticas de trabajo

1. Reproducir el fallo concreto y localizar el owner real antes de modificar. No introducir refactors amplios durante una corrección puntual sin necesidad demostrable.
2. Respetar la topología Domain → Application → Infrastructure → Hosts. Los límites `ARCH001`–`ARCH004`, los tests arquitectónicos y el análisis estático actúan como defensas complementarias.
3. Mantener los contratos públicos; cualquier modificación incompatible necesita plan de migración, tests y decisión. Prohibidos mocks de compatibilidad Windows que se reporten como pruebas reales.
4. Tests de comportamiento, errores y regresiones antes de declarar terminado. Uso preferente de MSTest. Las pruebas de ficheros/COM utilizan recursos controlados en directorios temporales; las de integración sensible requieren VM desechable.
5. No incluir rutas de usuario, secretos, nombres de equipos ni credenciales en perfiles/fixtures/documentación. No elevar privilegios ni alterar configuración global para facilitar una prueba.
6. Evitar nuevos procesos residentes, frameworks de automatización, caches o sistemas de artefactos mientras no exista una necesidad y una métrica de valor.
7. Cambiar documentación de su propietario: arquitectura/contratos en `ARQUITECTURA.md`, comandos en `TOOLING.md`, compatibilidad en `COMPATIBILIDAD.md`, pruebas Windows en `ACEPTACION_WINDOWS.md` y decisiones en `DECISIONES.md`. No crear documentos por sprint o fase ni registrar salidas efímeras como reglas permanentes.

## Configuración de calidad

`Directory.Build.props` habilita Nullable, analyzers, advertencias como errores, build determinista y generación XML necesaria para `IDE0005`. El aviso `CS1591` está excluido hasta establecer política de XML para APIs públicas; no se suprimen otros fallos con excepciones globales. `.editorconfig` limita las excepciones de `CA1707` y `CA1861` al código de tests para conservar nombres descriptivos y fixtures legibles; ambas reglas siguen vigentes en producción.

`Directory.Build.targets` protege las fronteras de referencias. `eng/maintenance/Test-Repository.ps1` comprueba sintaxis PowerShell, XML, JSON, enlaces locales y ausencia de artefactos de build versionados. `eng/mw.ps1` es el único orquestador de calidad; CI lo invoca sin redefinir los controles.

## Definición de terminado

- [ ] El problema está formulado y el ámbito del cambio es mínimo y coherente.
- [ ] El diseño conserva los contratos y separa reglas de efectos.
- [ ] Hay tests útiles para los casos correctos, negativos y de repetición/idempotencia donde proceda.
- [ ] `./mw.cmd all only` finaliza correctamente sobre **este estado del repositorio**.
- [ ] El diff no incluye binarios generados, rutas personales, secretos, deuda temporal ni documentación redundante.
- [ ] Si toca Explorer, COM, Inicio, Task Scheduler, permisos o estado persistente: se ejecuta la aceptación correspondiente en VM y se registra el resultado real, o se declara el gate pendiente y **no se etiqueta como estable**.
- [ ] El resultado se puede diagnosticar, revertir y reconstruir dentro de los límites del módulo.

## Evidencia y estados

Conservar SHA del commit o árbol exacto, SDK, sistema operativo, comando, timestamp y log. `PASS` acredita ejecución y postcondiciones; `FAIL` identifica una comprobación que no pasó; `BLOCKED` identifica prerequisitos pendientes; `NOT RUN` significa no ejecutado; `NOT APPLICABLE` exige justificación. Un `PASS` de MSTest **no** demuestra instalación, indexing de Inicio, compatibilidad MSIX ni supervivencia a reinstalación.

El log real aportado el 19-10-2026 del repositorio **anterior a esta consolidación** mostró formato correcto, compilaciones Debug/Release, 74/74 MSTest y validación/resolución de perfiles. Ese resultado acredita dicha revisión, **no** el runner/documentación modificados aquí ni CI ni VM.
