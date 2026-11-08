# MyWindows — Documento fundacional

> **Estado:** definición fundacional revisada; todavía no existe una implementación aprobada ni un repositorio operativo de MyWindows.
> **Revisión:** 2026-10-19 — disciplina de ingeniería incorporada tras revisar el código y la documentación de PC Health Monitor (PHM).
> **Propósito:** fijar visión, límites, estándares de calidad y criterio de aceptación antes del primer commit. Este documento permanece fuera del repositorio hasta crear la fuente de verdad definitiva; cuando se incorpore, tendrá un único propietario y no se duplicará.
> **Nombre:** `MyWindows`, provisional.
> **Relación con PHM:** proyecto independiente. PHM es una referencia de disciplina y experiencia, no una dependencia ni una plantilla que deba copiarse íntegramente.

## 1. Identidad y ambición

**MyWindows no es una aplicación monolítica ni un tema visual. Es un entorno de trabajo personal, reproducible, ampliable y profesional para Windows.**

El producto es la experiencia de Windows adaptada a su propietario. El repositorio privado contendrá código, definiciones de estado deseado y mecanismos para **planificar, instalar, actualizar, comprobar, reparar, desactivar, retirar y reconstruir** personalizaciones de forma controlada.

Se pretende reinstalar Windows, estrenar ordenador o alternar entre sobremesa y portátil y recuperar el entorno sin depender de una lista de ajustes manuales olvidados.

**Nivel de exigencia:** que sea un proyecto personal no rebaja el estándar de ingeniería. MyWindows debe mostrar la misma disciplina que se persigue en PHM, por dentro y, cuando exista interfaz, también por fuera. El alcance inicial será pequeño; la calidad exigida a ese alcance no será provisional.

La apariencia de Windows no es la prioridad inicial. Sí lo son la utilidad, la integración, la coherencia de la experiencia, la mantenibilidad y la permanencia de las mejoras. Una eventual interfaz de gestión será una presentación del sistema, no su fundamento.

## 2. Principios no negociables

1. **Calidad profesional desde el primer cambio.** Ownership claro, contratos verificables, código legible, pruebas pertinentes, revisiones y documentación vigente. Nada de «ya lo ordenaremos después» para código declarado estable.
2. **Recuperación antes que acumulación de funcionalidades.** La primera demostración valiosa es restaurar una personalización en un Windows limpio o VM y verificarla.
3. **Git como fuente de verdad del estado deseado.** El estado accidental de un equipo no define el producto; Git tampoco sustituye las copias de seguridad de datos o secretos.
4. **Multi-PC desde el principio.** Base compartida, capacidades combinables y perfiles por equipo sin duplicar configuraciones completas.
5. **Idempotencia demostrada.** Aplicar dos veces el mismo estado no genera duplicados ni alteraciones inesperadas; no basta con afirmarlo.
6. **Reversibilidad con alcance conocido.** Desactivar y retirar módulos no debe destruir datos ajenos ni perjudicar integraciones independientes. Las operaciones no reversibles se explicitan antes de ejecutar.
7. **Integraciones soportadas.** Usar APIs y puntos de extensión documentados; no parchear Explorer ni componentes reservados de Windows como fundamento.
8. **Seguridad por diseño.** Privilegios mínimos, entradas validadas, secretos fuera del repositorio, rutas seguras, confirmación y copias de respaldo cuando corresponda.
9. **Ligereza y aislamiento.** No introducir servicios residentes, procesos persistentes ni acoplamiento al shell sin una necesidad justificada.
10. **Trazabilidad honesta.** Una comprobación no ejecutada es `NOT RUN` o `BLOCKED`, nunca `PASS`; las pruebas reales de Windows no se sustituyen por validación estática.
11. **Complejidad justificada.** Se hereda la disciplina de PHM, no su cantidad de scripts, validadores, servicios o ceremonias. Cada elemento permanente debe responder a una responsabilidad actual.
12. **Evolución por necesidad.** Cada módulo resuelve un problema cotidiano identificable y se considera terminado solo cuando cumple su contrato de calidad.

## 3. Primeras familias de funcionalidades

### A. Explorador a medida

