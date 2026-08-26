# DEV LOG — LA LUZ MALA map generator

Running log of AI-assisted changes to the greybox map generator, kept in this
folder so any AI session (Claude, etc.) working on this project can catch up
on recent context without re-deriving it from scratch. Newest entries on top.
See `MAP_README.md` for the static architecture reference, **`GUIA_MAPA.md` para entender el
flujo de Regenerar y la persistencia (por qué "al regenerar se cambia todo" y cómo evitarlo)**,
y `HANDOFF_Auto_YPF.md` para la config del auto/opening-drive + YPF.

**2026-08-03: log reseteado a propósito.** El historial viejo (cientos de
entradas) quedó lleno de intentos fallidos, diagnósticos equivocados y
reversiones sobre la ruta pavimentada / spawn del auto — más ruido que
señal. Se resume acá SOLO el estado actual y lo que hay que saber para no
repetir los mismos errores. Si necesitás el detalle histórico, está en
`git log` (código) y en el historial de Unity Version Control / Plastic
(escena y assets — dos sistemas de control de versiones DISTINTOS en este
proyecto, ver más abajo).

## 2026-08-07 — Pack PS1 "Haunted Nature" (5 árboles + 6 arbustos) + limpieza de árboles Missing

owner: "agregame los arboles y bushes que trae este archivo asi los puedo ir agregando a mano"
(pack fortunaliquida, itch.io — venía como UN `.blend` con 11 objetos + texturas empaquetadas).
Se exportó con **Blender 5.1 headless** a `Assets/ExternalAssets/HauntedNature/{Models,Textures}`:
11 FBX (`bush1`–`bush6`, `tree1`–`tree5`) con pivote en la base + 17 PNG despaqueteadas.
`HauntedNaturePackImporter.cs` (AssetPostprocessor + InitializeOnLoad, SIN botón) hace la parte de
Unity: texturas a filtro Point/alpha, crea materiales URP + prefabs `HN_*` en `HauntedNature/Prefabs/`
(copa/arbusto = cutout AlphaClip DOBLE cara; tronco = opaco), y **auto-agrega los 11 como prototipos
de árbol al pincel Paint Trees de todos los terrenos** (`EnsurePrototypes`, una vez por máquina vía
guard `EditorPrefs Folklore_HN_ProtosAdded` — si después borrás alguno del pincel a mano no te lo
re-agrega). Agregar prototipos NO afecta instancias ya pintadas. El owner los pinta a mano; después
Save Terrain + commit (incluí `Assets/ExternalAssets/HauntedNature/`).

owner: "se perdieron los arbustos... eliminalos". Los arbustos VIEJOS eran prototipos de árbol
"Missing" (prefab per-máquina perdido; los pinos/PSX son versionados → NO están Missing). Se limpiaron
con un menú one-shot (`set_treePrototypes` sin los null + reindex de instancias) que **ya se corrió y
se removió** — no quedó en el menú (owner: "ya podes borrar el boton").

owner: "mira lo chicos que se ven... dejalos a la misma altura [que los pinos]" + "aumenta los bushes
en la misma proporción". Los modelos venían a ~1–1.4 m (árboles) / ~0.22–0.28 m (arbustos); los pinos
(BigPine = PSX ×3, pintados ×0.9–1.5) rondan 7–11 m. `HN_Scale = 6f`: se escala la RAÍZ de cada prefab
HN ×6 (mismo criterio que BigPine), MISMO factor árboles y arbustos → árboles ~6–8 m, arbustos ~1.3–1.7 m.
Después: "aumentame un 1.5 el tamaño de los arboles nuevos, los otros dejalos como estan" → escala
SEPARADA: `HN_TreeScale = 9f` (6×1.5) para `HN_tree*`, `HN_BushScale = 6f` para `HN_bush*` (sin cambio),
pinos intactos. Se elige por prefijo del fbx. Bump `PrefabVersion` (→3) reconstruye los prefabs ya creados.

owner: "los nuevos arboles no tienen fisicas para moverse con el viento, añadiselo". Las copas HN
pasaron de URP/Lit cutout a shader **`Folklore/TreeWind`** (Assets/Scripts/TreeWind.shader) — el MISMO
que usan las copas de los pinos PSX (`PsxMat(..., wind:true)`): cutout + `Cull Off` + balanceo por
`_Time` y altura del vértice, con `_WindStrength=0.68`/`_WindSpeed=1.0`. El TRONCO sigue en URP/Lit
(no se mueve, como el `PSX_PineTrunk`). No necesita WindZone (el shader anima solo). `PrefabVersion=4`.

owner: "los ultimos 3 arboles tienen las texturas rotas" (tree3/4/5 = copas marrones gigantes). Causa:
el exportador FBX NO mantiene el orden de submesh — en tree3/4/5 la copa (quad) quedó en el slot 0 y el
tronco en el 1, al revés que tree1/2. La asignación fija por posición (`slot0=tronco`) les ponía la
corteza en la copa. Fix (`PrefabVersion=2`): asignar el material por **nombre** del material original
(`"wood1"/"WOOD2"` = tronco), no por índice → corteza siempre al tronco, copa al quad. Al reconstruir
(overwrite, GUID intacto) + `RefreshPrototypes` las instancias pintadas se arreglan solas.

## 2026-08-07 — Postes de luz a lo largo de la ruta + ASSET_CREDITS.md

owner: "postes de luz a lo largo de TODA la ruta, de inicio a fin". Asset "Electric Pole" by
notsospecialgames (itch.io) → `Assets/ExternalAssets/ElectricPole/{electric_pole,wire}.glb` (GLB nativo).
`ElectricPoleBuilder.Build(mapRoot)` (llamado en MapGenerator DESPUÉS de ApplySavedLayout, para leer las
piezas de asfalto de la EXTENSIÓN que recién existen tras RecreateClones): lee la línea central de TODAS
las piezas `PavedRoad_Surface (N)` de la malla viva (índice i*5+1 = centro, igual que CarBuilder), ordena
inicio→fin, y planta un poste cada 26 m (≈ largo del cable del asset) sobre un hombro (11 m del centro),
apoyado en el piso, travesaño cruzado a la ruta. Postes = PROCEDURALES (no hand-movable: se crean después
del layout). Alto de poste 9 m; el GLB venía ~10.9 m. **Cables** (`wire.glb`, eje largo = Z nativo 24 m):
`SpawnWire` tiende un cable entre los puntos de enganche de postes vecinos (`SpawnPole` devuelve el
enganche = punta − 0.8 m), orientado con `LookRotation` y escalado solo en Z a la distancia; se recentra
por si el pivote no está en el medio; se saltea si el hueco es > Spacing×1.7 (poste salteado). ⚠ FIX de
orden: el builder tuvo que moverse en MapGenerator a DESPUÉS del re-parenteo de las `PavedRoad_Surface`
(línea ~224, junto al SnapToRoadExtensionTip real) — antes corría cuando las piezas seguían sueltas en la
raíz de la escena y no encontraba ninguna ("no encontré piezas de asfalto").
owner: "poné DOS cables, uno a cada lado, no uno al centro" → `SpawnPole` devuelve el centro y se calculan
2 enganches L/R = centro ± perp×`WireSpread` (1.6); se tiende un cable por lado (2 por tramo).
owner: "no se guardan cuando muevo los postes" → como se crean después de ApplySavedLayout, se agregó
`MapLayoutPersistence.ApplySavedToGroup("PostesDeLuz")` (llamado tras `ElectricPoleBuilder.Build`): re-aplica
el layout guardado SOLO a ese grupo → mover/borrar un POSTE y Save Map Layout persiste.
owner: "dos cables uno a cada lado" (largo forcejeo): al final los cables SÍ estaban (2 por tramo, a ±1.9
del centro = las puntas del travesaño), solo que finitos y no se veían. Un intento con cilindro propio salió
gigante porque el layout le aplicó una escala vieja → REVERTIDO al wire.glb del asset.
owner: "generalos en TODOS los postes" → (1) se subió el umbral de salteo de 44 m a 150 m (antes salteaba
tramos de postes lejanos). (2) los CABLES son 100% PROCEDURALES: `SkipSubtree` ahora incluye `Cable_*` →
NO se guardan ni se les aplica layout (si se guardaran, al cambiar la cantidad los índices se corren y el
layout les pone posiciones viejas encima = se descolocan). Los postes se guardan; los cables los siguen.

owner pidió además llevar un LOG de licencias: creado **`ASSET_CREDITS.md`** (raíz) con todos los assets
(autor/fuente/licencia/uso); actualizar en CADA descarga. Ver memoria feedback_asset_credits.

## 2026-08-07 — PC noventoso + silla en la YPF

