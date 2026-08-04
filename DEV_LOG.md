# DEV LOG — LA LUZ MALA map generator

Running log of AI-assisted changes to the greybox map generator, kept in this
folder so any AI session (Claude, etc.) working on this project can catch up
on recent context without re-deriving it from scratch. Newest entries on top.
See `MAP_README.md` for the static architecture reference.

**2026-08-03: log reseteado a propósito.** El historial viejo (cientos de
entradas) quedó lleno de intentos fallidos, diagnósticos equivocados y
reversiones sobre la ruta pavimentada / spawn del auto — más ruido que
señal. Se resume acá SOLO el estado actual y lo que hay que saber para no
repetir los mismos errores. Si necesitás el detalle histórico, está en
`git log` (código) y en el historial de Unity Version Control / Plastic
(escena y assets — dos sistemas de control de versiones DISTINTOS en este
proyecto, ver más abajo).

---

## 2026-08-04 — Fix: el auto quedaba trabado entrando a la YPF (2 sistemas de corrección peleándose)

`CarAutoDrive`'s "volver al asfalto si se desvía" (`rescuing`, ver más
abajo) no reconocía el playón de tierra/pavimento junto al surtidor como
"asfalto real" — adentro del lote tironeaba al auto de vuelta hacia la
ruta principal MIENTRAS el sistema de estacionamiento del compañero
(`SnapToRoadExtensionTip`) lo llevaba al surtidor real. Ninguno ganaba
nunca → auto trabado, motor andando, sin llegar. Apagado `rescuing`
dentro de `inLotZone` (el destino ahí es el playón, no la ruta). También
bajado mucho el volumen de `WindAmbience` (0.35 → 0.015, quedó en el
default viejo por algún reset de Plastic).

**v2 (mismo día):** seguía trabándose, un poco ANTES de los últimos 3
waypoints -- `inLotZone` (por ÍNDICE de waypoint) no coincidía con dónde
el auto realmente cruza de asfalto a playón. Cambiado a distancia REAL
restante (`remaining < slowdownDistance`, ~45m) en vez de conteo de
waypoints -- cubre toda la zona final con margen.

**Importante:** ninguno de estos 2 fixes necesita Generate (es lógica
de C# pura en un script runtime) -- alcanza con que Unity recompile y
darle Play. Correr Generate reconstruye el terreno con la caché/semilla
LOCAL y puede romper la escena sincronizada del compañero (ver sección
de abajo) -- no correrlo solo para probar un fix de código.

---

## Cómo está armada la ruta pavimentada y el spawn del auto (estado actual)

- **`PavedRoad_Surface` y `PavedRoad_Surface (1)`, `(2)`... NO son
  duplicados basura.** Son extensiones de la ruta que el owner agregó A
  MANO en el Editor (arrastrando/duplicando el mesh) para alargar el
  camino más allá de lo que genera `MapLayout.PavedControls`. **No
  borrarlos** ni tratarlos como limpieza pendiente.
- El auto (`Renault12`) y su recorrido completo (spawn, rotación,
  waypoints hasta girar y frenar al lado de un surtidor real en la YPF)
  los arma **`CarBuilder.SnapToRoadExtensionTip()`**, llamado desde
  `MapGenerator.Generate()` justo después de
  `MapLayoutPersistence.ApplySavedLayout()`. Lee la geometría VIVA de la
  escena (todas las piezas `PavedRoad_Surface*`, la YPF real, los
  surtidores reales) — no usa coordenadas hardcodeadas ni
  `MapLayout.PavedRoute` directamente para el spawn.
  - Si esto alguna vez deja de correr o el auto vuelve a aparecer en un
    lugar random: lo primero es confirmar que `SnapToRoadExtensionTip`
    sigue LLAMADO desde `Generate()` (se perdió esa conexión una vez en
    un merge) antes de tocar nada más.
- `Assets/_FolkloreArchives/layout_FullMap.json` (el "Save Map Layout"
  genérico) fue borrado por el compañero — tenía entradas viejas de
  clones que `MapLayoutPersistence.RecreateClones()` recreaba a ciegas
  en cada Generate, sin importar cuántas copias ya hubiera. Si vuelve a
  aparecer y a acumular objetos "X (N)" fantasma después de cada
  Generate, ese es el sospechoso número uno.

## Dos sistemas de control de versiones en este repo — cuidado

- **git**: código (`Assets/Scripts/`, `Assets/editor/`) y poco más (ver
  `.gitignore`, política "CODE-ONLY").
- **Unity Version Control (Plastic SCM)**, cliente `cm` en
  `/c/Program Files/PlasticSCM5/client/cm`: TODO lo demás (escena,
  terreno, materiales, layouts) — y también algo de código, porque el
  compañero edita directo desde Unity sin pasar por git.
- **Un mismo archivo de código puede tener cambios en AMBOS sistemas al
  mismo tiempo, y no se sincronizan solos entre sí.** Si vas a hacer
  `cm update`/`cm undo` sobre archivos que también están en git, primero
  confirmá que tus cambios de código están commiteados en git — después
  de cualquier operación de Plastic, comparar contra `git status`/`git
  diff` y restaurar con `git checkout HEAD -- <archivo>` si Plastic pisó
  algo que ya estaba resuelto en git.
- **No asumas que "Generate" te va a mostrar lo mismo que tiene el
  compañero.** Generate reconstruye el terreno/mapa PROCEDURALMENTE con
  la caché y semilla LOCALES — si la escena ya vino sincronizada con el
  mapa completo del compañero (su propio último Generate, guardado en el
  `.unity`), correr Generate de nuevo local puede pisar eso con un
  resultado distinto (terreno fragmentado, etc.). Si el pedido es "quiero
  ver lo que subió mi compañero", primero probar simplemente recargar la
  escena SIN tocar Generate.