Acciones propias sobre archivos y carpetas mediante puntos de extensión admitidos por Windows. Posibles ejemplos: hashes, rutas y metadatos, comparaciones de carpetas, apertura de repositorios y operaciones repetitivas verificables.

**Regla arquitectónica:** una extensión que cargue dentro de Explorer debe ser mínima, rápida y resistente a fallos. El trabajo costoso, las reglas y las operaciones con efectos se delegarán a procesos o capas independientes. Ninguna funcionalidad justifica comprometer la estabilidad del Explorador.

### B. Centro de comandos

Acceso por teclado a acciones, búsquedas y herramientas personales. Evaluar **PowerToys Command Palette y sus capacidades de extensión reales** antes de diseñar un lanzador propio.

Ejemplos: abrir proyectos, consultar Git, invocar herramientas independientes de PHM, ejecutar diagnósticos de red o lanzar operaciones controladas.

### C. Automatizaciones

Acciones manuales, por atajo, programadas o activadas por eventos soportados de Windows: preparar entornos de trabajo, clasificar archivos o responder a conectividad.

Cada automatización exige contrato de activación, permisos, parámetros, política de fallos, observabilidad, verificación y retirada. Las acciones destructivas necesitan protecciones y no presuponen rutas o usuarios concretos.

**Diseño común:** cuando corresponda, la misma acción del núcleo podrá exponerse desde Explorer, Command Palette, CLI o automatización, sin replicar las reglas en cada adaptador. No construir un framework de acciones más amplio que los consumidores reales.

## 4. Modelo de configuración multi-equipo

**Capas previstas:**

- **Base personal:** ajustes y herramientas comunes a todos los Windows compatibles.
- **Capacidades o roles:** p. ej. `development` y `network-tools`, combinables, sin copias integrales.
- **Perfil del equipo:** diferencias concretas de sobremesa, portátil y futuros dispositivos.
- **Secretos y estado local:** fuera del repositorio en texto claro, con almacenamiento y aprovisionamiento adecuados por equipo.

El resolvedor de perfiles debe tener reglas de precedencia deterministas, validación de conflictos, dependencias explícitas y resultados comprobables mediante tests. No asumir nombres de usuario, letras de unidad, rutas absolutas ni hardware como invariantes globales. Usar rutas conocidas, configuración y comprobaciones de capacidades.

**No todo se replica:** identidades, credenciales, claves privadas, opciones sensibles, monitores y particularidades de hardware podrán requerir restauración parcial y pasos manuales declarados. No falsear el estado «restaurado» cuando falten.

## 5. Instalación, actualización, reparación y recuperación

El primer producto útil es un flujo reproducible de reconstrucción, no una interfaz de administración.

1. **Inspect / preflight:** detectar sistema, permisos, dependencias, estado actual y perfil aplicable.
2. **Plan:** exponer cambios esperados, elementos intactos, riesgos, requisitos y operaciones que precisan confirmación; sin modificar el equipo.
3. **Apply:** instalar dependencias por medios soportados (WinGet cuando convenga), desplegar módulos y aplicar estado declarado sin duplicados.
4. **Verify:** comprobar postcondiciones y funcionamiento real, no únicamente que existan archivos o claves.
5. **Status / drift:** señalar diferencias entre configuración deseada y realidad, sin reparar silenciosamente.
6. **Repair:** corregir desviaciones previstas, con el mismo modelo de permisos, seguridad e idempotencia.
7. **Disable / remove:** desactivar o retirar solo recursos propios, respetar datos de usuario y dependencias; informar lo que no se puede revertir.
8. **Report:** explicar cambios realizados, `PASS`/`FAIL`/`SKIPPED`/`NOT RUN`, limitaciones, logs y pasos manuales restantes.

**Seguridad operacional:** identificar qué recursos posee MyWindows y cuáles preexistían; no sobrescribir ni borrar recursos ajenos por coincidencia de nombre. Evitar ejecuciones concurrentes del mismo cambio cuando puedan corromper estado. Manejar fallos parciales y reintentos con una política explícita; no prometer rollback universal de operaciones que no admiten reversión.

**Contrato mínimo por módulo:** identificador y versión, requisitos, configuración y dependencias, cambios previstos, aplicación idempotente, verificación de postcondiciones, actualización, retirada/inhabilitación, recuperación y límites conocidos. La forma concreta del manifiesto se decidirá al construir el primer módulo, con tests contractuales.

