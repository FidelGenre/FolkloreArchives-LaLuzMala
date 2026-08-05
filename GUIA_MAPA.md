# GUÍA DEL MAPA — cómo funciona y por qué "al Regenerar se cambia todo"

> Para la IA del compañero (y para no perder trabajo). `MAP_README.md` explica la
> arquitectura de builders; este archivo explica el **flujo de Regenerar y la persistencia**,
> que es lo que causa "se me cambió todo al regenerar". `DEV_LOG.md` tiene la historia
> detallada. `HANDOFF_Auto_YPF.md`, la config del auto/YPF.

## 1. Idea central
El mapa es **híbrido**: una base **generada por código** + **ajustes a mano** que se
**congelan** en archivos versionados. Regenerar **rehace la base desde el código** y después
**vuelve a aplicar** los ajustes guardados encima. Por eso:

> **Todo cambio a mano que NO esté guardado en la persistencia, se PIERDE al Regenerar.**
> Regenerar no es "refrescar"; es "reconstruir desde cero + reaplicar lo guardado".

## 2. Qué pasa exactamente al Regenerar
`Tools ▸ Folklore Archives ▸ Generate Greybox Map` → `MapGenerator.Generate()`:

1. **`DeleteMap()`** — borra TODO el objeto raíz `FOLKLORE_MAP`. (Rescata aparte los
   *terrenos extra* que agregaste a mano para no borrarlos.)
2. **Builders** (código) — reconstruyen el mapa desde cero leyendo `MapLayout.cs` (datos puros:
   posiciones, caminos, tuning). Acá NADIE tiene coordenadas movidas a mano; todo sale del código.
3. **`MapLayoutPersistence.ApplySavedLayout()`** — lee `layout_FullMap.json` y **pisa** pos/rot/
   escala de los objetos con lo que guardaste, borra los que marcaste como borrados y recrea los
   duplicados `(1)`,`(2)`. **Esto corre AL FINAL**, así que gana sobre el código.
4. Ajustes finales de código que dependen de la escena ya armada (ej. el opening-drive del auto,
   `CarBuilder.SnapToRoadExtensionTip`), y el cosido de terrenos vecinos.

Resultado: la posición final de cada cosa = **código**, salvo que esté en `layout_FullMap.json`,
donde manda **la mano**.

## 3. Los DOS sistemas de persistencia (lo que sobrevive a Regenerar)
Todo lo demás se pierde. Solo sobrevive lo guardado en estos dos, **y commiteado a la rama**:

### a) `layout_FullMap.json` — posiciones/borrados/duplicados
- Guarda pos + rot + escala de cada objeto (por su path/nombre), qué está borrado y qué es duplicado.
- **Cómo guardar:** moviste/rotaste/escalaste/borraste/duplicaste algo → `Tools ▸ Folklore
  Archives ▸ Save Map Layout`. Si no lo hacés, el próximo Regenerate lo devuelve a donde lo pone el código.
- Ojo: si querés que **el código** vuelva a mandar sobre un objeto (que se mueva solo al cambiar
  `MapLayout.cs`), hay que **sacar su entrada** de este JSON — mientras esté, el JSON congela.

### b) `Assets/_FolkloreArchives/ExtraTerrains/MergedTerrain.asset` — el terreno
- El terreno **YA NO se regenera** procedural. Es **un solo `TerrainData` permanente** (alturas +
  pintura + árboles + pasto horneados). `TerrainBuilder` lo reusa tal cual si el asset existe.
- **Cómo guardar:** editaste alturas/pintura a mano → `Tools ▸ Folklore Archives ▸ Save Terrain`
  (hace `SetDirty` + `SaveAssets`). Si no, Unity puede no persistir el `.asset`.
- No hay "diffs" de terreno (los sistemas viejos de `terrain_edits`/`paint`/`tree_removals` quedaron
  dormidos porque se corrompían; ver DEV_LOG). Se edita a mano y se guarda con Save Terrain.