owner: "añadí el PC y la silla, dejalos cerca de la YPF que yo los acomodo". Dos GLB
(`90s_desktop_pc_-_psx.glb` de visualdiscette CC-BY, y `low_poly_office_chair.glb`) copiados a
`Assets/ExternalAssets/DesktopPC/{desktop_pc,office_chair}.glb`. `AreaPoiBuilder.PlaceYpfComputer(g,p,t)`
(llamado en `YpfStation`): instancia ambos con `SpawnModelFrom` (auto-escala + apoya en el piso) cerca
del centro del lote — PC ~0.65 m de ancho, silla ~1.15 m de alto. Nombres ÚNICOS (`DesktopPC_YPF`,
`OfficeChair_YPF`) → el owner los mueve/escala y el layout (Save Map Layout) guarda su transform. El PC
venía a escala grande (5.6 m) → por eso el auto-escala. Crédito a acreditar: "90s Desktop PC - PSX" by
visualdiscette (CC Attribution).

## 2026-08-07 — Re-brand de la estación a YPF (logo + colores)

owner: "cambiá el logo '6twelve' por el de YPF y los colores de alrededor, como en la foto". El modelo
`GasStationProps` (GLB, materiales embebidos) trae la marca "6twelve": material `6twelve.001` (Image_24)
= logo del techo; material `Sign` (Image_1, rayas rosa/cyan) = banda de la marquesina (mallas
`6twelve_Sign_0` + `The_ceiling_Sign_0`). NO se pueden editar como .mat sueltos → se **sobreescriben en
el builder** tras instanciar. `AreaPoiBuilder.StyleYpfStation(st)` (llamado en `YpfStation` junto a
`AddMeshColliders`): banda → material navy YPF (0.03,0.06,0.16); logo → material con textura
`ypf_logo.png` (generada con PowerShell System.Drawing: fondo azul YPF + "YPF" blanco, centrada para las
UV del panel [0.12,0.96]), emisiva para leerse de día/noche. Identifica el logo por nombre de malla
(contiene "logo") y la banda por material == "Sign" (no toca el tótem `6twelve_Sign`). Materiales
versionados con `SaveMaterialStable`. Reemplazar `ypf_logo.png` por el logo oficial (mismo path) lo actualiza.

Ajustes: (1) el GLB trae la V invertida → el logo salía AL REVÉS (upside down); se da vuelta desde el
material (`_BaseMap`/`_EmissionMap` scale.y = -1, offset.y = 1) → sirve con cualquier imagen sin editarla.
(2) la banda no era como la foto (owner: "franjas GRISES con una AZUL central") → se cambió el color sólido
por textura `ypf_band.png` (5 franjas horizontales gris/gris-oscuro/AZUL/gris-oscuro/gris, simétrica =
a prueba de flip) mapeada con las UV originales de la banda. (3) owner: "brilla mucho... tiene un borde
blanco" — era la EMISIÓN (bloom = halo blanco + tapaba las sombras). Se SACÓ la emisión del logo
(DisableKeyword _EMISSION + EmissionColor negro + GI None, smoothness 0) → material lit normal que
proyecta/recibe sombras, sin halo. Si de noche queda muy oscuro, subir una emisión suave. (4) borde
blanco al costado = las caras LATERALES de la caja del cartel muestrean la textura en u≈0.78 / v≈0.82
(derecha/arriba), donde caían las letras. Fix: `ypf_logo.png` recompuesto a 512×512 con el logo CENTRADO
al ~47% + margen azul (bg (0,80,144)), así los cantos caen sobre azul. ⚠ Si se reemplaza el logo, dejarlo
centrado con margen azul (no que las letras lleguen a los bordes) o vuelve el canto blanco. (5) owner:
"quedó chiquito" → se agrandó el logo (ocupa casi todo el frente) y para que NO vuelva el borde se
arregla la MALLA: `FixLogoRimUVs` clona la malla del cartel y manda la UV de las caras del canto (eje
más fino del panel) a una esquina azul → los cantos siempre azules sin importar el tamaño del logo.

## 2026-08-07 — Tótem de precios re-brandeado a YPF

owner: "el cartel de precios tiene que quedar como el de la foto". El tótem es la malla
`6twelve_Sign_6twelve_Sign_0`, material `6twelve_Sign` (Image_53 base metal gris + Image_54 emisión
negra). El contenido de la textura está ESPEJADO (la UV lo da vuelta al mostrarlo). Se extrajeron las
2 texturas (256²) y se PINTÓ ENCIMA en las mismas posiciones (espejado) con System.Drawing: caja azul
"YPF" donde estaba el logo 6twelve, y labels SUPER/INFINIA/DIESEL sobre las 3 filas → `ypf_totem_base.png`
+ `ypf_totem_emis.png`. `StyleYpfStation`: material `_ypfTotemMat` (base + emisión enmascarada al 0.6 →
solo YPF/precios brillan, como cartel iluminado) asignado donde el material sea `6twelve_Sign`. Los
dígitos de precio quedaron los originales (naranjas). Editar los ypf_totem_*.png para cambiar precios/labels.
CORRECCIÓN: el primer intento salió ESPEJADO — la malla del tótem muestra la textura TAL CUAL (no la da
vuelta), así que el `FlipX` que le había puesto era el error. Regenerado SIN espejar + precios en PESOS
(blancos: SUPER 1.020 / INFINIA 1.220 / DIESEL 1.067). Es solo cambio de textura → foco en Unity (sin
regenerar). ⚠ Rutas de las imágenes YPF: `Assets/ExternalAssets/GasStationProps/ypf_{logo,band,totem_base,totem_emis}.png`.

## 2026-08-07 — Cerco de alambre alrededor de la YPF (PSX Modular Chain-Link Fence)

owner: "poné vallas de alambre alrededor de la YPF" (asset PSX de DanglingBat, itch.io — venía en
GLB; la página NO trae instrucciones). Los 6 GLB (recto ×2, extremos, esquinas ext/int) se pasaron a
FBX con Blender headless a `Assets/ExternalAssets/ChainLinkFence/{Models,Textures}`, pivote base-centro.
Recto = 2×2 m. `ChainLinkFenceImporter.cs` (AssetPostprocessor) configura texturas (Point + alfa, y el
`*_normal` como Normal map). `AreaPoiBuilder`: `FenceMaterials()` crea 2 materiales versionados con
`SaveMaterialStable` (malla `chain_link` = cutout DOBLE cara; `galvanized_steel` = opaco), asignados por
NOMBRE de material (no por submesh). `FenceYpf()` cierra el lote (±27 X, ±24 Z, derivado de YpfPad*) con
paneles rectos de 2 m en **NORTE/OESTE/SUR**; el **ESTE queda ABIERTO** = entrada (el auto de la
secuencia ingresa desde el sureste cruzando ese lado — se verificó que NO cruza N/O/S). Cada panel lleva
BoxCollider fino (2×2×0.15) para que el jugador no lo atraviese. Horneado en el builder → regenerate-safe.

⚠ OJO — los paneles se llaman TODOS "Valla" A PROPÓSITO. El owner AGREGA vallas a mano (duplicando →
"Valla (N)"), y el layout (`RecreateClones`) las recrea en cada Generate clonando el panel base "Valla".
Un intento de renombrarlas a `Valla_N` (para poder destildar una sola) ROMPIÓ esto: RecreateClones dejó
de encontrar la base "Valla" y NO recreó las vallas puestas a mano → "me borraste todas las vallas".
REVERTIDO. Los paneles deben seguir llamándose "Valla". El JSON NUNCA se tocó (las vallas a mano están
en layout_FullMap.json). Si el owner quiere borrar UN panel puntual, es tema del sistema de layout, NO
renombrar. Pendiente: una forma segura de borrar un panel sin romper las vallas a mano.
RESUELTO: `AreaPoiBuilder.RemoveUnwantedFencePanels(mapRoot)` (llamado en MapGenerator DESPUÉS de
ApplySavedLayout, para agarrar builder + clones) borra paneles del cerco por POSICIÓN LOCAL (x,z) según
lista `FenceRemoveLocal` (tolerancia 1.5 m). destildar no servía porque había panel builder + duplicado
a mano superpuestos. Para sacar un panel: agregar su Position (X,Z) del Inspector a `FenceRemoveLocal`.
Actual: (-2, 24).

## 2026-08-07 — Colisiones a la Torre Mirador (HuntingTower)

owner: "añadile colisiones a este objeto". `AreaPoiBuilder.BuildMirador`: tras spawnear la torre
(`TorreMirador`, FBX HuntingTower) y arreglar su material, se llama a `AddMeshColliders(towerInst,
"torre mirador")` — MeshCollider NO-convexo por cada mesh real (patas/escalera/plataforma/barandas),
mismo helper que la estación YPF. Se le agregó un param `label` a `AddMeshColliders` (para el log) y
un guard `root == null`. Horneado en el builder → se re-aplica cada Generate (no hace falta a mano).

## 2026-08-07 — Capas de terreno "Missing": movidas a carpeta VERSIONADA + auto-cura