**Respaldo:** Git conserva definiciones y código, pero **no es un backup completo**. Mantener copias independientes de material no reconstruible, exportaciones necesarias y secretos cifrados, al menos una fuera del disco principal; ensayar la restauración, no solo generar copias.

## 6. Arquitectura y ownership

El árbol definitivo se decidirá con la primera implementación. Las **fronteras conceptuales**, en cambio, son obligatorias desde el comienzo:

- **Domain / Action Core:** contratos y reglas puras: identidad de acciones, validación de parámetros, resolución de perfiles, dependencias y semántica de planificación. Sin llamadas directas al shell, al registro ni a procesos externos.
- **Application / Use Cases:** orquestación de `plan`, `apply`, `verify`, `repair` y `remove`, tratamiento de resultados, límites transaccionales y coordinación; no contiene UI ni implementaciones concretas de Windows.
- **Infrastructure / Windows Adapters:** filesystem, registro, Task Scheduler, instalación, PowerToys y demás integraciones externas conforme a contratos. Aislar dependencias y efectos.
- **Hosts / Surfaces:** CLI y, cuando existan, Explorer, Command Palette y automatizaciones. Traducen entradas y presentan resultados; no deciden reglas de dominio.
- **Profiles / Configuration:** especificaciones declarativas y su validación, independientes de la identidad de una máquina concreta.
- **Presentation (futuro):** interfaz y estado visual sobre contratos existentes. La GUI no es requisito para usar el núcleo ni para recuperar Windows.

La composición de dependencias debe preservar la independencia del dominio. La infraestructura implementa contratos definidos en capas interiores; los adaptadores no se llaman entre sí para resolver reglas comunes. Nada de mezclar UI, acceso al sistema, negocio y scripts de despliegue en un único archivo.

**Regla PHM adoptada:** antes de cambiar código hay que identificar **owner + contrato + guard de regresión + comprobación mínima**. Si una responsabilidad ya tiene propietario, evolucionarlo sin crear otro helper, fachada, runner o documento por comodidad. Los archivos sobredimensionados, las dependencias circulares y las reglas duplicadas se corrigen mediante una separación por responsabilidades, no repartiendo líneas arbitrariamente.

Estructura orientativa, **no compromiso de carpetas vacías**:

```text
MyWindows/
  src/              # Capas y hosts que realmente existan
  profiles/         # Estado deseado y perfiles reales
  tests/            # Comportamiento, contratos y regresiones
  tooling/          # Entradas operativas necesarias
  docs/             # Documentación permanente con owner claro
```

## 7. Tecnologías candidatas y límites

- **C# y .NET:** candidatos para núcleo, casos de uso, adaptadores y ejecutables; habilitar nulabilidad, análisis y errores de compilación estrictos donde apliquen.
- **PowerShell:** instalación, bootstrap, diagnóstico y operaciones controladas del sistema; no convertirse en la capa de negocio ni proliferar en scripts de una sola ocasión.
- **WinGet:** adquisición de dependencias cuando esté disponible y sea apropiado; contemplar errores de paquete, versión y conectividad.
- **PowerToys Command Palette:** investigar SDK, empaquetado y compatibilidad antes de comprometer una integración.
- **Extensiones de shell soportadas por Windows 11:** investigar implementación, aislamiento, registro y desinstalación antes de elegir tecnología.
- **Programador de tareas y eventos de Windows:** automatizaciones soportadas y desactivables.

No dar por hecho acceso a puntos de integración privados de Windows, como Configuración o el panel rápido de Wi-Fi. Las versiones mínimas soportadas, empaquetado y capacidades concretas se fijarán con una prueba técnica, no por suposiciones. No se adopta una interfaz WPF, WinUI ni web hasta existir una necesidad y una decisión explícita.

## 8. Estándar de ingeniería heredado de PHM

La revisión estática de PHM muestra prácticas que queremos trasladar: fronteras `Domain` / `Infrastructure` / `App`, punto de entrada operativo, contratos de arquitectura y mantenibilidad, pruebas, validación por etapas, documentación propietaria, seguridad de publicación y evidencia ligada al estado evaluado. **Se copia el criterio, no su complejidad acumulada.**

