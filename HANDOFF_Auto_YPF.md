# HANDOFF — Configuración del auto (opening-drive) + YPF + NPCs

> Para la IA del compañero. Escrito desde la máquina de **joaquin** (rama `FidelGenre`),
> con la configuración que a él le quedó **bien**. Si en la máquina del compañero "no le
> queda bien" (el auto va por otro lado, entra mal a la YPF, los personajes chicos/sin
> colisión, etc.), este archivo dice **qué mirar y cómo dejarlo igual**. Complementa a
> `DEV_LOG.md` (ahí está el detalle largo de cada cambio).

## ⚠️ Lo más importante primero
Todos estos cambios se **HORNEAN al Regenerar el mapa** (viven en el código de los builders,
NO en la escena). Si el compañero abre la escena sin regenerar, ve el estado viejo horneado.

**Pasos para que le quede igual:**
1. Traer de la rama la última versión de estos archivos (todos ya commiteados):
   - `Assets/editor/MapGenerator/CarBuilder.cs`
   - `Assets/editor/MapGenerator/MapGenerator.cs`
   - `Assets/editor/MapGenerator/AreaPoiBuilder.cs`
   - `Assets/editor/MapGenerator/FriendNpcBuilder.cs`
   - `Assets/editor/MapGenerator/CriminalNpcBuilder.cs`
   - `Assets/Scripts/HumanWalkAnim.cs`
   - `Assets/Scripts/OpeningDriveSequence.cs`
   - `Assets/_FolkloreArchives/layout_FullMap.json`  ← **clave** (ver abajo)
2. **Regenerar**: `Tools ▸ Folklore Archives ▸ Generate Greybox Map`.
3. Mirar la consola: debe salir
   `[CarBuilder] Opening-drive reconstruido desde la escena: N puntos de asfalto, ...`

## Por qué depende del layout (la causa más probable de la diferencia)
El opening-drive **NO** usa coordenadas fijas: reconstruye el recorrido leyendo la
**geometría real de la escena** (las piezas de asfalto y la YPF, con su transform vivo).
Esa geometría la hornea `layout_FullMap.json`. Joaquin **movió a mano** el corredor, así que
si el compañero tiene otro `layout_FullMap.json` (o quedó un merge raro), el auto se arma
distinto aunque el código sea idéntico.

**Posiciones que tienen que coincidir** (valores de la máquina de joaquin, en `layout_FullMap.json`):

| Objeto (path en el layout) | pos | rot (euler) | escala |
|---|---|---|---|
| `/PavedRoad_Surface#0` (asfalto original) | (0, 0, **-143**) | (0,0,0) | 1 |
| `/PavedRoad_Surface (1)#0` (extensión a mano) | (**1011.8**, 0.34, **-130.5**) | (0.49, **353.96**, 0) | 1 |
| `/AreasAndPOIs#0/ML_009_EstacionYPF#0` (la YPF) | (**449**, 17, **-71.3**) | (0,0,0) | **2** |

Si en la máquina del compañero estos difieren, el auto y el estacionamiento van a otro lado.
La forma correcta de igualar es **usar el mismo `layout_FullMap.json`** (es el "source of
truth" de los movimientos a mano); no hardcodear coordenadas en el código.

## Config del auto (constantes en `CarBuilder.cs`)
Todo esto es lo que a joaquin le quedó bien. Si el compañero quiere lo mismo, deben coincidir:

- `RightLaneOffset = 8f` — corre el auto **8 m a la derecha** de la línea central (Argentina =
  mano derecha). Negativo = izquierda. Calculado por punto con `RightOf` (perpendicular derecha
  = up×forward de Unity), así respeta la mano derecha aunque la ruta doble.
- `YpfEntryOffset = (59.65, -18.13)` — dónde **dobla y entra** a la YPF, relativo al centro de
  la estación (el auto va derecho por la ruta y recién ahí dobla).
- `ParkBesidePumpDist = 3.5f` — frena a 3.5 m **al lado de un surtidor** (busca los surtidores
  del modelo por nombre: `pump`/`surtidor`/`dispenser`; si no encuentra, avanza 22 m al centro).
- El auto spawnea ~15 m adentro de la **punta más lejana** de la ruta (extensión incluida).

> Nota: NO atar el punto de entrada al `TEST_PLAYER` vivo — ese es el spawn del jugador y se
> mueve por otros motivos (si se lo pone al inicio del mapa, el auto se va hasta los árboles).

## Otros cambios de la sesión (también se hornean al Regenerar)
- **YPF sólida**: colliders en todo el modelo (`AddMeshColliders`) + piso de cemento con
  BoxCollider casi al ras (no traba al auto en la entrada). (`AreaPoiBuilder.cs`)
- **Líneas de estacionamiento** en el playón (grupo `LineasEstacionamiento`). (`AreaPoiBuilder.cs`)
- **NPCs con colisión**: `CapsuleCollider` horneado en amigos y criminales
  (`FriendNpcBuilder`/`CriminalNpcBuilder`). Se **apaga solo** mientras van sentados en el auto
  (lo maneja `HumanWalkAnim`, si no traba el manejo) y se prende al bajar/caminar.
- **Tamaño al bajarse**: `OpeningDriveSequence.StandFriend` ahora desparenta con
  `SetParent(null, false)` + `localScale = Vector3.one` (antes con `true` heredaban la escala
  del auto y quedaban chicos).

## Si sigue sin quedar igual — checklist
1. ¿Regeneró después de traer el código? (los builders solo corren en Generate).
2. ¿Tiene el **mismo `layout_FullMap.json`**? (comparar las 3 filas de la tabla de arriba).
3. ¿La consola tira el log `[CarBuilder] Opening-drive reconstruido...`? Si dice
   `0 surtidores detectados`, el modelo de la YPF no está resuelto en su escena (revisar que
   el FBX `Gas_station_Props` esté importado).
4. `Assets/_FolkloreArchives/Generated/` está **ignorada** (materiales por-máquina): NO debería
   afectar al auto, pero si el terreno/vegetación se ven raros, ver la entrada del DEV_LOG sobre
   "deps del terreno versionadas".