## 4. Regla de oro para NO perder trabajo
1. Hacé el cambio a mano en la escena.
2. Guardalo: **Save Map Layout** (objetos) y/o **Save Terrain** (terreno).
3. **Commiteá** a la rama `FidelGenre`: `layout_FullMap.json`, `MergedTerrain.asset` (+ sus deps
   en `Assets/_FolkloreArchives/TerrainAssets/`), la escena `SampleScene.unity` y el código tocado.
4. El compañero **trae** eso y **Regenera** → le queda igual.

Cambios estructurales/de comportamiento (no solo mover un objeto) van **horneados en el builder**
(código), no como edición suelta de escena — así son regenera-seguros y se comparten por código.

## 5. Por qué al compañero "se le cambia todo" (causas y solución)
- **No trajo el último `layout_FullMap.json`** (o hubo merge raro en ese JSON gigante) → Regenera y
  el mapa vuelve a las posiciones del **código**, no a las tuyas. **Solución:** que use el mismo
  `layout_FullMap.json` de la rama (es el "source of truth" de lo movido a mano).
- **No trajo el `MergedTerrain.asset`** (o sus deps en `TerrainAssets/`) → terreno distinto / "todo
  blanco". **Solución:** traer ese asset y su carpeta de deps (están versionados).
- **Editó a mano y no guardó** (sin Save Map Layout / Save Terrain) antes de Regenerar → lo perdió.
- **Espera que Regenerar respete ediciones sueltas de escena** → no lo hace; solo respeta lo que
  está en los 2 sistemas de persistencia de arriba.
- `Assets/_FolkloreArchives/Generated/` está **ignorada** a propósito (materiales por-máquina, para
  evitar conflictos "evil twin"); no se comparte y se regenera sola. No debería cambiar geometría.

## 6. Flujo de colaboración recomendado
- **Una sola persona mueve el layout a la vez** (el `layout_FullMap.json` es un archivo enorme y no
  hace merge lindo). El que movió cosas: Save Map Layout → commit. El otro: trae → Regenera.
- Cambios de **código** (builders) sí se pueden trabajar en paralelo y mergear normal.
- Antes de Regenerar, el que trae cambios conviene que **primero traiga** (layout + terreno + código)
  y **recién ahí** Regenere, para partir del mismo estado.

## 7. Mapa de archivos clave
| Archivo | Qué es |
|---|---|
| `Assets/editor/MapGenerator/MapLayout.cs` | Datos puros (coordenadas, caminos, tuning). Cambiás acá → afecta a todo. |
| `Assets/editor/MapGenerator/MapGenerator.cs` | Orquesta el Generate (orden de builders + persistencia). |
| `Assets/editor/MapGenerator/*Builder.cs` | Cada uno arma una parte (terreno, bosque, POIs, auto, NPCs...). |
| `Assets/editor/MapGenerator/MapLayoutPersistence.cs` | Save/Apply del `layout_FullMap.json`. |
| `Assets/_FolkloreArchives/layout_FullMap.json` | Congela posiciones/borrados/duplicados a mano. |
| `Assets/_FolkloreArchives/ExtraTerrains/MergedTerrain.asset` | El terreno permanente. |
| `Assets/_FolkloreArchives/TerrainAssets/` | Deps del terreno (capas/texturas/árboles) versionadas. |
| `MAP_README.md` | Arquitectura de builders (nota: la parte de terreno procedural está vieja; hoy es permanente, ver §3b). |
| `DEV_LOG.md` | Historia de cambios (lo más nuevo arriba). |
| `HANDOFF_Auto_YPF.md` | Config del auto/opening-drive + YPF. |

## TL;DR
Regenerar = reconstruir desde código + reaplicar `layout_FullMap.json` y usar `MergedTerrain.asset`.
Lo que quieras conservar: **Save Map Layout** y/o **Save Terrain**, y **commitealo**. Para que a
dos máquinas les quede igual, las dos tienen que partir del **mismo layout + mismo terreno + mismo
código** y recién ahí Regenerar.