### 8.1 Quality gates desde el primer módulo

MyWindows deberá disponer de una **única entrada operativa documentada** para las tareas habituales. Los nombres concretos y su implementación se decidirán en el bootstrap del repositorio; estas son las responsabilidades que no pueden faltar:

| Gate | Responsabilidad | Regla |
| --- | --- | --- |
| `format` | Formato reproducible de los lenguajes utilizados | Puede cambiar archivos; se ejecuta antes de certificar el estado. |
| `check` | Análisis estático, compilación de desarrollo, arquitectura, mantenibilidad y limpieza de cambios | No modifica código; informa todos los fallos relevantes. |
| `tests` | Pruebas deterministas y regresiones de los contratos existentes | Ejecuta pruebas reales, no inspecciona únicamente nombres de tests. |
| `build` | Compilación y empaquetado reproducibles de artefactos pertinentes | No oculta tests ni corrige código automáticamente. |
| `validate` | Cierre: coherencia de artefactos, configuración y resultados de gates | No declara válido un estado distinto al comprobado. |

En la primera iteración puede haber gates muy ligeros, pero no ficticios. Si todavía no existe packaging, se declara expresamente que no aplica; no se simula una compilación ni un `PASS`. No es obligatorio replicar el sistema de receipts/cache, release state machine ni el runner propio de PHM: se evaluarán si aportan valor real.

**CI mínimo:** en Windows debe ejecutar al menos formato en modo comprobación, análisis/compilación y tests deterministas afectados. Un control que solo se pueda hacer en VM o hardware real aparecerá como validación externa requerida, **no** como éxito inferido por CI. Las condiciones exactas de publicación se añadirán antes de la primera distribución.

### 8.2 Estrategia de pruebas

- **Unitarias:** resolución de perfiles, acciones puras, reglas de precedencia, validación de inputs y planes deterministas.
- **Contrato / integración aislada:** adaptadores con dependencias controladas, permisos, errores, resultados y separación de capas.
- **Recuperación en Windows/VM:** `plan` sin efectos, `apply`, segunda aplicación sin cambios, `verify`, desviación detectable, `repair` y `remove` sin residuos indebidos; fallos parciales y permisos insuficientes.
- **Compatibilidad y regresión:** versiones soportadas de Windows, procesos Explorer, registros y Programador de tareas cuando un módulo toque esas superficies.
- **Seguridad:** rutas inesperadas, entradas inválidas, recursos preexistentes, confirmaciones, secretos y operaciones destructivas.
- **Interfaz (cuando exista):** contratos de presentación, accesibilidad, navegación por teclado, escalado/DPI, estados vacíos/errores y consistencia visual; inspección real además de automatización.

Cada test protege un comportamiento o contrato observable. Priorizar tests frente a validadores que busquen cadenas o nombres internos en archivos fuente. Conservar fixtures y baselines solo si tienen consumidor permanente. Ninguna cifra artificial de tests equivale a calidad.

### 8.3 Mantenibilidad y disciplina de cambios

- Tipado estricto, nulabilidad controlada, advertencias pertinentes tratadas como fallos y formato estable; sin supresiones generales para «poner verde» el gate.
- Clases y módulos con responsabilidades estrechas; límites de tamaño y de concentración de lógica controlados desde el primer desarrollo. Los presupuestos numéricos se fijarán en el repositorio con evidencia real, tomando PHM como referencia, sin duplicar umbrales a ciegas.
- Contratos arquitectónicos automáticos para prohibir dependencias ilegales y preservar APIs públicas críticas. Si hay excepciones, deben ser explícitas, revisables y justificadas.
- Los tests de comportamiento priman sobre checks de texto; evitar guards frágiles que impidan refactors legítimos.
- Una sola fuente de verdad por norma: contrato ejecutable, configuración o documento propietario. No mantener instrucciones contradictorias ni históricos de fases como arquitectura permanente.
- El código de producto, tooling, pruebas y documentación no comparten responsabilidades. Evitar wrappers, scripts, ficheros vacíos, código muerto, datos falsos y artefactos temporales en el árbol estable.
- Las herramientas operativas deben dar errores accionables, salida legible y logs con datos sensibles protegidos, no miles de líneas de ruido.

### 8.4 Cambios, validación y evidencia