**Problema:** el terreno perdió texturas (tierra/barro, sendero, asfalto, PSX, nieve): varias
`Terrain Layers` mostraban "Missing". **Causa (NO fue el fix #1):** esas `.terrainlayer` y sus
texturas generadas (ruido/rotadas/tintadas) se guardaban en `Assets/_FolkloreArchives/Generated`
(carpeta IGNORADA, per-máquina). El terreno permanente (`MergedTerrain.asset`, VERSIONADO) las
referencia por GUID. `UseMergedTerrain` reusa el terreno TAL CUAL (no re-pinta), así que al
perderse/vaciarse `Generated/` las referencias quedaron muertas → "Missing". Además divergían
entre las dos máquinas (mismo defecto de raíz que los materiales del fix #1: asset versionado
apuntando a asset per-máquina).

**Fix (dos partes):**
1. **Versionar las capas.** Nueva carpeta `Assets/_FolkloreArchives/TerrainLayers` (VERSIONADA).
   Las 5 funciones de capa (`MuddyDirtLayer`, `TrailLayer`, `PavedRoadLayer`, `PsxLayer`,
   `CreateLayer`) + `NoisyTexture` + `Rotate90`/`Tint` (nuevo param `folder`) ahora escriben ahí.
   Contenido 100% determinista (`Mathf.PerlinNoise` con coords fijas, rotación/tinte de texturas
   versionadas) + reusa-si-existe → GUID estable; commiteado una vez, las dos máquinas comparten
   los MISMOS assets (igual que `SaveMaterialStable`).
2. **Auto-cura.** `TerrainBuilder.BuildLayers()` (extraído de `PaintTextures`) arma las 9 capas en
   orden fijo. `HealMissingLayers(td)`: si hay algún slot null (Missing) y la cantidad coincide con
   la procedural (9), reasigna el array completo (mismo orden → el splat pintado a mano NO se toca,
   es dato aparte). Se llama en `UseMergedTerrain` y para TODOS los terrenos en `MapGenerator`
   después de `RoadTerrainBuilder.Build`. Si alguien cambió las capas a mano (cantidad ≠ 9), avisa
   y no toca nada. **Aplica al Regenerar; después Save Terrain + commit** (así las capas versionadas
   y el terreno con las refs nuevas quedan compartidos y no vuelve a pasar).

## 2026-08-07 — Save Map Layout: los borrados (destildar) ya no reaparecen

**Problema:** destildabas el check activo de un objeto ("ocultar = borrar") + Save Map Layout, pero
al regenerar reaparecía. **Causa:** `ApplySavedLayout` BORRA con `DestroyImmediate` los objetos con
`deleted=true`. En el próximo Save Map Layout, `Walk` ya no ve ese objeto (fue destruido) → su
entrada `deleted=true` se perdía del JSON → al regenerar, el builder lo recreaba sin nada que dijera
"borralo" → reaparecía. En un flujo donde se guarda el layout seguido, los borrados nunca "quedaban".

**Fix:** `MergeOldDeletions(fresh)` en `SaveMapLayout`: antes de escribir, recupera del JSON anterior
las entradas `deleted=true` cuyo path ya NO está en la escena y las vuelve a meter. Así un borrado no
se olvida nunca (persiste entre saves). Para DES-borrar algo: re-tildarlo ANTES de que un Regenerar
lo destruya (si ya está presente/activo, su entrada nueva `deleted=false` pisa la vieja), o
`Clear Map Layout` para resetear todo. El borrado sigue aplicándose al **Regenerar**.

## 2026-08-06 — Auto spawnea al final de la ruta CON TERRENO (no en la punta flotante)

owner: "con el nuevo camino el auto se fue al final de ese y no en donde estaba, dejalo al
final de la ruta con terreno". El `RoadExtensionTerrain` es permanente y ya no se re-extiende,
así que las piezas de asfalto (`PavedRoad_Surface (N)`) que se agregan MÁS ALLÁ de él quedan
flotando sin terreno debajo. `CarBuilder.SnapToRoadExtensionTip` calculaba el spawn en
`pts[0]` = punta absoluta del asfalto → caía en la pieza flotante. **Fix:** tras armar la
línea de puntos, se recorta del frente (punta lejana) todo punto que caiga FUERA de cualquier
terreno (nuevo helper `IsOverAnyTerrain`, chequea la huella XZ de cada `Terrain`), así `pts[0]`
pasa a ser la punta más lejana que sí pisa terreno. Si ningún punto estuviera sobre terreno,
no recorta (comportamiento viejo). Se aplica en el próximo **Regenerar**.

## 2026-08-06 — Fix conflictos: materiales con GUID estable (`SaveMaterialStable`)

**Problema:** cada intercambio con el compañero traía ~46 conflictos en Plastic, casi
todos sobre `Assets/Settings/*.mat` (PSX_*, House_*, RetroCar*, Cemetery, HuntingTower,
DockWharf, WoodenFence, PSX_Character). **Causa:** los builders creaban esos materiales con
`AssetDatabase.DeleteAsset(path)` + `CreateAsset(mat, path)` en CADA Generate. Borrar+crear
le asigna un **GUID nuevo** cada vez → los dos regeneran → los mismos .mat quedan con GUIDs
distintos en cada máquina → Plastic los ve como "borrado de un lado / creado del otro" →
conflicto de estructura de directorios en cada exchange.

**Fix:** nuevo helper `BuilderUtils.SaveMaterialStable(Material built, string path)`: si el
.mat ya existe, **reusa ese asset** (GUID intacto) y le re-copia todo (shader +
`CopyPropertiesFromMaterial` + `renderQueue` + `globalIlluminationFlags`); si no existe, lo
crea (única vez). Devuelve el material que quedó en disco → el llamador DEBE usar el valor
devuelto. Reemplazado el par Delete+Create en los 12 sitios de materiales VERSIONADOS:
`OldLadyNpcBuilder`, `NetworkBuilder`, `FriendNpcBuilder` (x2), `CriminalNpcBuilder`,
`FenceBuilder`, `CarBuilder` (x2: carrocería + vidrio), `AreaPoiBuilder` (x4: tower, house,
tombstone, dock). Los de `Generated/` (mat_puddle, day/nightsky) se dejaron: esa carpeta está
en `ignore.conf`, nunca conflictúan. `ForestBuilder.PsxMat` ya usaba este patrón (era el
modelo a copiar).

⚠ **Última tanda de conflictos, una sola vez:** la primera vez que cada uno corre el código
nuevo, si el .mat todavía no existe se crea con GUID nuevo. Después de que AMBOS commiteen UNA
versión compartida, se estabiliza y no vuelve a chocar. Esto NO arregla los conflictos de
`SampleScene.unity` (los dos regeneran el mapa dentro de la escena) — eso es tema de flujo:
que uno solo regenere/commitee la escena por vez.

## 2026-08-03 — Toggle "Ver árboles de lejos (editor)"

owner: pidió un botón tildable (como Play From Scene View) para ver los árboles desde más lejos
mientras trabaja (ahora solo se ven de cerca — los PSX no billboardean bien, ver warning
Nature/Soft Occlusion). Nuevo `TreeViewDistanceToggle` (`Tools ▸ Folklore Archives ▸ Ver árboles
de lejos (editor)`). Tildado: fuerza `treeDistance`+`treeBillboardDistance` = 2000 en todos los
terrenos (malla completa, porque no billboardean), re-aplicando cada 0.5s por si Generate/día-noche
lo pisan. Destildado o al entrar a Play: restaura `MapLayout.TreeRenderDistance`. 100% editor-only
(EditorPrefs, no entra al build). Pesado con muchos árboles → es para trabajar cómodo, destildar
al terminar. (Excepción a "no hacer herramientas de un-click": el owner la pidió explícitamente.)

---

## 2026-08-03 — Terreno de Unity chico con bosque a los lados de la ruta extendida

owner: la ruta EXTENDIDA a mano (`PavedRoad_Surface (N)`) flotaba sobre el vacío; quería un
**terreno de Unity real** (con árboles), lo más chico posible, con bosque a ambos lados.

Nuevo `RoadTerrainBuilder.Build(mapRoot)` (llamado en `MapGenerator.Generate` después de
`ApplySavedLayout`/snap del auto). Como los terrenos de Unity son rectángulos alineados a los
ejes (no se curvan), "lo más chico" = el **AABB de la línea central de la extensión + margen**
(25m). La extensión va en diagonal (~-6°), así que el rectángulo queda largo y la ruta lo cruza
→ bosque a los dos lados. Terreno **plano** ~0.4m bajo la ruta (leve banquina), 1 capa de pasto,
pinos `Conifers [BOTD]` repartidos en grilla dejando **libre el corredor** de la ruta
(`RoadClearHalf`=9m), con `TerrainCollider`. Copia el material URP del terreno principal.

Se **rehace cada Generate** leyendo la posición viva de la extensión (si movés la ruta, la sigue).
Al principio borra cualquier `RoadExtensionTerrain` previo (DeleteMap lo "rescata" a la raíz por
tener otro TerrainData → hay que limpiarlo para no acumular). Asset versionado en
`Assets/_FolkloreArchives/ExtraTerrains/RoadExtensionTerrain.asset`. Ajustes arriba del archivo:
`Margin`, `RoadClearHalf`, `TreeStep`. Limitaciones: terreno plano (no sigue el leve tilt de la
ruta) y puede no auto-conectar sin costura con el terreno principal (distinto tamaño/resolución)
— si se ve un escalón/costura en el empalme, avisar.

**Pasto 3D (`EnsureGrass`).** owner: "añadile pasto al terreno, igual que el principal". Como el
terreno ya es permanente con árboles a mano, NO se rehace: `EnsureGrass` (llamado en REUSO y en
armado de cero) copia el pasto del terreno principal SIN tocar alturas/árboles.
**⚠️ LA CLAVE (costó MUCHO encontrarla):** el pasto salía ralo con densidad/res/prototipos/
detailObjectDensity IDÉNTICOS al principal. La diferencia era el **`detailScatterMode`**
(Coverage vs InstanceCount) — propiedad del TerrainData que NO se copia con los prototipos y que
hace que los MISMOS valores de densidad se dibujen totalmente distinto. Fix: `td.SetDetailScatterMode(
main.terrainData.detailScatterMode)`. Además: pinta TODAS las capas (mezcla corto+alto+arbustos,
no solo capa 0) y en CoverageMode copia el valor por celda tal cual (no escala por res). Si algo
de detalle/pasto/pintura NO se ve pese a estar seteado por script → chequear PRIMERO el scatter
mode. Es **idempotente**: si el terreno ya tiene detailPrototypes, no lo toca → respeta lo que el
owner borró/pintó a mano (ej: sacó el pasto de la ruta). Se arma solo la 1ra vez; para re-armar
desde cero, borrar `RoadExtensionTerrain.asset`.

**Pinos grandes + terreno PERMANENTE (editable a mano).** owner: "solo pinos, mucho más
grandes y más densos" y después "reemplazá los pinos por versiones agrandadas así los pongo a
mano; ¿se guardan mis cambios a mano?". (1) El pool ahora es SOLO pinos del terreno principal
(`PSX_Tree1`/`PSX_Tree4`, filtrados por nombre — los Conifers [BOTD] NO renderizan acá). (2)
Cada pino se agranda a un prefab standalone `BigPine_<n>` (×`PineScale`=3, en ExtraTerrains/),
y la escala por instancia del código baja a 0.9–1.5 (final ≈ mismo tamaño). Pintados a mano
(pincel tope 2×) salen grandes igual. (3) **Permanente**: si el asset ya tiene prototipos
`BigPine...`, el builder REUSA el terreno tal cual (no lo rehace) → preserva árboles pintados a
mano. Se rehace solo si tiene los pinos viejos o no existe (una vez), o borrando
`RoadExtensionTerrain.asset`. Tradeoff: al ser permanente, NO sigue si se mueve la ruta (como el
terreno principal). Densidad `TreeStep`=4.5, escala `TreeScaleMin/Max`.

Iteración: (a) faltaba `.ToArray()` en `SetTreeInstances` → error de compilación (Safe Mode),
corregido. (b) El terreno salía BLANCO: una TerrainLayer asignada pero sin pintar el alphamap no
renderiza textura → ahora se pinta el alphamap 100% a la capa de pasto. (c) Quedaba un HUECO
entre el terreno principal y éste (la ruta flotaba en el medio): ahora se busca el terreno
principal (mayor área) y se estira `minX` hasta solaparlo ~5m. El "corredor libre de árboles"
usa la central de TODO el asfalto (base + extensiones) para no plantar árboles sobre el tramo
base que ahora tapa el terreno.

---

## 2026-08-03 — Auto: no congelar su posición en el layout (quedaba desfasado)

owner movió un poco la ruta y el auto quedó **desfasado**. Causa: el auto (`Renault12`) se
posiciona 100% por código cada Generate (`CarBuilder.SnapToRoadExtensionTip` lo reubica en la
punta de la ruta), pero su transform estaba **guardado** en `layout_FullMap.json` (congelado en
la punta VIEJA, 1842). Como `ApplySavedLayout` corre ANTES del snap, el snap igual lo corregía
al regenerar, pero entre generates (o si el owner mira sin regenerar) el auto quedaba en la
punta vieja mientras la ruta se movía.

**Fix:** (1) saqué la entrada `/Renault12#0` del JSON; (2) `MapLayoutPersistence.Walk` ahora
NO guarda el transform del root del auto (nuevo `SkipOwnTransform("Renault12")`, análogo al
`SkipSubtree("DOG")`) pero SÍ guarda a los amigos sentados adentro (sus hijos, con pose a mano).
Así el auto nunca vuelve a quedar congelado: siempre lo manda el código. Verificado que el
empalme extensión↔asfalto original conecta en ~(872,-100), así que el recorrido no se rompe.
Recordatorio: **el auto se re-alinea al Regenerar** (lee la geometría viva de la ruta).

---

## 2026-08-03 — NPCs humanos: colisión + recuperar tamaño al bajar del auto

**Colisión.** Ni `FriendNpcBuilder` ni `CriminalNpcBuilder` agregaban collider → se los
atravesaba. Ahora ambos hornean un **CapsuleCollider** en la raíz del NPC (altura =
`targetHeight`, radio 0.16×altura, centro a media altura). `HumanWalkAnim` lo apaga
automáticamente mientras el personaje va **sentado** en el auto (si no, su collider estático
choca contra el Rigidbody del auto y traba/trepida el manejo) y lo prende al bajar/caminar.
Los criminales nunca se sientan → siempre sólidos.

**Tamaño al bajarse.** `OpeningDriveSequence.StandFriend` usaba `SetParent(null, true)`, que
preserva la escala MUNDIAL — el amigo heredaba la escala del auto y, si no era exactamente 1,
quedaba chico/grande ("no recuperaron su tamaño inicial"). Cambiado a `SetParent(null, false)`
+ `localScale = Vector3.one` (el `Model` conserva su propia escala de altura): el amigo vuelve
SIEMPRE a su tamaño de parado, sin importar la escala del auto. Se fija posición y rotación
(parado derecho) explícitas porque `false` no preserva el transform mundial.

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

**Mano derecha.** owner: "el auto va por la izquierda, debería ir por la derecha" (Argentina).
El spawn y los waypoints de ruta se corren `RightLaneOffset`=**8m** hacia la DERECHA de la
dirección de viaje (perpendicular por punto: `RightOf`, right = up×forward de Unity). Negativo
invierte el lado. **Nota de integración:** la constante y el helper `RightOf` llegaron de la
máquina de joaquin DEFINIDOS pero sin que nadie los llamara (mismo patrón "escrito pero sin
conectar" que pasó con `SnapToRoadExtensionTip`) -- conectados en `SnapToRoadExtensionTip`
(paso 2b): la línea central completa se corre punto por punto ANTES de calcular spawn/yaw/
waypoints; los puntos de entrada y estacionamiento de la YPF no se corren (son lugares
puntuales, no carril). Los offsets se calculan todos sobre la línea original antes de aplicar
ninguno.

---
---
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

**Quinta causa (LA IMPORTANTE, resuelta):** con lo anterior arreglado,
el auto seguía sin respetar bien la ruta (iba derecho, muy lento). Se
probó subir `steerGain` -- diagnóstico EQUIVOCADO, revertido después.
La causa real: en `MapGenerator.Generate()`, `CarBuilder.
SnapToRoadExtensionTip()` se llamaba justo después de
`ApplySavedLayout()`, pero el código que vuelve a colgar las piezas
`PavedRoad_Surface*` rescatadas por `DeleteMap()` dentro de
`root.transform` corre MÁS ABAJO todavía. En el momento en que
`SnapToRoadExtensionTip` buscaba esas piezas como hijas directas del
mapa, TODAVÍA estaban sueltas en la raíz de la escena -- no encontraba
ninguna (`pts.Count < 2`), abortaba EN SILENCIO (sin loguear nada), y
quedaba vigente la ruta de fallback de 21 puntos que arma
`CarBuilder.Build()` (`"Ruta real trazada por asfalto (raycast+material)"`,
mucho más tosca que la real). Se detectó comparando qué log aparecía en
consola: solo salía el de la ruta de 21 puntos, nunca el
`"[CarBuilder] Opening-drive reconstruido desde la escena..."` de
`SnapToRoadExtensionTip`. **Fix:** movido el llamado a
`SnapToRoadExtensionTip` a DESPUÉS del bloque que re-parentea
`PavedRoad_Surface*` de vuelta a `root.transform`. Es un script Editor
(`Assets/editor/MapGenerator/MapGenerator.cs`) -- necesita correr
Generate para tomar efecto.

**Sexta causa (resuelta):** con la ruta real ya usada (98 puntos), el
auto arrancaba en zigzag (izquierda-derecha) justo en la punta. Causa:
`SnapToRoadExtensionTip` pasa `MapLayout.PavedRoute` COMPLETO por el
transform de CADA pieza `PavedRoad_Surface*` (base + extensiones) --
para una pieza mucho más chica que la ruta completa (como la extensión),
eso genera puntos matemáticamente válidos pero MUY por fuera de su malla
real (extrapolación). Al ordenar todo por X, esos puntos extrapolados se
mezclaban con los reales de la otra pieza justo en la zona de solape
(la punta, donde arranca el auto) → zigzag. **Fix:** filtrar los puntos
de cada pieza contra su propio bounding box real (XZ, con 8m de margen)
antes de sumarlos a la lista combinada. Editor script -- necesita
Generate. **(Superado por la séptima causa, abajo -- el filtro no
filtraba nada porque cada pieza usa la malla COMPLETA de la ruta.)**

**Séptima causa (LA REAL del zigzag, resuelta):** los waypoints nuevos
estaban corridos ~10m hacia la banquina respecto del bake viejo que
funcionaba (verificado comparando los dos arrays guardados en la escena:
en X≈1868, el viejo decía z=+10.4 y el nuevo z≈−2). El auto perseguía
esa línea corrida → chocaba el borde/guardarrail → rebotaba → zigzag.
Causa de fondo: `SnapToRoadExtensionTip` reconstruía el centro de la
ruta con `MapLayout.PavedRoute` (el trazado TEÓRICO del código), pero
ese trazado cambió con la extensión del mapa de 200m y ya no coincide
con la malla real que el compañero dejó colocada en la escena. **Fix:**
la línea central ahora se lee DIRECTO de los vértices de la malla real
de cada pieza (`RoadsideBuilder.BuildPavedRoadMesh` genera 5 vértices
por sección transversal; el índice `i*5+1` es exactamente el centro),
en el orden natural del recorrido, sin usar `MapLayout.PavedRoute` ni
re-ordenar puntos globalmente por X (eso mezclaría piezas que se
solapan). Editor script -- necesita Generate.

**Octava causa (LA REAL del zigzag, confirmada con telemetría):** con
los waypoints ya verificados sobre el asfalto, el auto seguía igual. Se
agregó telemetría runtime a `CarAutoDrive` (log 2x/seg de todo el estado
del controlador + choques con nombre) y una sola corrida mostró el dato:
`resc=True` en el 100% de las líneas, incluso con el auto andando
perfecto por el centro de la ruta a 48 km/h. `IsOnAsphalt()` tiraba su
raycast desde 2m ARRIBA del auto hacia abajo con `Physics.Raycast`
simple: lo primero que tocaba era el TECHO del propio auto -- nunca
llegaba al asfalto, devolvía `false` SIEMPRE, y el auto hacía TODO el
trayecto en modo rescate (aim por `FindNearestAsphalt` + steerGain*3)
en vez de seguir sus waypoints. El zigzag del arranque era ese sistema
eligiendo puntos de asfalto laterales en la punta. Este bug estaba
desde el principio -- explica por qué `rescuing` vivió prendido toda su
historia. **Fix:** `RaycastAll` ignorando los colliders del propio auto
(mismo patrón que ya usaba `CarController.FixedUpdate`) y también los
triggers (`QueryTriggerInteraction.Ignore` -- los triggers de historia
flotan sobre la ruta y también daban falsos negativos). Script runtime
-- recompila y listo, sin Generate. **Confirmado por el owner: el auto
ya sigue la ruta correctamente. Telemetría removida.**

**ESTADO FINAL 2026-08-04: Generate funciona.** El flujo que quedó
andando: con el mapa en buen estado, `Save Map Layout` una vez (ya
hecho, `layout_FullMap.json` existe) → Generate reproduce el mapa
correctamente, con el auto spawneando en la punta de la ruta real y
manejando solo hasta la YPF. La regla vieja de "no correr Generate" ya
no aplica.

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

---

## Fix: recorte del Scene view en mapas grandes (12/8/2026)

El owner reportó que "cuando me acerco a algo se empieza a borrar" en la
vista Scene. Causa: el **Dynamic Clipping** del Scene view — con el mapa ya
enorme, Unity agranda el near-clip plane automáticamente y recorta lo
cercano. La UI para apagarlo (ícono de cámara del Scene view) queda tapada
por los botones custom "Pasar a Día / Niebla" y es difícil de encontrar en
Unity 6.

Solución en código (NO por UI): `Assets/editor/SceneViewClipFix.cs`
(`[InitializeOnLoad]`). Apaga `dynamicClip` y fija `nearClip=0.1`,
`farClip=6000` en todos los SceneView, re-aplicándose en cada load/recompile
y en `duringSceneGui` (idempotente). Es config del EDITOR: no se hornea en la
escena y NO afecta al juego (la Main Camera tiene su propio near/far).

---

## Cables de postes ahora guardables con Save Map Layout (12/8/2026)

El owner reportó que al mover postes Y cables a mano y regenerar, se
revertían. Diagnóstico:
- **Postes**: ya persistían (grupo `PostesDeLuz` + `ApplySavedToGroup`). Lo que
  se veía "revertido" era el CABLE: se genera pegado a la posición POR DEFECTO
  del poste y luego el poste salta a la posición guardada, quedando el cable
  flotando en el lugar viejo.
- **Cables**: estaban excluidos a propósito (`SkipSubtree` con `"Cable_"`), 100%
  procedurales → nunca se guardaban.

Fix:
- `MapLayoutPersistence.SkipSubtree`: se quitó `Cable_` → los cables ahora se
  guardan/aplican como todo lo demás.
- `ElectricPoleBuilder.SpawnWire`: nombres ESTABLES por tramo+lado
  (`Cable_{i}_L` / `Cable_{i}_R`) en vez de contador corrido, para que el layout
  guardado le caiga siempre al mismo cable y no se descoloque si cambia la
  cantidad (un nombre viejo que ya no existe no matchea y se ignora).

Nota de flujo para el owner: si movés un POSTE, su cable NO lo sigue solo (se
genera en la posición por defecto). Movés también el cable a mano y hacés Save
Map Layout — ahí sí persisten los dos.

---

## Postes/cables: persistencia por SUPERVIVENCIA, no por layout (12/8/2026)

El layout por índice (`ApplySavedToGroup("PostesDeLuz")`) seguía sin funcionar:
al mover el GRUPO ENTERO, su transform propio no se restauraba (ApplyWalk solo
aplica a los hijos, no al grupo), y los índices se corrían. El owner probó mover
el conjunto entero y se revertía igual.

Solución definitiva (mismo patrón que la ruta real y el terreno extra del
compañero): el grupo **"PostesDeLuz" SOBREVIVE al Generate**.
- `DeleteMap`: rescata `PostesDeLuz` (SetParent(null)) antes de destruir el mapa.
- `Generate`: si `PostesDeLuz` existe, lo re-parentea de vuelta TAL CUAL (no lo
  regenera). Solo llama a `ElectricPoleBuilder.Build` si NO existe (primera vez).
- `MapLayoutPersistence.SkipSubtree`: agrega `"PostesDeLuz"` → el layout ni lo
  guarda ni le aplica nada encima (no interfiere con lo acomodado a mano).
- `ApplySavedToGroup(...)` queda definido pero ya no se llama.

Resultado: lo que el owner mueva/borre a mano (grupo, postes o cables sueltos) es
100% a prueba de Generate, sin depender de nombres/índices. Ya NO hace falta "Save
Map Layout" para los postes. Para regenerarlos de cero: borrar el objeto
"PostesDeLuz" a mano y regenerar.

---

## Relleno de árboles lado oeste (campamento/vieja) (12/8/2026)

Owner pidió rellenar con árboles la mitad OESTE (lado humano: campamento + casa
de la vieja + campo de caza + laguna), del río (medio del mapa, x=ForestSplitX
=300) al borde oeste, con los HN nuevos (HauntedNature: HN_tree1..5, HN_bush1..6)
al azar, "que no quede muy poblado".

Como el bosque está horneado en el terreno permanente (MergedTerrain) y
ForestBuilder saltea el scatter (forestCached), no se puede hacer por builder ni
editando el .asset a mano: se hace por código de terreno.

`TreeRegionFill.cs` — menú `Tools > Folklore Archives > Rellenar Arboles (lado
campamento)`. Scatter ralo (grilla 11m, chance 0.16, 30% arbustos, min-spacing 6m)
en x<300, respetando las mismas exclusiones que ForestBuilder (agua/orilla del
lago, ríos, caminos, claros de props, lote de la vieja, estepa, línea de árboles).
Escribe TerrainData.treeInstances y guarda el terreno. IDEMPOTENTE: cachea las
posiciones que planta (regionfill_positions.bytes) y al re-correr borra ese relleno
anterior y replanta con los parámetros actuales → se puede re-tunear la densidad
sin duplicar ni tocar lo que el owner pintó a mano. Parámetros tuneables arriba del
archivo (GridStep/PlaceChance/BushFraction/MinSpacing/escalas).

Actualización: el owner confirmó que el relleno quedó bien y pidió sacar el botón.
`TreeRegionFill.cs` ELIMINADO (los árboles ya quedaron horneados en MergedTerrain,
persisten solos). Si en el futuro hay que re-rellenar/re-tunear, recrear el comando
(parámetros y exclusiones documentados arriba) o pintar a mano con Paint Trees.

---

## Toggle "Ver pasto de lejos (editor)" (12/8/2026)

Owner pidió un botón como el de árboles pero para el pasto. `GrassViewDistanceToggle.cs`
— gemelo de TreeViewDistanceToggle. Menú `Tools > Folklore Archives > Ver pasto de
lejos (editor)` (tilde). Mientras está tildado fuerza `detailObjectDistance=250` en
todos los terrenos y corre `ForestBuilder.SetGrassFadeGlobals(250)` (si no el shader
de pasto lo desvanece igual). Solo editor: al entrar a Play restaura
MapLayout.DetailRenderDistance; Enforce cada 0.5s por si Generate/día-noche lo pisan.
FarDistance ajustable arriba del archivo.

---

## Troncos del campamento → modelo PS1 real (16/8/2026)

Owner: reemplazar los 3 troncos-asiento del campamento PRINCIPAL (eran cilindros
procedurales "LogSeat", IDs 1-3 en CampsiteBuilder) por el asset descargado "Retro
PSX Style Fallen Tree Trunk" de ratoddy (itch.io — sumado a ASSET_CREDITS).

Asset copiado a `Assets/ExternalAssets/FallenTrunk/` (trunk.fbx + texture/texture.png).
Medido en Blender (desde el .blend; el FBX rompe el importer de Blender por una luz):
mesh 2.13 × 9.37 × 2.10 → eje largo Y, sección ~2.1. A ~3.4 m de largo da radio ≈0.37,
casi igual que los cilindros viejos.

CampsiteBuilder:
- Nuevo `TrunkSeat(...)`: instancia el FBX, DETECTA el eje más largo en runtime y lo
  acuesta a lo largo de +Z (base yaw=0), lo escala a targetLen, apoya la base en el
  piso centrado, y le pone MeshCollider convexo (el cilindro tenía capsule). Usa la
  textura propia del asset (CampTexMat: URP point+mate). Si falta el FBX → fallback al
  cilindro procedural.
- El FBX se exportó del .blend con CÁMARA + 2 LUCES adentro → se destruyen al instanciar
  (si no, metía cámaras/luces sueltas en el campamento).
- `Reg` ahora tiene overload `Reg(go, bake)`: los troncos van con bake:false (se ubican
  solos, no se les aplica el BakedLayout viejo de los cilindros) pero IGUAL incrementan
  el ID → no se corren los IDs de leña/carpas/mesa. Entradas 1-3 del BakedLayout quedan
  sin usar a propósito.

Para verlo: regenerar el mapa (el campamento es procedural, se rearma en cada Generate).

Fix textura (16/8/2026): el tronco se veía con manchones blancos. La textura es un
atlas RGBA (corteza/anillos opacos + musgo sobre FONDO TRANSPARENTE alfa 0 con RGB
blanco + hongos opacos); el material opaco mostraba ese fondo del musgo como blanco.
Nuevo `TrunkMat()` = URP/Lit con RECORTE ALFA (cutout, cutoff 0.5, doble cara, mate),
y fuerza el import de la textura a alphaIsTransparency + point + sin mips. Reemplaza el
CampTexMat opaco que usaba antes.

---

## Gallinero + 4 gallinas en la granja (18/8/2026)

Owner: agregar a la granja (casa de la vieja / AbandonedFarm) el chicken coop y 4
gallinas. Assets Sketchfab CC-BY (acreditar): "Chicken Coop (Free)" by wolfgar74 +
"PS1 Chicken" by honungsbi8. Ambos GLB (materiales/texturas embebidos → Unity los
importa a URP solos). Copiados a `Assets/ExternalAssets/ChickenFarm/`. Medidos en
Blender: coop 167×279×249 (alto ~249 su unidad) → escala por altura a 2.2 m; gallina
~2×2×2 → 0.38 m.

`ChickenCoopBuilder.cs`: instancia coop + 4 gallinas bajo FOLKLORE_MAP en un grupo
"ChickenCoop" (nombres únicos: Coop, Chicken_0..3), escala por altura, apoya base en
el piso, yaw. Se llama desde `HouseBuilder.BuildBarn` (rama UseAbandonedFarm), que
corre ANTES de ApplySavedLayout → quedan cubiertos por Save Map Layout (movibles/
borrables a mano, persisten al regenerar; mismo criterio que PC/silla YPF). Coop con
MeshCollider por pieza; gallinas sin collider (chicas, decorativas). Posición base
(195,170) cerca de OldLadyHouseCenter — el owner la ajusta a mano. Sumados a
ASSET_CREDITS.

Nota animación (18/8/2026): el GLB "PS1 Chicken" trae 1 clip (`Armature.001Action`,
malla skinned). Se importa clip + rig, pero las gallinas se colocan como props
ESTÁTICOS (sin Animator) — decisión del owner: dejarlas quietas por ahora. Para
animarlas MÁS ADELANTE (regenerate-safe): NO agregar el Animator a mano en la escena
(el builder re-instancia las gallinas en cada Generate y lo borra). Hacerlo en
`ChickenCoopBuilder.Place(...)`: crear/attachear un AnimatorController que loopee el
clip, con offset de tiempo por gallina (Chicken_0..3) para desincronizarlas.

---

## Chancho + caballo en la granja (18/8/2026)

Owner: sumar 2 animales más a la granja. Assets Sketchfab CC-BY (acreditar): "PS1 Pig"
by Jo_Zinn5632 + "Cavalo no estilo de PS1" by Moustache_Cat. GLB, copiados a
`Assets/ExternalAssets/ChickenFarm/` (ps1_pig.glb, cavalo_ps1.glb).

Se agregan en `ChickenCoopBuilder`: chancho + caballo como hermanos sueltos bajo
FOLKLORE_MAP (nombres únicos "Pig"/"Horse") → cubiertos por Save Map Layout igual que
las gallinas. Escala por altura (Pig 0.85 m, Horse 1.6 m).

OJO orientación del CABALLO: el GLB viene ACOSTADO de costado. Verificado parseando el
glTF (bounds en espacio Unity Y-up): Pig X=2.19 Y=3.34 Z=6.51 (parado OK); Horse
X=1.71 Y=0.63 Z=2.09 (su alto real 1.71 está en X, no en Y). Se agregó `extraEuler` a
`Place(...)` que endereza ANTES de medir/escalar; el caballo usa `HorseRollDeg=-90`
(roll sobre Z). Si sale PATAS ARRIBA, cambiar a +90 (constante arriba del archivo).

Animaciones: pig trae 1 clip, horse 6 clips (ambos skinned). Se colocan ESTÁTICOS
(sin Animator), igual criterio que las gallinas. Colliders: coop sí (MeshFilter);
animales (skinned, sin MeshFilter) NO por ahora — decorativos.

---

## Animaciones de los animales de la granja (18/8/2026)

Owner: animar los animales (dijo "como los pollos" — aclaración: las gallinas estaban
ESTÁTICAS, así que se animan TODOS ahora: 4 gallinas + chancho + caballo).

Enfoque regenerate-safe (horneado en el builder):
- Nuevo runtime `Assets/Scripts/LoopClipAnim.cs` (namespace FolkloreArchives): reproduce
  un AnimationClip LEGACY en loop vía componente Animation, con `startOffset` para
  desincronizar copias. Corre en Play/build (no en el Scene view en modo edición).
- `ChickenCoopBuilder.MakeLegacyClip(glb, cache)`: saca el clip del GLB (prefiere "idle"
  si hay; chicken/pig traen 1, horse trae 6), hace una COPIA legacy+loop como asset en
  Generated/anim_*.anim.
- `AttachAnim(inst, clip, offset)`: saca el Animator que trae el GLB (choca con Animation
  legacy) y agrega LoopClipAnim en la raíz del modelo. Gallinas con offset i*0.5s
  (desincronizadas); chancho/caballo offset 0 (uno solo cada uno).

Notas:
- Se ven quietos en el Scene view (modo edición); ANIMAN al dar Play / en el build.
- Caballo: agarra el clip "idle" o el primero de los 6; si queda raro (galopando en el
  lugar) se cambia el criterio de MakeLegacyClip.
- Siguen sin collider (skinned, decorativos) y sin marcar static (animan).

Corrección (18/8/2026): el CABALLO desaparecía al dar Play con su animación legacy (su
clip mueve la raíz/huesos → se va de cuadro / rompe skinning). Se dejó ESTÁTICO (sin
AttachAnim). Gallinas + chancho siguen animados. Para animar el caballo bien en el
futuro haría falta un Animator + controller propio (no legacy).

---

## Corral de gallinas (reja sin fierros + madera) (20/8/2026)

Owner: hacer el corral de las gallinas en la granja — tomar la reja del asset chain-link
PERO sin los fierros/postes de adelante (dejar solo el tejido) y rodearla con madera.

`CorralBuilder.cs`:
- `NettingMesh()`: toma la malla de chain_link_fence_01.fbx (1 mesh, 2 submeshes) y arma
  una versión SOLO-TEJIDO copiando el/los submesh(es) que NO son de acero (material
  "steel/galv" = los fierros del frente). Guarda Generated/mesh_chainlink_netting.asset.
  Material del tejido = reusa Assets/Settings/ChainLinkFence_Chain.mat (cutout doble cara)
  o lo arma inline.
- `Build()`: corral rectangular (Center 195,169; 9×8 m; paneles de 2 m) con POSTES y
  TRAVESAÑOS de madera (Cube + material corral_wood marrón) y el tejido entre postes.
  Entrada (gate) = panel del medio del lado sur salteado. Colliders: postes/travesaños
  (Cube nativo) + BoxCollider fino en cada panel de tejido.
- Grupo "CorralGallinas" bajo FOLKLORE_MAP, creado desde HouseBuilder.BuildBarn (antes de
  ApplySavedLayout) → guardable/movible con Save Map Layout. Nombres únicos (Poste_/
  Reja_/Travesano_ + índice global).

No es asset nuevo (reusa el chain-link de DanglingBat, ya en ASSET_CREDITS). Parámetros
(tamaño/alto/posición) tuneables arriba del archivo.

Textura madera corral (20/8/2026): postes+travesaños del corral usan Wood_04.jpg (pino claro; owner pidió más clara)
del pack AbandonedFarm (misma madera que el galpón → combina), vía MatTextured, en vez
del marrón plano. Fallback a color si falta la textura.

Techo del corral (20/8/2026): CorralBuilder.BuildRoof — vigas de madera a lo largo de X
(una por línea de postes en Z, a la altura PostH) + tejido HORIZONTAL tileado (~2m,
acostado con rot -90° en X, recentrado por bounds) cubriendo toda la planta. Nombres
TechoViga_/TechoReja_.

---

## Muebles PSX en la casa de la vieja (20/8/2026)

Owner: agregar el "PSX Furniture Pack" (Akneeee, itch.io name-your-price) a la casa de
la vieja — es el "pack de muebles viejos" que motivó desactivar los muebles nappin.
GLB único (furniture.glb, texturas embebidas) con todas las piezas ya dispuestas como
habitación. Copiado a Assets/ExternalAssets/PSXFurniture/.

Medido en Blender: viene ~2.3× grande (silla 2.1 m, ropero 4.6 m) → escala 0.43 =
tamaño real. `PsxFurnitureBuilder.Build(mapRoot, houseBounds)`: instancia el GLB entero
bajo FOLKLORE_MAP (NO bajo la casa, para no heredar AlpHouseScale), escala 0.43, centra
en la planta de la casa (hb.center XZ) y apoya en el piso (hb.min.y). Colliders
MeshCollider por pieza. Nombre único "MueblesVieja" → guardable con Save Map Layout
(grupo o pieza individual). Se llama desde HouseBuilder.BuildAlpHouse (antes de
ApplySavedLayout). Sumado a ASSET_CREDITS.

---

## Muebles abandonados (PSX Derelict) en la casa de la vieja (20/8/2026)

Owner: agregar "PSX Derelict Furniture" (Daniel Jurys, CC0) a la casa de la vieja pero
APARTE del PSX Furniture Pack ya puesto. 6 GLB sueltos (chair, couch, fridge, mattress,
shelf, vase), ya a tamaño REAL (escala 1) y SIN animación. Copiados a
Assets/ExternalAssets/PSXDerelict/.

`PsxDerelictFurnitureBuilder.Build(mapRoot, hb)`: grupo aparte "MueblesViejaDerelict"
bajo FOLKLORE_MAP; cada pieza en una fracción del footprint de la casa (agrupadas en el
FONDO, fz 0.73-0.86, repartidas en X) → set separado del otro (que quedó centrado).
Base al piso (hb.min.y), colliders MeshCollider, nombres únicos → Save Map Layout. Se
llama desde HouseBuilder.BuildAlpHouse tras PsxFurnitureBuilder. CC0 → sin obligación de
acreditar (igual anotado en ASSET_CREDITS).

Cama doble (20/8/2026): "Low Poly PSX Style Double Bed" (Icevanilla, Sketchfab, CC-BY).
GLB a tamaño real (2.06×1.54×2.56 m), sin animación. Copiado a PSXDerelict/double_bed.glb
y agregado a PsxDerelictFurnitureBuilder.Items en un rincón dormitorio (frente-izq,
fx0.22/fz0.28, yaw90) → aparte del cluster del fondo. Sumado a ASSET_CREDITS (CC-BY).

Fix brillo cama v2 (20/8/2026): el fix por propiedades no pegó (material no URP o con emisión). Ahora
brillaba vs el resto. PsxDerelictFurnitureBuilder.MakeMatte() deja los materiales MATE
(smoothness/metallic 0, specular + reflejos OFF) sobre COPIAS del material (no toca el
asset importado). Se aplica a TODAS las piezas del set derelict para que queden parejas.

Fix brillo cama v2: seguía brillando tras regenerar (material importado no era URP/Lit
o tenía emisión → ignoraba la luz). MakeMatte ahora REARMA cada material como URP/Lit
mate tomando la textura base, y apaga specular/reflejos + EMISIÓN (EmissiveIsBlack).

Hogar/chimenea (20/8/2026): "Fireplace Low-poly" (MaX3Dd, Sketchfab, CC-BY). GLB chico
(0.67×0.52×0.26 m) → se ESCALA por altura a 1.3 m. Copiado a PSXDerelict/fireplace.glb,
agregado a PsxDerelictFurnitureBuilder.Items (pared derecha, fx0.94/fz0.55, yaw-90,
targetH1.3). Se agregó campo targetH a Items (>0 = escalar por altura; 0 = tamaño real).
MakeMatte aplica igual (sin brillo). Sumado a ASSET_CREDITS (CC-BY).

Mesa PS1 duplicada en casa vieja (22/8/2026): owner pidió duplicar la mesa del campamento
criminal (PS1_Table) en la casa de la vieja. PsxDerelictFurnitureBuilder.PlacePs1Table():
instancia HouseFurniture_PS1/PS1_Table.fbx con el mismo material (stove_atlas mate, igual
que CriminalCampBuilder.PS1KitMat), escala ~0.75 m, en el centro de la casa, nombre único
"PS1_Table" en el grupo MueblesViejaDerelict → Save Map Layout. Reusa asset existente (sin
descarga nueva).

Sillas PS1 + comedor (22/8/2026): owner pidió copiar también la silla del campamento criminal.
Refactor: PlacePs1Table → PlacePs1Kit(fbxName, uniqueName, fx, fz, yaw, targetH) genérico. En la
casa de la vieja ahora arma un COMEDOR: PS1_Table (centro) + 4 PS1_Chair alrededor (sur/norte/
oeste/este, mirando la mesa), mismo material stove_atlas mate, nombres únicos → Save Map Layout.

Bonfire (22/8/2026): "Bonfire Lowpoly" (Christian Gentry, Sketchfab, CC-BY). GLB tamaño real
(1.81×0.87×1.81 m), sin animación ni emisión (llamas pintadas en textura). Copiado a
PSXDerelict/bonfire.glb, agregado a PsxDerelictFurnitureBuilder.Items con matte=FALSE (se
conserva su material original, no se le fuerza el mate como al resto). Se agregó campo bool
'matte' a Items. Nota: no ilumina de noche (no tiene emisión/luz); si se quiere que brille se
puede sumar un Point Light + emisivo aparte. Sumado a ASSET_CREDITS (CC-BY).

Bonfire solo madera (22/8/2026): owner pidió sacar el anillo de piedras y dejar solo la
madera. La malla es una sola (1 material) → PsxDerelictFurnitureBuilder.StripBonfireStones()
filtra por GEOMETRÍA en espacio mundo: descarta los triángulos con radio horizontal > 80% del
máximo (las piedras del borde; hay un hueco claro en r 0.68-0.76 sobre 0.907). Crea malla nueva
"bonfire_wood" (conserva verts/uv/colores) y la asigna; el collider usa esa malla filtrada.

Fuego + luz bonfire (22/8/2026): owner pidió partículas de fuego e iluminación en la hoguera.
CampsiteBuilder.AddFireParticles se hizo public (reuso del sistema de fuego PS1). Nuevo
PsxDerelictFurnitureBuilder.AddBonfireFire(): agrega Point Light cálida (color 1,0.55,0.2;
intensity 2.6; range 12; sin sombras) + partículas de fuego, colgadas del GRUPO (escala 1, para
que el tamaño de partícula no dependa de la escala del modelo) posicionadas en el top real de la
madera (PropBounds mundo). Nombres BonfireLight/FireParticles → Save Map Layout.

Fuego bonfire v2 (22/8/2026): owner "no se ven partículas" (estaba en el menú co-op/spawn,
lejos de la casa; los objetos BonfireLight/FireParticles SÍ existían en la jerarquía).
AddFireParticles ahora devuelve el GameObject. AddBonfireFire agranda el fuego del bonfire
(startSize 0.5-1.15, lifetime 0.55-1.1, rate 32, shape radius 0.45), lo sube sobre los troncos
(0.55*alto) y sube la luz (intensity 3.2, range 14). Se ve en Scene view (focus FireParticles) y
en Play estando cerca del bonfire.

Baño en casa vieja (22/8/2026): 3 GLB (bathroom_set 17 mallas 0.88x1.15x0.79; dirty_sink
0.68x0.80x1.0; psx_toilet GIGANTE 1.76x4.13x2.62 → targetH 0.75). Copiados a PSXDerelict/
(bathroom_set.glb/dirty_sink.glb/psx_toilet.glb), agregados a PsxDerelictFurnitureBuilder.Items
en rincón frente-derecha (fz 0.15-0.30), matte=true. OJO: el owner NO pasó los links de descarga
→ ASSET_CREDITS con ⚠ (pedir fuente/autor/licencia).

Fuego bonfire v3 (22/8/2026): owner sigue sin ver fuego en Play (estaba en el LOBBY co-op
"elegí personaje", donde la simulación puede estar pausada → partículas no emiten). Se subió
la luz (intensity 5, range 18) y se puso prewarm=true + playOnAwake + ps.Play() para que el
fuego arranque YA encendido. Test definitivo: Scene view, doble-clic en FireParticles (frame) →
se ven las llamas y dónde está. En Play hay que estar cerca del bonfire y con la partida ya
iniciada (no en el lobby).

Fuego bonfire v4 - FIX REAL (22/8/2026): el fuego NO se veía porque el owner movió el bonfire al
hogar y lo escaló a ~0.0055 vía Save Map Layout, pero el fuego/luz colgaban del GRUPO en la
pos/escala por defecto → al aplicar el layout el bonfire se mudaba/achicaba y el fuego quedaba
huérfano (lejos y gigante). Fix: AddBonfireFire ahora cuelga BonfireLight + FireParticles del
BONFIRE (siguen su move+escala del layout) y usa main.scalingMode = Shape para que el TAMAÑO de
partícula sea inmune a la escala chiquísima del bonfire (velocidad/tamaño ignoran la escala; solo
el shape de emisión se achica → emisión casi puntual, ok). La luz (range mundo) no depende de escala.
La fogata del campamento se veía porque su fuego está en un grupo a escala 1.

Farm props (22/8/2026): "PSXProp - Farm Props" (Wardster, Sketchfab, CC-BY). 1 GLB combinado
(pozo/well, barriles, rueda de afilar, carrete de cable, farm_prop_P1) + un PLANO gigante de
display (55 m) que se descarta. Copiado a ChickenFarm/farm_props.glb. ChickenCoopBuilder.
PlaceFarmProps(): instancia el GLB entero como cluster "FarmProps" en el patio de la granja
(FarmPropsSpot 204,179), borra los hijos "Plano"/"Materiais"/"decalMoss", apoya en el piso,
MakeMatte (público ahora, reusado de PsxDerelictFurnitureBuilder), colliders. Nombre único →
Save Map Layout (el owner reparte las piezas). Props a tamaño real (escala 1). CC-BY → acreditar
a Wardster (en ASSET_CREDITS).

## 2026-08-26 — HANDOFF: cinemática campamento (noche→día) + misión del RANCHO DE LA VIEJA (cañas)

Contexto para retomar en cualquier sesión nueva. Todo esto vive en `Assets/Scripts/CampsiteSequence.cs`
(director coroutine que corre desde el auto → campamento → noche → perro/Luz Mala → despertar →
mañana → rancho) + botones de editor en el menú **Folklore ▸ …**. Referenciado por OBJETO
(no coords hardcodeadas): `letrina.007`, `RanchoViejo`, `OldLady_Storyteller`, `TranqueraCorral`,
`Ovejas`. Coords sí-hardcodeadas en campos públicos de CampsiteSequence: `houseDoorPos`,
`corralGateStand`, `sheepPasturePos`, `playerSitPos`, `nightCamPos`, `luzMalaPos`, `dogPoopPos`,
`dogBarkPos`.

CINEMÁTICA CAMPAMENTO (ya andaba, se sumó):
- Perro (Rufus) al lado del jugador en el tronco de la fogata: se ACHICA (`dogFireScaleMul=0.4`,
  escala el hijo "Model"); se restaura al despertarse de noche.
- Despertar nuevo día (`WakeNewDay`): tras el reto por ladrar, Rufus MANTIENE su cámara hasta
  volver a la carpa; el humano se para al lado de la carpa y lo reta; al llegar ambos entran y se
  acuestan; AMANECE lento con el mismo plano cenital del campamento (`MakeNightCam`) + parpadeo
  negro (cámara mirando el techo de la carpa) + aparecen afuera → control libre.
- Mañana (`MorningAfterWake`): limpiás la caca de Rufus con E; hablás con el malecasual (parado
  fuera de su carpa; chica y negro en los troncos) sobre pescar; faltan las cañas (revisás el
  auto); deciden ir a un rancho a pedir prestado; caminás LIBRE al rancho.

MISIÓN RANCHO (nuevo, `RanchoBathroomScene`):
- Tocás la puerta de la casa (`houseDoorPos` 136.13,125.44) → no atiende → al granero.
- Tocás la LETRINA (`letrina.007`) → se ACTIVA `RanchoViejo` (susto: flash negro + ladrido) →
  "¡propiedad privada!" → pedís caña → "pregúntenle a mi mujer, ya la despierto" → el viejo camina
  a `OldLady_Storyteller`, ella viene al jugador → "mucho gusto…" → presta las cañas a cambio de
  SACAR LAS OVEJAS.
- Hint "Abrí la tranquera" → abrís `TranqueraCorral` con E → las 4 `Ovejas` caminan solas al
  pastizal (`sheepPasturePos` 124.07,167.37).

BOTONES DE EDITOR (menú Folklore):
- `LetrinaFixer.cs` → "Reponer letrina (fresca con texturas)": la letrina de la escena es un
  Combined Mesh (static batch) que se rompe al moverla; esto instancia una copia limpia del
  AbandonedFarm.fbx en el mismo transform. (Ya usado; la letrina quedó en piezas letrina.00x.)
- `RanchoNpcSetup.cs` →
  · "Poner viejo del rancho en la letrina": construye `RanchoViejo` del `OldManNPC/Character_32.fbx`
    (pack Characters PSX, rig Mixamo) con material URP + `HumanWalkAnim` + MixamoLimbs, DESACTIVADO
    en la puerta de la letrina; de paso agrega HumanWalkAnim a `OldLady_Storyteller` (EnsureMobility).
  · "Armar tranquera del corral (abrible)": desde el `Cube.184` seleccionado (combined mesh) arma
    una réplica-plank con bisagra en un extremo (eje Y) + `CorralGate` (E abre/cierra) y desactiva
    el original. `openDeg=95` (poné -95 si abre para el lado equivocado).
  · "Poner ovejas en el corral": 4 ovejas (`Sheep/sheep.obj`) en (108.9,154.4) con material
    URP/Point; `SheepHeight=1.6`.
- Componente nuevo: `Assets/Scripts/CorralGate.cs` (bisagra vertical + E + InteractHint + sonido).

ASSETS (en `Assets/ExternalAssets/`, GITIGNORED — no se versionan):
- `OldManNPC/Character_32.fbx` + `Character_32.png`: viejo canoso (sweater/jeans), extraído de
  `Downloads/Characters_psx.rar` (pack "Characters PSX" de Elbolilloduro, CC0 — mismo de la vieja).
- `Sheep/sheep.obj` + `sheep.mtl` + `sheep_tex.jpg`: oveja, extraída de `Downloads/9143.zip`
  (OBJ estilo Sketchfab). ⚠ ASSET_CREDITS: falta fuente/autor/licencia de la oveja — pedir al owner.

LAYOUT: 4 clones de chancho `Pig (1..4)` (copiados por un amigo, estaban en el corral de las
ovejas) marcados `deleted:true` en `layout_FullMap.json` → `MapLayoutPersistence` NO los recrea al
regenerar (línea 295 solo clona si `deleted=false`; 267 "ocultar = borrar"). El original `/Pig#0`
del gallinero queda intacto.

PENDIENTE (próximos pasos, FALTAN coords TEST_PLAYER del owner):
1. Caja de herramientas ARRIBA del granero + SCREAMER del pollo (`ChickenFarm/ps1_chicken.glb`):
   subir al granero → salta el pollo (susto) → agarrás la caja → bajás → se la das a la vieja.
2. Arreglar la CADENA del baño (minijuego simple). Variante 2 jugadores: el perro caza conejos que
   comen los cultivos mientras el humano arregla el baño (DIFERIDO a 2 players, como el sueño de
   las ovejas saltando la valla).
3. MATES adentro con la vieja → cuenta la HISTORIA de la Luz Mala → vuelven al campamento.

RECORDATORIOS: la escena `.unity` NO está en git (cambios de escena = Ctrl+S en Unity + Tools ▸
Folklore Archives ▸ Save Map Layout). Commit+push tras cada cambio de código. `Assets/Resources/` y
`ExternalAssets/` gitignored. No regenerar el mapa hand-editeado. Solo links de assets REALES.
