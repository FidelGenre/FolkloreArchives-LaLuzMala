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

## 2026-08-04 — RAÍZ encontrada y arreglada: Generate duplicaba "PavedRoad_Surface"

Causa exacta del mapa fragmentado/ruta flotando después de Generate:
`RoadsideBuilder.BuildPavedRoadMesh()` creaba, en CADA Generate, un
`GameObject` procedural nuevo llamado literalmente `"PavedRoad_Surface"`
(mesh horneada desde `MapLayout.PavedRoute`, la ruta VIEJA/estática) —
sin chequear si ya existía uno. Al mismo tiempo, `MapGenerator.DeleteMap()`
rescata (desparentea antes de destruir la raíz vieja) cualquier objeto
`PavedRoad_Surface*` para no perder la ruta REAL que arma el compañero a
mano/EasyRoads3D — y ese rescatado se vuelve a colgar de la raíz nueva
más adelante en `Generate()` (línea ~179). Resultado: dos objetos con el
mismo nombre bajo el mismo padre en cada regenerado -- el rescatado
(real, con las extensiones del compañero) y el procedural nuevo
(desalineado, porque usa la ruta vieja y no el trazado extendido) --
literalmente la escena queda fragmentada donde el mesh viejo no coincide
con el terreno actual.

**Fix:** comentada la llamada a `BuildPavedRoadMesh()` en
`RoadsideBuilder.Build()` (`Assets/editor/MapGenerator/RoadsideBuilder.cs`).
La ruta real ya la provee el compañero y `CarBuilder.SnapToRoadExtensionTip()`
la lee en vivo -- este mesh procedural quedó obsoleto y ahora ni se crea.

**Segunda causa encontrada el mismo día (más importante):** aunque el
mesh duplicado de la ruta ya no aparecía, Generate seguía dando un mapa
"partido" (terreno igual, pero árboles/casas/campamento en posiciones
distintas a las de la escena sincronizada) porque `layout_FullMap.json`
estaba BORRADO -- sin ese archivo, `MapLayoutPersistence.ApplySavedLayout()`
no tiene nada que reaplicar después de que los Builders procedurales
reconstruyen todo desde cero, así que cualquier cosa ajustada a mano
(fuera de lo que el código calcula por fórmula) se pierde en cada
regenerado.

**SOLUCIÓN QUE FUNCIONÓ:** con el mapa en el estado bueno (recién
sincronizado, SIN tocar Generate), correr primero
`Tools > Folklore Archives > Save Map Layout` (genera `layout_FullMap.json`
fresco desde el estado actual) y RECIÉN DESPUÉS `Generate Greybox Map`.
Confirmado por el owner: así el mapa vuelve a salir conectado e igual al
que tiene el compañero. **Antes de tocar Generate en este workspace,
correr siempre Save Map Layout primero con el mapa en buen estado.**

**Tercera causa (resuelta):** con lo de arriba aplicado, quedaban 1-2
piezas magenta cerca de la casa de la vieja (ej. `q9:Mesh1`, una submalla
real del prefab `House_Prefab` de ALP_Assets -- no es nada inventado por
código nuestro, confirmado leyendo el .prefab). La causa: `NappinUrp()`
(en `HouseBuilder.cs`) cachea los materiales URP convertidos en
`_napMatCache`, un `Dictionary` **static** keyeado por el material
Standard original -- sobrevive entre corridas de Generate en la misma
sesión del Editor. Cada Generate destruye TODO el mapa viejo
(`DeleteMap`), lo que puede invalidar esos materiales (viven solo en
memoria, nunca se guardan como `.mat`) -- pero el caché seguía
devolviendo la referencia vieja ya rota. **Fix:** `_napMatCache.Clear()`
al principio de `BuildAlpHouse()`.

**Cuarta causa (resuelta, script runtime, no relacionada a Generate):**
con el mapa ya bien, el auto arrancaba a girar feo apenas empezaba a
manejar solo (jugador y perro ya sentados adentro). Causa:
`CarAutoDrive.HitIsAsphalt()` solo reconocía asfalto real por nombre de
MATERIAL ("asphalt" en el nombre) -- la ruta real del compañero
(`PavedRoad_Surface*`) no tiene por qué llamarse así. El auto arrancaba
parado ENCIMA de esa pieza y el chequeo la rechazaba, activando
`rescuing` (steering x3 buscando "asfalto real") desde el primer frame.
Fix: `HitIsAsphalt` ahora también reconoce la ruta por NOMBRE de objeto
(`PavedRoad_Surface*`), mismo criterio que ya usa `CarBuilder`/
`MapGenerator`. Es un script runtime (`Assets/Scripts/CarAutoDrive.cs`)
-- aplica solo con que Unity recompile, no hace falta Generate.

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