Antes de implementar se define: problema, owner, contrato afectado, superficie Windows tocada, riesgo, test/regresión y comprobación final. Después se modifica **lo mínimo coherente** en la capa propietaria, sin refactorizaciones ajenas al alcance.

El cierre debe distinguir sin ambigüedad:

- **`PASS`:** comando/prueba ejecutado con resultado satisfactorio para ese contenido.
- **`FAIL`:** comprobación ejecutada y fallida.
- **`NOT RUN`:** no ejecutada; no permite atribuir éxito.
- **`BLOCKED`:** no fue posible ejecutarla por entorno, permisos o dependencias.
- **`NOT APPLICABLE`:** fuera del contrato de ese módulo o fase, con justificación.

Para pruebas externas, adjuntar cuando sea apropiado versión de Windows, entorno (VM/equipo), escenario, resultado y evidencia reproducible. No inventar resultados, no confundir compilación con funcionamiento real, y no afirmar compatibilidad de una versión que no se probó.

## 9. Definición de terminado: módulo y repositorio

Un **módulo estable** exige, como mínimo:

1. Problema útil delimitado, owner, contratos de entrada/salida y dependencias documentados.
2. Implementación en su capa correcta, sin romper contratos públicos accidentalmente ni introducir acoplamiento oculto.
3. Requisitos y permisos comprobados; operación reversible o limitaciones irreversibles comunicadas.
4. Instalación, actualización cuando aplique, idempotencia, verificación, reparación y retirada cubiertas por pruebas adecuadas.
5. Errores y estados parciales definidos; datos propios identificables y secretos protegidos.
6. Gates aplicables realmente ejecutados con resultados trazables, además de aceptación en Windows/VM si toca el sistema.
7. Documentación propietaria y ejemplos de uso actualizados; sin scripts o fixtures temporales sobrantes.
8. Verificación de que puede reconstruirse desde una instalación limpia compatible sin recordar pasos ocultos.

Un **repositorio preparado para evolucionar** exige desde el primer commit: convenciones de código y archivos, build reproducible, estructura de responsabilidades real, tests deterministas, CI ejecutable, instrucciones de desarrollo y política de cambios/seguridad. El primer commit no necesita una funcionalidad enorme; sí debe tener un camino de desarrollo que funcione de verdad.

Para una distribución posterior se añadirán versión de artefacto, integridad, estrategia de actualización y reparación, verificación de desinstalación y criterio de reversión; **no se declarará lista antes de probar ese ciclo**.

## 10. Calidad exterior y eventual interfaz

Aunque no se construya una interfaz ahora, su calidad futura es una exigencia del producto: jerarquía visual, consistencia de componentes, estados de error/carga, accesibilidad, navegación por teclado, densidad y escalado de Windows, y rendimiento. La UX de los comandos y del instalador **ya es experiencia de usuario**: planes claros, confirmaciones proporcionadas, diagnósticos comprensibles y cambios previsibles.

Si se añade GUI, deberá consumir casos de uso y contratos públicos existentes. No duplicará reglas de permisos o perfiles en ViewModels. El sistema visual tendrá un propietario único (tokens/estilos compartidos y pruebas relevantes), sin sacrificar estabilidad o recuperabilidad por estética.

## 11. Fases de trabajo

### Fase 0 — Decisiones técnicas y base de calidad

Inventariar versiones y capacidades reales de sobremesa/portátil, estudiar extensiones soportadas y recuperación, elegir el primer caso inocuo. Establecer contratos de capas, convenciones, tooling único, CI y matriz de pruebas antes de desarrollar funciones. No modificar PHM.

**Salida:** decisiones técnicas justificadas, riesgos conocidos y definición de calidad operativa; ninguna compatibilidad afirmada sin evidencias.

### Fase 1 — Recuperación demostrada

Crear repositorio privado y perfiles `base`/equipos reales, núcleo acotado con `plan`, `apply`, `verify`, `status`, `repair` y `remove` (con `repair` implementado como reaplicación idempotente cuando sea suficiente). Implementar una personalización trivial y reversible; probar desde Windows limpio en VM, ejecutar dos veces y retirar. Conservar evidencia de lo recuperable y lo manual.

