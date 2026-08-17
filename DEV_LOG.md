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
