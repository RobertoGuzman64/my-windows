# Decisiones técnicas vigentes

Este registro contiene **elecciones actuales y límites**, no el historial de cada fase. Las decisiones anteriores se reconstruyen desde Git cuando haga falta, sin conservar una segunda cronología normativa en `docs/`. Cambios que alteren contratos o compatibilidad requieren una nueva decisión justificada.

| ID | Decisión | Estado actual | Motivo / condición de revisión |
| --- | --- | --- | --- |
| D01 | .NET 10, SDK fijado en `global.json` con `latestFeature` | Adoptada, compatibilidad multi-PC pendiente | Código único y CI reproducible; cotejar con inventarios reales |
| D02 | Domain, Application, Infrastructure, Cli y MSTest | Adoptada | Responsabilidades y fronteras verificables, sin GUI prematura |
| D03 | JSON estricto v1, `base` obligatorio, roles/equipo explícitos, sin override | Adoptada | Un único estado deseado, sin rutas locales ni secretos |
| D04 | `repo-shortcut` opt-in en el rol `repository`, nunca en `base` | Implementada, aceptación G4 pendiente | Personalización reversible sin afectar perfiles base |
| D05 | Ownership del `.lnk` mediante recibo, hash y COM ShellLink; bloqueo ante dudas | Implementada, G4 pendiente | No adoptar ni borrar recursos ajenos; el recibo no es firma digital |
| D06 | `plan` no escribe; `apply` idempotente; `repair`/`remove` explícitos | Adoptada, integración real pendiente | Evitar cambios silenciosos y reversión indiscriminada |
| D07 | Acción Git reutilizable con Git sin shell y límites de ejecución/salida | Implementada en CLI | Futuro Command Palette debe consumir Application, no duplicar Git |
| D08 | SHA-256 streaming de archivos locales, cancelación y comprobación de metadatos | Implementada en CLI | Futuro Explorer delega fuera del proceso shell |
| D09 | Integraciones modernas por mecanismos Microsoft soportados; sin MSIX/COM inventados | Pendiente de prototipo G5/G6 | Investigar versiones y publicar solo tras ensayos completos |
| D10 | Task Scheduler Git candidata, opt-in, con recibo y sin servicio residente | Preparada, G7 bloqueado | Sin instalación implícita ni necesidad de uso demostrada |
| D11 | `mw.cmd all only` y CI comparten gates; auditoría estructural bajo demanda y en validación | Implantada, ejecución nueva pendiente | Calidad verificable con diagnósticos y sin caches de resultados |
| D12 | Nullable, warnings-as-errors, `IDE0005`; `CS1591` excluido y `CA1707`/`CA1861` exceptuados **solo en tests** | Adoptada | Mantener rigor sin penalizar nombres/fixtures de pruebas |
| D13 | Documentación normativa mínima por propietario; logs fuera del repositorio | Adoptada | Evitar documentación histórica convertida en fuente de verdad |

## Pendientes que bloquean decisiones de producto

- Inventarios de sobremesa, portátil y VM; mínimo real de Windows, PowerToys y permisos.
- G4: recuperación desde VM limpia con CI/evidencias vinculadas al mismo commit, fallos y colisiones reales.
- G5/G6: empaquetado real y compatibilidad de Command Palette/Explorer; no hay adaptadores desplegables.
- G7: utilidad recurrente contrastada, horario/repositorio y aceptación Task Scheduler real.
- Plan de backup de secretos/datos y política de distribución/actualización futuros; Git no es un backup integral.

## Cambio de decisión

Registrar qué garantía deja de servir, alternativa elegida, riesgo, compatibilidad/migración y tests que la protegen. Si una decisión solo atañe a un módulo, actualizar su contrato en [arquitectura](ARQUITECTURA.md) sin abrir un documento nuevo por cada pequeño cambio.