**Salida:** primer módulo estable conforme a la definición de terminado, no solo scripts que funcionan en el ordenador del autor.

### Fase 2 — Centro de comandos

Implementar una acción cotidiana de desarrollo, exponerla en Command Palette si la integración se confirma técnicamente, y demostrar despliegue y recuperación multi-PC.

**Salida:** herramienta personal útil, aislada, instalable, verificable y mantenible.

### Fase 3 — Explorador

Exponer una acción segura sobre archivos/carpetas; aislar carga costosa fuera de Explorer, comprobar rendimiento y estabilidad y ejecutar retirada completa.

**Salida:** integración contextual soportada y reversible con regresiones específicas.

### Fase 4 — Automatizaciones

Introducir una automatización real con activador declarativo, política de fallos, posibilidad de desactivarla y verificación de estado.

**Salida:** automatización reconstruible en los equipos a los que corresponda.

### Fases posteriores — Evolución por necesidad

Herramientas de red, integración opcional con PHM, más acciones y perfiles, diagnóstico, interfaz de gestión si llega a justificarla la experiencia real. El monitor de redes es una posibilidad, **no un requisito**.

Las fases son un plan de arranque, no nombres para carpetas ni infraestructuras permanentes. La arquitectura se organiza por responsabilidad actual.

## 12. Reglas contra el «infierno infinito»

- No hay obligación de completar un producto imaginario; sí de **terminar bien cada módulo**.
- Una función útil, probada y recuperable vale más que diez funciones previstas.
- Una mejora manual que no puede reconstruirse no es una personalización estable.
- No inventar abstracciones, servicios, validators, runners ni CLI paralelas antes de necesitar sus contratos.
- No introducir procesos residentes para tareas que puedan ejecutarse bajo demanda.
- Ningún módulo se apropia de reglas de otro: contratos compartidos, dependencias unidireccionales.
- No acumular planes, campañas, fases o documentos históricos como si fueran arquitectura vigente.
- No ocultar fallos, degradaciones, pasos manuales ni pruebas no ejecutadas.
- No modificar Windows por estética como sustituto de un valor funcional demostrable.
- La sofisticación se mide en **robustez, seguridad, claridad y capacidad de evolución**, no en número de proyectos o líneas de código.

## 13. Decisiones acordadas y cuestiones abiertas

**Acordado:** Windows es la plataforma objetivo; núcleo sin interfaz obligatoria; apariencia secundaria al inicio pero calidad exterior exigible; disciplina equivalente a PHM; arquitectura con responsabilidades y contratos protegidos; CI y tests desde el primer módulo; Git como fuente del estado deseado; recuperación e idempotencia comprobables; perfiles multi-PC; Explorer y centro de comandos como primeras áreas; automatizaciones futuras; integraciones soportadas; PHM permanece independiente.

**Pendiente de prueba/decisión:** nombre definitivo, versión mínima de Windows, SDK de .NET y empaquetado, integración exacta de PowerToys, soporte de shell, configuración de profiles reales, formato de manifiestos, almacenamiento y backup de datos, privilegios necesarios, política de versiones/distribución, comandos concretos del CLI y umbrales de mantenibilidad. No cerrar estos puntos con datos inventados.

## 14. Próximo paso práctico

**Primero crear fundamentos profesionales; después sumar funcionalidades.** El primer trabajo es transformar estas reglas en un repositorio con owners reales, checks que funcionan, CI y una prueba de recuperación inocua. La aceptación debe incluir:

1. Compilación/format/análisis y tests deterministas ejecutados, con resultados identificables.
2. `plan` sin efectos en Windows, más instalación y verificación observables.
3. Segunda aplicación sin cambios indebidos.
4. Retirada sin eliminar recursos ajenos y reconstrucción limpia en una VM compatible.
5. Registro explícito de cualquier comprobación pendiente, bloqueo o intervención manual.

Solo después ampliar Explorer, Command Palette y automatizaciones conforme a necesidades reales. Nada obliga a construir una gran GUI; sí obliga a que cualquier interfaz que llegue tenga el mismo acabado que su núcleo.

---

**Principio rector:** *«No personalizamos una instalación de Windows. Construimos un entorno personal que podemos reconstruir en cada Windows compatible, con disciplina de ingeniería profesional desde el primer módulo».*
