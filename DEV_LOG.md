# DEV LOG — LA LUZ MALA map generator

Running log of AI-assisted changes to the greybox map generator, kept in this
folder so any AI session (Claude, etc.) working on this project can catch up
on recent context without re-deriving it from scratch. Newest entries on top.
See `MAP_README.md` for the static architecture reference.

---

## 2026-07-24 — NOTA DE DISEÑO: secuencia del cementerio → puente → bote → mirador

Anotado tal cual lo contó el owner (todavía sin implementar, sin código nuevo).
Encadena varias piezas que ya están armadas en el mapa: Cementerio (`AreaPoiBuilder.
CemeteryArea`), el puente (`BridgeBuilder`, cruce en x≈315-375), el muelle/bote
(`AreaPoiBuilder.LakeShoreDock`, "BoteVarado") y el Mirador (`AreaPoiBuilder.
BridgeLookout`, torre pasando el puente lado sur).

**Secuencia (borrador):**
1. Después de ir al río a pescar, Rufus (perro) y el humano van al Cementerio y
   desentierran "lo de la luz mala" (el objeto/relato que despierta a La Luz Mala).
2. Escuchan gritos, miran hacia el puente.
3. Ven a los asesinos llevándose secuestrados a los amigos y el auto, cruzando el
   puente.
4. Rufus + humano empiezan a correr HACIA EL CAMPAMENTO mientras los persigue La
   Luz Mala.
5. Se suben al bote porque el puente se cae por el peso del auto.
6. La Luz Mala se frena (no puede cruzar el agua/el punto donde se cae el puente).
7. Siguen en el bote hasta llegar al otro lado.
8. Suben a la Torre (el Mirador) y ven que se están llevando a los amigos al
   campamento de los asesinos.

**PENDIENTE (lo que el owner quiere resolver ahora):** qué pasa DESPUÉS de llegar a
la Torre y ver el secuestro -- no está decidido todavía, es la próxima parte a
diseñar.

**Notas técnicas para cuando se implemente:** el puente que "se cae" es un evento
nuevo (`BridgeBuilder` hoy es estático, sin física de colapso); la persecución de
La Luz Mala necesita lógica de detección de límite (se frena en el agua, no sigue
al bote); el bote (`BoteVarado`, hoy decorativo/estático en `LakeShoreDock`) pasaría
a necesitar ser un objeto usable/animado. Nada de esto está armado todavía.

---

## 2026-07-21 — Borrado de árboles persistente (integrado a Save Terrain Paint)

`TreePersistence.cs`: hace que el borrado manual de árboles (pincel Paint Trees +
Shift) sobreviva al Generate. Los árboles son `TreeInstances` con posición
normalizada y el bosque es determinístico → se guarda un **diff de posiciones
removidas** (no se congela el bosque; procedural sigue mandando en el resto).
- `ForestBuilder.Build` (tras `SetTreeInstances`): `CaptureBaseline(td)` (set
  procedural completo → `Generated/tree_baseline.bytes`) + `ApplyTreeRemovals(td)`
  (dropea lo borrado).
- `TerrainPaintPersistence.SaveTerrainPaint`: ahora también llama
  `TreePersistence.SaveTreeRemovals(live)` → `tree_removals.bytes` = baseline ∖ vivo.
  `ClearTerrainPaint` también borra las remociones.
- ⚠ Flujo: **Generar una vez** (captura baseline) → borrar árboles con el pincel →
  **Save Terrain Paint** → regenerar. Si guardás sin baseline, avisa "regenerá primero".

---

## 2026-07-21 — Granja abandonada (asset PSX de mcpato) reemplaza el galpón

- Asset: `Aband1.1.fbx` (mcpato, itch.io, "Abandoned Farm PSX") → importado como
  `Assets/ExternalAssets/AbandonedFarm/AbandonedFarm.fbx`. Es UNA escena horneada
  (478 objetos, texturas EMBEBIDAS). Nombres genéricos (Cube.NNN) → no modular.
- `AbandonedFarmBuilder.cs`: instancia el FBX entero en la granja (grupo
  `AbandonedFarm > FarmModel`), **desactiva el terreno propio del diorama** por
  prefijo de nombre (rios/globaltrees/cespe/tree/ground/piso/agua…), convierte
  materiales built-in→URP (anti-magenta), y aplica un **transform PERSISTENTE**.
- **Persistencia + tool**: `Tools ▸ Folklore Archives ▸ Guardar Transform de la Granja`
  → guarda pos/rot/escala del grupo `AbandonedFarm` en
  `Assets/_FolkloreArchives/farm_transform.txt`; el builder lo relee en cada Generate.
  (1ª versión = transform del GRUPO entero; per-objeto se puede extender si hace falta.)
- Wire: `HouseBuilder.BuildBarn` → `if (UseAbandonedFarm) AbandonedFarmBuilder.Build(...)`.
  **`UseAbandonedFarm=false` vuelve al galpón BarnShed viejo** (código intacto). Backup
  del estado previo en Plastic cs:75.
- ⚠ Colocación inicial a OJO (escala/rot desconocidas del FBX) → default en
  OldLadyHouseCenter escala 1; el owner mueve+guarda. Si queda un piso del asset sin
  desactivar, sumar su nombre a `TerrainPrefixes`. La casa ALP sigue puesta (ver si pisa).

---

## 2026-07-20 — Casa de la vieja → GRANJA + fix spawn enterrado

- **Spawn enterrado:** el jugador usaba altura FIJA (`RoadSurfaceHeight`) mientras perro/auto
  muestrean el terreno. Fix en `TestPlayerBuilder` (muestrea `SampleHeight`) + botón
  *Reubicar Spawn sobre el terreno* + **snap al suelo por raycast en `MapExplorer.Start()`**
  (robusto: se apoya solo en cada Play, sin importar la posición guardada).
- **Granja (PERMANENTE, horneado — cs:66/67/68):** `HouseBuilder.BuildBarn` ahora
  instancia el **galpón REAL** (`Assets/ExternalAssets/BarnShed/source/ruined_house_4.glb`,
  el mismo modelo que usaba la Estancia) al lado de la casa (`OldLadyBarnCenter`),
  escalado a ~13 m y apoyado en el piso; si falta el modelo, cae al galpón procedural
  de madera de antes. Se construye en cada Generate → **regenerate-safe** (ya no se
  pierde al regenerar). Constantes: `BarnModelDir`, `BarnTargetSize=13`, `BarnYaw=90`.
- **Estancia DESACTIVADA** (`AreaPoiBuilder.Estancia` → grupo vacío): sacaba el "casco"
  (`country house01/Models/House.fbx`, salía MAGENTA por shader built-in) + un
  `GalponModelo` que DUPLICABA el galpón. Se deja el grupo vacío y registrado para NO
  correr los índices de persistencia de los demás POIs.
- Borrado `OldLadyFarmTools.cs` (el menú *Mudar Galpón…* manual) — quedó obsoleto y era
  peligroso post-regenerado (borraba el grupo `OldLadyBarn` que ahora tiene el galpón).
- ⚠ La escena venía con POSICIONES DEL CÓDIGO (casa ~185,178) — hubo un regenerado; ya
  NO está en 404,625. Ajustar `BarnYaw`/`OldLadyBarnCenter` si el galpón queda mal.

---

## 2026-07-20 — Recuperación del galpón + guardado + amueblado (pack All.fbx)

Joaquín hizo **Undo en Plastic** y volvió a aparecer `OldLadyBarn` (el galpón que se
había perdido) junto a `OldLadyHouse_ALP` (instancia de `House_Prefab`, con `Room0X`).
- **Guardado (a pedido "guardá todo"):** check-in de todo lo pendiente →
  `cs:53` escena (galpón+casa) + settings, `cs:54` packs de muebles
  (`Assets/ExternalAssets/FurniturePacks/All` + `PSX`) + `HouseFurnisher.cs` +
  paquetes URP/HDRP, `cs:55` metas, `cs:56` mejora del amueblado. Único item dejado
  FUERA de VC a propósito: `Assets/Settings/PSX_Character.mat` (filtro VHS descartado).
- **Amueblado** (`HouseFurnisher.cs`, menú *Tools ▸ Folklore Archives ▸ Amueblar Casa
  de la Vieja*): reescrito con **28 piezas verificadas** contra los 686 nodos de
  `All.fbx`, y colocación **relativa a los bounds del ambiente** (fx/fz ∈ [-1,1] ×
  medio-ancho × 0.82) para no clavar contra paredes. 5 sets → 5 `Room0X`
  (dormitorio / living / cocina-comedor / dormitorio 2 / baño). NO corre en Generate:
  es un botón → se corre a mano, así **no regenera** y el galpón manual queda intacto.
- ⚠ El galpón `OldLadyBarn` está puesto A MANO bajo `FOLKLORE_MAP` → **Generate lo
  borra** (DeleteMap). Por ahora: NO regenerar. Pendiente: hornearlo regenerate-safe
  en `HouseBuilder` (ahora sí el asset del galpón existe en el proyecto, recuperado).
- ⚠ 1er pase de muebles: orientaciones/posiciones a ojo → ajustar con captura en modo Día.

---

## 2026-07-13 — Campamento ladrones ×1.6: fix altura de ranchos + árboles en el medio

(Continuación del CriminalCampBuilder.) Al escalar el camp ×1.6:
- `Shack` tenía H/T/ridgeY/doorH HARDCODEADOS → los ranchos se hacían más anchos pero NO
  más altos ("quedó igual"). Ahora `Shack` recibe `sc` y escala TODAS las medidas.
- Árboles/arbustos se excluían solo 12m de `MainCriminalCamp` → quedaban en el medio del
  camp agrandado. Subido a **24m** en `ScatterTrees` y `ScatterBushes` (pasto ya en 26m).
  ⚠️ Bosque cacheado → **Rebuild Forest (forzar)** + Generate.

---

## 2026-07-11 — Casa: muebles Kenney (color plano) → pack nappin texturizado

El owner quiere que la casa de la vieja se vea más creíble. Los muebles Kenney son
de color plano; el pack **House Interior Pack (nappin.dev)** — 57 modelos lowpoly
texturizados con paleta de gradientes — da un interior mucho más cohesivo.

- **Integración** (`HouseBuilder`): nuevo prefijo `NAP_` en `FurnitureItems`. En
  `PlaceFurniture`, si el modelo empieza con `NAP_`, carga el prefab de
  `Assets/nappin/HouseInteriorPack/Prefabs/(Prb)<Nombre>.prefab`.
- **Materiales**: los prefabs de nappin usan shader **built-in (Standard)** → en URP
  saldrían magenta. `NappinUrp(src)` convierte cada material a URP/Lit copiando la
  textura del gradiente (`_MainTex`→`_BaseMap`), color y emisión (para las luces
  `EmissiveWarm`). Cacheado por material fuente.
- **Mapeo** (mismo orden/cantidad/posiciones → IDs de persistencia estables): cama,
  mesas de luz, ropero, cómoda, sofá, sillas, mesa ratona, estante, lámpara, consola
  TV, bacha/cocina/campana/heladera, inodoro, lavabo, espejo, perchero, etc. Sin
  equivalente nappin (siguen Kenney/PS1): alfombra, radio, TV vintage, bañera, banco,
  y la cocina PS1 (mesada/alacenas/mesa/sillas).
- **Crédito**: House Interior Pack por **nappin** (nappin.dev).
- ⚠ 1er pase: rotaciones/posiciones pueden necesitar ajuste (el "facing" nativo de
  nappin difiere del de Kenney) → revisar en captura de DÍA y afinar.

## 2026-07-10 — Fogata: tamaño del modelo fijado a mano (escala 150)

La persistencia del campamento guarda la transform del GRUPO `Campfire` (id 0), pero
el owner escala el MODELO interno `Campfire_Default` (hijo del grupo, que es lo que se
selecciona al clickear) → su tamaño no se guardaba. Como pidió, se fijó en código:
`PS1Prop`/`SeatProp` ahora aceptan `fixedScale` opcional, y `FirePit` coloca el modelo
de la fogata con `fixedScale = (150,150,150)` (el valor que eligió) en vez de escalar
por altura. Si quiere otro tamaño, cambiar ese número. **Necesita regenerar.**

---

## 2026-07-10 — FIX REAL del pasto en la fogata (el BeachPath lo metía, no el radio)

El pasto seguía atravesando la fogata aunque el claro era 11m centrado justo en ella.
Causa real: en `ForestBuilder.SetupGrass`, el bloque del **BeachPath** (sendero
campamento→playa, que ARRANCA en `MapLayout.Campsite`) ponía "pasto corto" con
`continue` **ANTES** del claro del campamento → esas briznas nunca pasaban por el
chequeo de radio (por eso agrandar el radio no hacía nada). Fix: mover el `continue`
del claro del campamento ARRIBA del bloque del BeachPath. Ahora la fogata queda pelada.
(`SetupProceduralGrass` no tenía BeachPath, ya estaba ok.) El radio quedó en 11.
**Necesita regenerar.**

---

## 2026-07-10 — Fogata: partículas de fuego PS1 + más claro de pasto

- **Claro de pasto** del campamento `CampsiteClearRadius 9 → 11`. Los dos sistemas de
  pasto ya excluían 9m centrado en `Campsite` (fogata al centro, verificado en el
  layout: id 0 en 0,0,0), pero el pasto es billboard de 4-7m de alto → el rooteado
  justo en el borde de 9m "se asoma" sobre la fogata. 11m lo empuja lejos.
- **Partículas de fuego PS1** (`CampsiteBuilder.AddFireParticles`, hijo del grupo
  `Campfire`): `ParticleSystem` billboard, cono hacia arriba, `colorOverLifetime`
  amarillo→naranja→rojo con fade, `sizeOverLifetime` que se achica al subir, ~22/s,
  40 máx. Material `mat_camp_fireparticle` (URP Particles/Unlit, transparente +
  aditivo) con textura `tex_camp_fireparticle` 32² radial naranja→alpha cuantizado
  (crunch PS1, filtro Point). Se mantienen la brasa emisiva + la luz puntual como
  glow estático.
- ⚠ **Los ParticleSystem NO se animan en el Scene view en modo edición** (sólo si
  seleccionás el objeto o en Play/Game view). En edición se ve la brasa/luz; el fuego
  animado se ve dándole Play. No afecta la persistencia (las partículas son hijas del
  grupo Campfire = id 0, no un objeto registrado aparte; PersistCount sigue 9).
- **Necesita regenerar.**

---

## 2026-07-10 — Persistencia de ediciones del campamento (como la de muebles)

El owner acomodó el campamento a mano (movió/escaló troncos, carpas, etc.) y quería
que no se pierda al regenerar. Nuevo `CampsitePersistence.cs` (mismo patrón que
`FurniturePersistence`):
- **IDs estables por objeto de dressing:** `CampsiteBuilder.Build` registra 9 objetos
  con `Reg(...)` en orden fijo → nombre `Camp_##_...` (0 fogata, 1-3 troncos, 4 leña,
  5-7 carpas, 8 mesa). Los builders (`FirePit/HLog/Firewood/PS1Tent/PicnicTable`)
  ahora devuelven su GameObject. Const `PersistCount = 9` + `PersistNames[]`.
- **Menú `Tools > Folklore Archives > Save Campsite Layout`:** guarda pos/rot/escala
  LOCAL (relativa al grupo Campsite) de cada `Camp_##` + marca borrados, a
  `Assets/_FolkloreArchives/campsite_layout.json`. `Clear Campsite Layout` para volver
  a código. `Build` llama `Begin()` (carga) y `Register()` aplica el override o borra.
- **Migración de escena vieja:** los objetos del campamento actual todavía NO tienen el
  prefijo `Camp_##` (recién se ponen al regenerar). Para no perder las ediciones YA
  hechas, `SaveCampsiteLayout` tiene un camino B: si no hay ningún `Camp_`, matchea los
  hijos por nombre base EN ORDEN contra `PersistNames` (asume todos presentes/en orden,
  cierto justo después de generar sin borrar) y les migra el nombre. Así el owner puede
  guardar sus cambios actuales ANTES de regenerar.
- **FLUJO OWNER:** 1) Save Campsite Layout (captura+migra lo actual), 2) regenerar → los
  cambios vuelven. Re-Save tras nuevas ediciones. **Guardar SIEMPRE antes de regenerar.**
- Sub-partes internas (ash/ember/luz de la fogata; leños de la pila; partes de la mesa)
  se mueven rígidas con su grupo padre → alcanza con mover el objeto top-level.
- **Necesita compilar** (aparece el menú) y después Save.

---

## 2026-07-10 — FIX carpas multiplicadas (cada FBX del pack trae 5 carpas)

Al regenerar aparecían ~15 carpas desparramadas. Causa: cada FBX de carpa del pack
3Dexter **contiene las 5 variantes de forma** de ese color (5 `Tent_Base` + 5
`Tent_SupportBar`, verificado en el OBJ), y al escalar por altura se desplegaban a lo
ancho (el modelo abarca x≈[1,14]). Las bolsas de dormir venían igual (~5 por FBX).
- `CampsiteBuilder`: nuevo `PS1Tent` con `CropToOneTent` — instancia el FBX, se queda
  con **1 base (lona) + la barra de soporte más cercana** (por bounds), destruye el
  resto (`DestroyImmediate`), y **recentra en XZ** al origen del root para poder
  ubicarla. Materiales por nombre de sub-objeto: lona (`Tent_<Color>`) vs palos
  (`Poles`, detecta "Support"/"Bar"). 3 instancias = 3 carpas.
- Refactor: `PS1Prop` (props de una pieza, p.ej. la fogata) + `PS1Tent` comparten
  `InstProp`/`SeatProp`. La **fogata** es 1 sola (3 partes, mismo atlas) → sigue con
  `PS1Prop`, no se recorta.
- Se **quitaron las bolsas de dormir** (también multiplicadas y sólo decorativas);
  sus FBX/PNG quedan en el pack sin usar.
- **Necesita regenerar.** Sigue pendiente revisar en DÍA el facing/tamaño de las carpas.

---

## 2026-07-10 — Campamento: swap a modelos PS1 reales (pack CC0 de 3Dexter)

El owner bajó el pack **"Retro/Demolished Campground Environment" de 3Dexter3D**
(itch.io, **CC0**) a Downloads. Copiados a `Assets/ExternalAssets/CampsitePS1/`:
3 carpas (Orange/Green/DarkBlue) + `Campfire_Default` + 2 bolsas de dormir + sus
texturas (`Textures/`). Trae FBX + PNG; los `.mtl` apuntan a rutas absolutas del
autor (`C:/Users/ianmc/...`) → Unity no linkea las texturas solo.

`CampsiteBuilder` reescrito para usar los modelos reales donde el pack los tiene, y
mantener lo procedural que quedó bien:
- **Materiales por código** (`CampTexMat`): una URP `MatTextured` por textura del
  pack, con filtro **Point** (forzado en el import) + **mate** (specular/reflejos OFF,
  el mismo fix del halo). `PS1Prop(fbx, texBySub[], x, z, yaw, targetH)` instancia el
  FBX, asigna materiales por submalla (`texBySub` de largo 1 = a todas; N = por índice),
  escala a la altura objetivo preservando la rotación/escala de import (Y-up + yaw), y
  asienta en el piso. Las **carpas** tienen 2 submallas: `[Tent_<Color>, Poles]`
  (índice 0 = lona, 1 = palos, según el orden del OBJ). La **fogata** usa un atlas
  único (`CampfireBake`).
- **Fogata:** modelo PS1 + disco de ceniza (charcoal) + **brasa emisiva + luz** cálida
  (el modelo es estático, sin fuego/luz propios). Se mantiene el grupo `Campfire`.
- **Se dejó procedural** (el pack no lo trae y quedó bien): troncos-asiento, pila de
  leña, mesa. Se borró el código de carpa procedural (paneles/triángulo/lona) y las
  texturas `CanvasTex`/`StoneTex` que ya no se usan.
- ⚠ **A revisar en DÍA:** (1) el "facing" nativo de las carpas es desconocido → si la
  puerta no mira a la fogata, ajustar el `yaw` (posible flip de 180). (2) Asignación
  lona/palos por índice de submalla — si salen cambiados, invertir `texBySub`. (3) Las
  carpas NO tienen collider (se puede atravesar) — agregar MeshCollider si molesta.
- **Necesita regenerar** + visto del owner.

---

## 2026-07-10 — FIX carpas que brillaban (halo blanco): materiales del campamento a mate

De noche, al acercarse, las carpas armaban un gran disco blanco. No era emisión: era
**brillo especular** — las lonas son paneles planos e inclinados que espejaban la luz
puntual de la fogata hacia la cámara, y el bloom del post-FX lo agrandaba (los troncos
no brillaban por ser cilindros curvos y oscuros). Fix en `CampsiteBuilder.MatTex`:
todos los materiales del campamento van **mate** — `_Smoothness=0`, specular OFF
(`_SPECULARHIGHLIGHTS_OFF`) y reflejos de entorno OFF (`_ENVIRONMENTREFLECTIONS_OFF`).
Quedan iluminados por la fogata (difuso) pero sin espejar. **Necesita regenerar.**

---

## 2026-07-10 — Claro sin pasto alrededor del campamento (el pasto alto tapaba las carpas)

El pasto 3D crecía entre las carpas/fogata y quedaba feo. Había solo un claro chico de
5m alrededor de la fogata VIEJA en `SetupGrass`, y `SetupProceduralGrass` (el pasto
alto de la captura) NO tenía exclusión de campamento.
- Nueva const `MapLayout.CampsiteClearRadius = 9f` (el dressing de CampsiteBuilder llega
  ~7-8m del centro).
- `ForestBuilder.SetupGrass`: el viejo `Distance(Campsite+(3,2)) < 5f` → ahora
  `Distance(Campsite) < CampsiteClearRadius` (centrado en el campamento real).
- `ForestBuilder.SetupProceduralGrass`: agregado el mismo claro (antes no tenía).
- Árboles/arbustos ya estaban excluidos <12m del Campsite, así que sólo era el pasto.
  La transición no queda dura porque el thinning por `dGameplay` ya ralea alrededor.
- **Necesita regenerar.**

---

## 2026-07-10 — Campamento del jugador rediseñado (fogata + troncos + carpas, sin autos)

El owner pasó una foto de referencia (camping real en Lago Queñi): fogata central,
troncos-asiento caídos alrededor, carpas atrás. Pidió replicar eso PERO **sin autos**
(la ref tenía camionetas) y con assets **estilo PS1**. El campamento anterior eran
placeholders (auto de cubos, fogata de cilindro+esferas, carpas = cubos naranjas).

Nuevo `CampsiteBuilder.cs` (llamado desde `LandmarkBuilder`, reemplaza el dressing
viejo; se conservan el grupo `Campsite`, el label y los markers de spawn):

- **Estilo PS1 sin depender de un pack:** genera **texturas procedurales de 64² con
  `FilterMode.Point` + sin mipmaps** (corteza, lona, carbón, piedra) — mismo patrón que
  `BridgeBuilder.MetalTex`, cacheadas en Generated. Geometría simple texturizada, nada
  de color plano.
- **Fogata** (grupo `Campfire`, se mantiene el nombre porque "tocarla = muerte" según
  el guion): disco de ceniza/carbón + aro de 9 piedras + 5 leños en teepee + brasa
  emisiva + luz puntual cálida (point, range 14).
- **Troncos-asiento**: 3 cilindros caídos en herradura abierta al sur (donde se sienta
  la gente mirando el fuego), con `FromToRotation` para acostarlos.
- **Pila de leña** (`Firewood`): troncos apilados + ramas.
- **Carpas** (`Tent`): carpa canadiense a dos aguas armada como malla combinada
  (2 faldones de lona inclinados + triángulo de fondo, frente abierto = puerta + piso).
  3 carpas atrás (norte) mirando a la fogata, tinte naranja/verde/azul sobre la misma
  textura de lona. ⚠ cada carpa usa `BuildCombinedStatic` con **nombre único**
  (Tent_0/1/2) para no pisar el mismo `mesh_*.asset`.
- **Mesa de camping** rústica (tablón + 2 bancos + patas) al costado este.
- **SIN autos** (a pedido). Se borraron del código el auto de cubos y sus materiales
  huérfanos (`carMat/blackMat/tentMat/stoneMat`) de `LandmarkBuilder`.
- **Pendiente/– nota:** la "llama" es brasa emisiva + luz (sin partículas) → si el owner
  quiere fuego animado, agregar un `ParticleSystem`. Si consigue modelos PS1 reales de
  carpa/fogata, se pueden swapear (como la cocina). Ajustar posiciones/orientación de
  carpas con captura en DÍA. (El warning "Graphics Ring Buffer space" de la captura es
  de GPU/escena, no de este cambio.)
- **Necesita regenerar** + visto del owner.

---

## 2026-07-09 — Persistencia de ediciones de muebles (mover/rotar/borrar sobrevive al regenerar)

El owner pidió poder mover/rotar/borrar muebles a mano y que no se pierdan al
regenerar el mapa (mismo problema que el terreno). Nuevo `FurniturePersistence.cs`
(análogo a `TerrainEditPersistence`):

- **ID estable por mueble:** cada mueble ahora se llama `Furn_##_modelo`, donde `##`
  es su ÍNDICE en `HouseBuilder.FurnitureItems` (la tabla pasó a ser un campo
  `public static readonly`). Ese ID es la clave para guardar/restaurar.
- **Menú `Tools > Folklore Archives > Save Furniture Layout`:** busca el grupo
  `OldLadyHouse`, recorre los hijos `Furn_##_*`, y guarda pos/rot/escala LOCAL de
  cada uno + marca como `deleted` los IDs de la tabla que ya no están en la escena.
  Se escribe a `Assets/_FolkloreArchives/furniture_layout.json` (fuera de Generated,
  se versiona). También `Clear Furniture Layout` para borrar el archivo y volver a
  la colocación por código.
- **Aplicación:** `BuildFurnitureKenney` llama `FurniturePersistence.Load()` y
  `PlaceFurniture` consulta por ID: si está `deleted` → no lo crea; si tiene
  transform guardado → lo aplica TAL CUAL y saltea la colocación procedural.
- **Cambio importante en PlaceFurniture:** se ELIMINÓ el "holder" vacío. Ahora el
  objeto `Furn_##` ES el FBX (instancia de prefab), porque al clickear en la escena
  Unity selecciona el prefab, no un padre vacío → así lo que el owner mueve/rota es
  exactamente lo que se persiste. Para que el modelo siga parado se preserva la
  rotación/escala de eje del import (`r0`/`s0`) y sólo se le compone el yaw:
  `localRotation = Euler(0,yaw,0) * r0`, `localScale = s0 * (targetH/altura)`.
- **Semántica:** una vez guardado, el JSON es AUTORITATIVO para todos los IDs que
  contiene (la tabla de código sólo aplica a IDs nuevos que no estén en el archivo).
  Si se REORDENA/INSERTA filas en `FurnitureItems`, los IDs se desalinean → volver a
  Save. Flujo owner: generar → mover a mano → Save Furniture Layout → regenerar.
- **Necesita regenerar** (para que los muebles tomen los nombres `Furn_##`), después
  ya se puede empezar a acomodar y guardar.

---

## 2026-07-09 — Cocina: swap a assets PS1 texturizados (los Kenney se veían muy lisos)

Al owner no le gustaron los Kenney (color plano, muy "lisos"); quiere estilo PS1
texturizado. Dejó un pack en Downloads: **PS1 Kitchen Pack (Free) de Dazed Crow
Games**. La versión FREE trae solo **4 FBX** (`PS1_Cabinet_Base`, `PS1_Cabinet_Upper`,
`PS1_Chair`, `PS1_Table`) + **un atlas 256²** compartido (`stove_atlas.png`). Copiados
a `Assets/ExternalAssets/HouseFurniture_PS1/` (+ LICENSE/README).

- **Reemplazos en la cocina-comedor:** mesada base (×2), alacena alta (×2), mesa y
  4 sillas del comedor → PS1. Siguen Kenney (hasta bajar más PS1): bacha
  (`kitchenSink`), cocina (`kitchenStove`), campana (`hoodLarge`), heladera
  (`kitchenFridgeSmall`). El resto de la casa sigue Kenney por ahora.
- **Material** (`HouseBuilder.Ps1Mat`): a diferencia de Kenney (color plano por
  nombre), los PS1 usan UV sobre un atlas único → un solo `MatTextured("ps1_kitchen",
  atlas)` para todas las submallas. Se fuerza el import del atlas a **FilterMode.Point
  + sin mipmaps + sin compresión** (una vez) para el crunch retro PS1.
- `PlaceFurniture` ahora ramifica por prefijo `PS1_`: carga del dir PS1 y aplica el
  atlas; si no, comportamiento Kenney (remapeo de color por nombre). Mismo holder/
  escala-por-altura/asiento. Los PS1 son Y-up, `Rotation=0/Scale=1` (README).
- ⚠️ **LICENCIA (no CC0):** Free con **atribución obligatoria** ("Assets by Tyler at
  (Dazed Crow Games)" en créditos) y **prohibido subir los .fbx/.png fuente a repos
  públicos**. OK si el Plastic es privado + se acredita. Anotarlo en los créditos del
  juego.
- **Pendiente:** el owner va a bajar más packs PS1 para reemplazar el resto de los
  muebles (living, dormitorios, baño). Posiciones/rotaciones de la cocina PS1 pueden
  necesitar ajuste (facing nativo distinto al Kenney) → revisar en captura de DÍA.

---

## 2026-07-09 — Amueblado de la casa con Kenney Furniture Kit (CC0, low-poly)

El owner pidió amueblar la casa en L con assets estilo PSX/low-poly. Elegido
**Kenney Furniture Kit (CC0)** — un solo kit low-poly que cubre TODO, incluido
cocina y baño (que Poly Haven no tenía). Bajado de kenney.nl, copiados 30 FBX a
`Assets/ExternalAssets/HouseFurniture_Kenney/` (+ License.txt).

- **Materiales:** el kit NO trae textura; cada submalla usa un material de color
  plano por NOMBRE (`wood`, `metal`, `metalDark`, `carpet`, `glass`, `lamp`, …
  15 en total). En URP esos materiales importados del FBX salen ROSA. Fix
  (`HouseBuilder.KenneyMat`): recreo los 15 colores del kit como materiales URP
  propios (`BuilderUtils.Mat("kfurn_"+color)`) y en `PlaceFurniture` REMAPEO cada
  submaterial por nombre (match de la clave de paleta más larga contenida en el
  nombre importado, case-insensitive) → conserva el multicolor (patas de madera +
  almohadón, etc.), un material por color = buen batching, nada de rosa. Paleta
  `KPalette` hardcodeada (colores Kd leídos de los .mtl del kit). `lamp` lleva
  emisión 0.5. Fallback `_defaultMat` gris si un nombre no matchea.
- **Colocación** (`BuildFurnitureKenney`): tabla (modelo, x, z, yaw, alturaObjetivo,
  baseY). Mismo patrón que Poly Haven: holder que se rota/escala (los Kenney son
  Y-up → quedan parados), escala midiendo bounds a la altura objetivo, asienta la
  base en `floorWorldY + baseY`. Nuevo param **baseY** para colgar de la pared
  (alacenas altas de cocina 1.55, campana 1.55, espejo de baño 1.15). Se llama
  DESPUÉS del reset de localPosition (las coords de muebles son relativas al grupo).
- **Qué va en cada ambiente:** Dorm. principal → cama doble + 2 mesas de luz +
  ropero + cómoda. Living → sofá + sillón + mesa ratona + alfombra + biblioteca +
  lámpara de pie + mueble con TV vintage + radio. Cocina-comedor → mesada
  (alacena+bacha+cocina+alacena) + alacenas altas + campana + heladera chica +
  mesa + 4 sillas. Dorm2 → cama simple + mesa de luz + ropero. Baño → inodoro +
  lavamanos + espejo + bañadera. Galería → banco + sillón + mesita + planta +
  perchero.
- Se reemplazó el `BuildFurniture` viejo (Poly Haven) por el de Kenney. Los FBX de
  Poly Haven en `Assets/ExternalAssets/HouseFurniture/` quedan huérfanos (se pueden
  borrar). No se llama nada de eso.
- **Riesgo/pendiente:** (1) el remapeo depende de que Unity preserve el NOMBRE del
  material del FBX; si el importer no crea materiales, todo cae al fallback gris (no
  rosa) → si el owner ve muebles monocromos, pasar a parsear el .mtl por submalla.
  (2) Posiciones/rotaciones/alturas son 1er pase estimado (no se conoce el facing
  nativo de los modelos) → **afinar con captura en modo DÍA**.
- **Necesita regenerar** + visto del owner.

---

## 2026-07-09 — Casa de la vieja REDISEÑADA en planta "L" (sin muebles)

Al owner no le gustó cómo quedó la casa (caja rectangular 14×12 simétrica con techo
casi plano → silueta genérica). Le gustó el ESTILO (piedra + revoque verde-oliva +
chapa + chimenea + galería), así que se rehízo solo la volumetría/planta. Se le
mostraron 3 opciones (A planta en L, B rectangular con dos aguas + galería corrida,
C dos cuerpos escalonados) y eligió **A — planta en L**.

`HouseBuilder.cs` reescrito casi entero. Bounding box ahora **16 (x) × 14 (z)**,
centrado en `OldLadyRanch (398,625)` (antes 14×12 → el grupo se corrió ~1-2m; se
actualizó `MapLayout.OldLadyLotMin (384,611)` / `OldLadyLotMax (420,637)` para el
aplanado del terreno y la exclusión de árboles/valla).

- **Planta en L:** cuerpo principal N-S (x0..8, z0..14: dorm. principal S, living
  centro con chimenea O, baño+dorm2 al N) + ala este perpendicular (x8..16, z0..7:
  cocina-comedor). Galería techada en el codo NE (x8..16, z7..14), abierta al este y
  al norte, con columnas de piedra + viga + deck de madera. **Entrada** por la
  galería al living (puerta en x=8, z8.5). El piso sigue siendo losa completa del
  bounding box (la L la hacen las paredes/techos, no el piso).
- **Techos a DOS AGUAS que se cruzan a distinta altura** (esto es lo que arregla la
  silueta): cuerpo principal con cumbrera N-S en x=4, `MainRidgeY=4.6` (más alto);
  ala este con cumbrera E-W en z=3.5, `WingRidgeY=3.95` (más bajo). Faldones =
  cajas finas inclinadas (`AddSlope`, calcula ángulo/centro entre punto-alero y
  punto-cumbrera). Galería = techo a un agua que cae del muro O (x8) al este (x16).
- **Hastiales (triángulos de revoque)** bajo cada dos aguas como prisma triangular
  de malla propia (`AddGable`, doble cara + UVs planas): sur y norte del cuerpo
  principal, y el frente del ala al patio (x=16). El extremo x=8 del ala muere
  contra el cuerpo, no lleva triángulo.
- **Chimenea** de piedra saliente en la pared oeste del living, sube sobre la
  cumbrera (`AddBox` 'z', y 0..MainRidgeY+1). Valla + 2 portones al este: sin cambios.
- **SIN muebles** (a pedido). `BuildFurniture`/`PlaceFurniture` quedan en el archivo
  pero NO se llaman; sus coords son de la planta vieja (14×12 en grilla) → rehacer la
  tabla cuando el owner quiera amueblar la L.
- **Simplificaciones greybox conocidas:** donde el dos aguas del ala se cruza con el
  faldón este del cuerpo principal hay solape de mallas (valle sin recortar) — lee
  bien de afuera pero puede haber leve z-fighting en la junta. Los faldones y
  columnas son cajas (UV estirable). Afinar ángulos/aleros con captura en modo DÍA.
- **Necesita regenerar** (`Tools > Folklore Archives > Generate…`) + visto del owner.

---

## 2026-07-07 — FIX muebles acostados (rotación de eje del FBX)

Los muebles quedaban "mal puestos"/acostados: los FBX de Poly Haven (Blender Z-up,
`.meta`: bakeAxisConversion 0) traen su propia rotación de eje en el root, y
`PlaceFurniture` la PISABA al hacer `inst.localRotation = Euler(0,rotY,0)` → el
mueble se acostaba. Fix: envolver cada instancia en un GO "holder" y rotar/escalar/
posicionar el HOLDER, dejando el FBX con su rotación propia (parado). `PrefabUtility.
InstantiatePrefab` en vez de Object.Instantiate. Escala mide bounds antes de rotar.
Las rotaciones Y (facing) siguen siendo estimadas → afinar con captura en modo DÍA.

---

## 2026-07-07 — Casa FASE 2: muebles de Poly Haven (HouseBuilder.BuildFurniture)

Bajados 9 modelos CC0 de Poly Haven (FBX 1k + Diffuse + nor_gl) a
`Assets/ExternalAssets/HouseFurniture/<Model>/`: Sofa_01, ArmChair_01, CoffeeTable_01,
WoodenTable_02, WoodenChair_01, GothicBed_01, ClassicNightstand_01, GothicCommode_01,
Rockingchair_01. (Descarga vía API polyhaven.com/files/<id>, en Python por word-splitting
del shell.) No hay importador GLTF → se usa FBX + material URP propio (Poly Haven usa
UN atlas por modelo, así que un `MatTextured(diff, nor)` cubre todo el modelo).
`BuildFurniture` (llamado al final de Build, tras el zeroing de hijos): tabla de
(modelo, x, z, rotY, alturaObjetivo); `PlaceFurniture` instancia el FBX, le asigna el
material, lo escala midiendo bounds hasta la altura real, y lo asienta en el piso
(ajusta y para que bounds.min.y = piso). Ambientes: living (sofá+2 sillones+ratona),
comedor (mesa+4 sillas), dorm1 (cama simple+mesa luz), dorm2 (cama doble+cómoda+mesa
luz), galería (hamaca). PENDIENTE: cocina (sin electro en PH) y baño (sin sanitarios
en PH) — usar nappin u otra fuente. Rotaciones/posiciones son estimadas → iterar.

---

## 2026-07-07 — Lote de la casa: terreno aplanado + sin árboles/arbustos

La casa flotaba (terreno con pendiente) y había árboles dentro del cerco.
- `MapLayout`: nuevas consts `OldLadyLotMin (607,587)`, `OldLadyLotMax (643,613)`,
  `OldLadyLotHeight = 25.5` (bounds world del cerco de HouseBuilder).
- `TerrainBuilder.HeightAt`: al final, aplana el rect del lote a OldLadyLotHeight
  con transición smoothstep de 12m → la casa asienta a nivel (samplea esa altura).
- `ForestBuilder`: exclusión del RECTÁNGULO completo del lote (+1m margen) en
  ScatterTrees, ScatterBushes, SetupGrass y SetupProceduralGrass → sin árboles,
  arbustos NI pasto en el patio/perímetro (reemplaza la vieja exclusión de 9m).
  (Clutter/puddles NO excluidos aún — pedir si se quiere el patio 100% pelado.)
- Si la valla de HouseBuilder cambia de tamaño, actualizar OldLadyLotMin/Max.

---

## 2026-07-07 — FIX: la casa aparecía en el origen (0,0,0), no en OldLadyRanch

Bug: `HouseBuilder` armaba la geometría en frame LOCAL (0..W) esperando que el grupo
(en OldLadyRanch) la desplazara, pero `BuildCombinedStatic` fuerza `go.transform.
position = Vector3.zero` (world) → los hijos quedaban con localPosition = -groupPos
y la casa se renderizaba en el ORIGEN del mapa (junto al túnel). Verificado leyendo
la escena (House_Stone localPos = -613,-25.4,-594). Fix: al final de `Build()`,
`foreach (Transform child in group) child.localPosition = Vector3.zero;` → la
geometría queda bajo el grupo, en OldLadyRanch (620,600). (No cambiar
BuildCombinedStatic: túnel/puente/ruta dependen de su world-zero + verts en world.)

---

## 2026-07-07 — Borrado rancho placeholder (se superponía con la casa nueva)

El "Ranch" de `LandmarkBuilder` (cubos Walls/Roof/Door + RanchLight) estaba en
`MapLayout.OldLadyRanch`, EL MISMO punto donde `HouseBuilder` construye la casa
real → superpuestos, se veía mal. Eliminado el placeholder; queda solo un
`BuilderUtils.Label("OLD LADY'S RANCH")` de referencia. Sacado `adobeMat` (quedó
sin uso). La casa nueva queda sola en OldLadyRanch. Sigue: FASE 2 (muebles).

---

## 2026-07-07 — Casa de la vieja FASE 1: cáscara + valla (HouseBuilder.cs)

Nuevo `HouseBuilder.cs` (wired en MapGenerator tras LandmarkBuilder). Arma la casa
en `OldLadyRanch (620,600)` según el esquema del owner, estilo casa patagónica
(ref foto: base canto rodado + columnas piedra, revoque verde-oliva, chapa a poca
pendiente, chimenea piedra, galería). FASE 1 = solo estructura:
- Planta 14×12m: baño (NO) + dorm1 simple (NE) / living (O) + cocina (C) + comedor
  (E) / dorm2 doble (S). Galería sobresale al ESTE (entrada), chimenea al OESTE.
- Paredes con base de piedra (SB=1m) + revoque arriba, con aberturas (helper `Wall`
  + `Op` Door/Win). Interiores sin base de piedra. Techo chapa 2 faldones + galería.
- Valla de madera perimetral (lote -6..30 x, -7..19 z) con 2 PORTONES al este + pilares.
- Texturas CC0 (ambientCG): `PavingStones146` piedra, `PaintedPlaster017`+tint olivo
  paredes, `CorrugatedSteel007A` techo, `WoodFloor051` piso, `WoodFloor064` madera.
- Colliders en piedra/revoque/valla. Muros = cajas combinadas (UV con tiling fijo,
  puede estirarse — es greybox de layout/estilo para validar).
- PENDIENTE FASE 2: muebles de Poly Haven adentro. Posible: aplanar terreno bajo la
  casa (si el suelo tiene pendiente en OldLadyRanch, la casa puede quedar despareja).

---

## 2026-07-07 — Raleo del pasto de campo para que se vea la tierra

La textura Ground054 YA estaba aplicada (verificado: el guid del diffuse de
`layer_muddydirt` = Ground054 Color), pero el pasto 3D verde denso la tapaba y el
owner la veía "igual/verde". `ForestBuilder.SetupGrass`: agregado `grassThin=0.35`
al cálculo de densidad del pasto de campo (`v *= densityFactor * grassThin`) → deja
yuyos salteados con huecos de barro entre medio. No toca pasto de caminos/roderas
ni bushes/ferns. Ajustable (subir/bajar 0.35).

---

## 2026-07-07 — Suelo: textura de tierra REAL (ambientCG Ground054)

El intento con `MudTint` no funcionó porque (a) `BuilderUtils.Tint` CACHEA el .asset
tintado (no se regeneraba al cambiar el tinte) y (b) tintar la textura oliva del pack
no le sacaba el verde. Solución: bajada `Ground054` (tierra/barro marrón real, CC0)
de ambientCG a `Assets/ExternalAssets/TerrainTextures/Ground054/`. `MuddyDirtLayer`
ahora la usa DIRECTA como diffuse (Color) + NormalGL, tileSize 7. Fallback a la vieja
Muddy tintada si falta. Borrada la cache `tex_muddy_dirt_tinted.asset`.
NOTA: el pasto 3D verde (que el owner eligió mantener) sigue tapando el barro donde
es denso; si aún se ve muy verde, hay que ralear el detail (SetupGrass).

---

## 2026-07-07 — Suelo del terreno a barro marrón

Owner pidió "todo el terreno a barro, más marrón". En `MapLayout.cs`:
`BaseMudBlend 0.85 → 1.0` (suelo base 100% capa Muddy, sin pasto verde de textura)
y `MudTint (0.62,0.46,0.30) → (0.52,0.36,0.22)` (marrón barro más profundo/cálido).
El pasto 3D (detail) NO sigue la capa de textura y el owner eligió DEJARLO como está,
así que el barro se ve sobre todo en los huecos/caminos/bordes; donde hay pasto denso
sigue tapando. Si después quiere más barro visible, hay que ralear el detail (SetupGrass).

---

## 2026-07-07 — Look "cámara digital berreta" (corrección: NO VHS)

El primer intento quedó muy VHS (scanlines, RGB split, lavado a blanco) y el owner
NO quería eso. Ref nueva: video de celular berreta 2000s — imagen blanda/borrosa,
LEVEMENTE distorsionada por lente, colores casi normales (no lavados).
- `VhsPostFx.cs` reescrito: LensDistortion 0.22 (barril leve = "distorsión"),
  CA radial sutil 0.16, bloom umbral 1.1 (NO quema luces), saturación -8,
  contraste +6, sin postExposure, WB apenas cálido, FilmGrain Thin1 0.22, vignette
  0.2. Apaga SplitToning + LiftGammaGain (nada de negros lechosos).
- `PC_Renderer.asset`: chromaOffset/scanlineStrength/jitter → 0 (VHS apagado).
- `PC_RPAsset.asset` renderScale 0.65 (se mantiene, da la blandura de cámara berreta).
- Si la lente se ve "pellizcada" en vez de abombada, poner LensDistortion.intensity
  en negativo. Backup/revert: `_ConfigBackups/vhs_2026-07-07/RESTORE.txt`.

### (intento previo — descartado) Look "cámara 2000s / VHS"

Primer intento (muy VHS, no gustó). **Backup del estado ORIGINAL (FtF limpio) en
`_ConfigBackups/vhs_2026-07-07/` (con RESTORE.txt).**
- `VhsPostFx.cs` reescrito: desaturación -32, contraste -14, postExposure +0.35,
  bloom threshold 0.6/intensity 1.3 (luces quemadas), CA 0.55, LiftGammaGain con
  negros lechosos, WhiteBalance cálido (temp 16, tint 6), FilmGrain Large01 0.62,
  vignette 0.42. Apaga SplitToning + LensDistortion del grade viejo.
- `PC_RPAsset.asset` renderScale 0.85 → **0.65** (imagen más blanda/baja-res).
- `PC_Renderer.asset` (VhsChromaShiftFeature): chromaOffset 0.0012→0.004,
  scanlineStrength 0→0.1, jitter 0→0.0012 (RGB split + scanlines/wobble sutil).
- El grade de `VhsPostFx` corre en Start() → se ve en **Play mode**. renderScale +
  chroma feature se ven en Game view siempre. Revertir: copiar los archivos del
  backup a Assets/ (ver RESTORE.txt).

---

## 2026-07-06 — Texturas CC0 para la casa de la vieja (ambientCG)

Bajadas 5 texturas PBR CC0 de ambientCG (1K-JPG) a `Assets/ExternalAssets/HouseTextures/`
(cada una en su subcarpeta, con Color/NormalGL+DX/Roughness/Displacement/AO/Metal):
`PaintedPlaster017` (paredes/revoque), `Bricks097` (cimiento), `WoodFloor051` (piso),
`WoodFloor064` (madera marcos/vigas), `CorrugatedSteel007A` (techo chapa). Interior:
el owner baja "House Interior - Free" (nappin) del Asset Store. PENDIENTE: escribir
`HouseBuilder.cs` que arme la casa rústica en `OldLadyRanch (620,600)` con estas
texturas (usar los _NormalGL para Unity). Estilo: casa rural vieja patagónica.
ESPERAR: el owner va a mandar un ESQUEMA de la distribución antes de construir.
Estado deseado: "habitada pero vieja" (gastada, humilde, pero entera y ordenada).

---

## 2026-07-06 — Puente: más grande + textura metálica generada

- Agrandado: `Span 90→120`, `GirderH 1.8→2.6`, `GirderD 0.6→0.85`, `PierSize
  1.3→1.8`, `RailH 1.15→1.35`, `PierBaseY 4→3`.
- **Textura metálica** generada por código (`MetalTex`, `tex_bridgemetal`):
  brushing vertical + weathering Perlin + costuras horizontales. Aplicada a vigas
  (tint verde, metallic 0.75) y barandas (tint blanco) vía `MetalMat` con tiling.
  Pilares: textura de roca ForestPack Rock2 (`PierMat`). Ya no depende de un asset
  externo. Si el owner baja una textura metálica mejor, cambiar `MetalTex`/`MetalMat`.

---

## 2026-07-06 — Puente metálico sobre el cruce de agua (BridgeBuilder.cs)

Nuevo `BridgeBuilder.cs` (wired en MapGenerator después de RoadsideBuilder). Arma
un puente estilo rural (ref: vigas verdes + barandas blancas con tirantes + pilares
de hormigón) SOBRE la ruta existente (que ya es el tablero + tiene collider):
- **Vigas verdes** laterales segmentadas siguiendo la curva (`PavedRouteZAt`),
  a los bordes reales de la ruta (sur 4.5m, norte 12m) + vigas transversales.
- **Pilares de hormigón**: 2 columnas por caballete cada 20m, desde bajo el tablero
  hasta `PierBaseY=4` (bajo el agua) + viga cabezal.
- **Barandas blancas**: postes cada 4m + baranda superior + tirantes diagonales
  (look reticulado). Ambos lados.
- Todo con MeshCollider. Materiales por color (verde metálico, blanco, hormigón);
  si el owner baja una textura metálica, cambiar `GreenMat()` por `MatTextured`.
- **Ubicación:** `CenterX=800` (cruce del río), `Span=90`. AJUSTABLES — mover a
  donde el owner quiera el puente si 800 no es el cruce correcto.
- OJO: la ruta es asimétrica (4.5/12) → el puente es ancho del lado norte. Y los
  pilares bajan a Y=4; si el terreno ahí está a nivel de ruta (terraplén), quedan
  semienterrados — habría que carvear el terreno bajo el puente (pendiente/opcional).

---

## 2026-07-06 — Alturas de agua horneadas (río Y=9.6, lago Y=-3.3)

El owner ajustó la altura del agua a mano. Horneado: `River_Water` en
`EnvironmentBuilder` Y 7 → **9.6**; `Lake_Water` en `RoadsideBuilder` posición
(0,0,0) → **(0,-3.3,0)**. (La superficie del lago = LakeLevel 13 + (-3.3) ≈ 9.7,
alineada con el río.) X/Z/escala sin cambios.

---

## 2026-07-06 — Río y lago comparten material (mat_lakewater)

`River_Water` (EnvironmentBuilder) usaba `mat_water` y `Lake_Water` (RoadsideBuilder)
usaba `mat_lakewater` — mismos params base pero 2 assets distintos (y el del lago
con doble cara + posibles ediciones a mano), por eso se veían distintos. Ahora el
río usa el MISMO `mat_lakewater` (mismo color/emisión/_Cull=0). `mat_water.mat` queda
huérfano (inofensivo). EnvironmentBuilder corre antes que RoadsideBuilder; ambos
llaman `Mat("lakewater",...)` → mismo asset compartido.

---

## 2026-07-06 — Collider en la superficie de la ruta (PavedRoad_Surface)

`RoadsideBuilder.BuildPavedRoadMesh` ahora le agrega un `MeshCollider` (misma malla)
al `PavedRoad_Surface`. La ruta está en un terraplén a `RoadSurfaceHeight` fijo por
encima del terreno, así que sin collider el jugador caía a través. Ahora se puede
caminar/manejar sobre ella.

---

## 2026-07-06 — Toggle de niebla (menú Tools + botón Scene View)

`MapGenerator.ToggleFog()` — nuevo menú `Tools/Folklore Archives/Toggle Fog`
(atajo Ctrl+Shift+F) + botón en el Scene View debajo del de día/noche
(`DayNightSceneButton.Draw`, muestra "🌫 Niebla: ON/OFF"). Solo flipea
`RenderSettings.fog`; density/color/mode quedan como el preset activo, así que
re-activar restaura el clima. Para inspeccionar el mapa sin la bruma.

---

## 2026-07-06 — TunnelMesh override + pasto sobre el terreno real

- **TunnelMesh horneado:** el owner movió el FBX del túnel hacia adelante
  (x 4.3 → 2.709). Antes no se bakeaba (era el output de `PlaceFbxTunnel`); ahora
  hay `TunnelMeshPos/Scale/Yaw` que se aplican como override justo después de
  `PlaceFbxTunnel`. Las 3 luces no se movieron (quedaron en su posición calculada).
- **Pasto sobre el Terrain real** (`BuildTerrainGrassNearTunnel`): el owner esculpió
  terreno a la izquierda/oeste del túnel y quería pasto ahí (sobre el Terrain de
  Unity, NO sobre mi loma). Calcula la posición mundial de la entrada (matriz
  `groupTRS * facadeTRS`), y esparce ~340 matas de pasto (misma malla cross-quad +
  `GrassMat`) + ~14 arbustos en radio 40m, muestreando `terrain.SampleHeight`.
  Descarta ruta (`DistToPolyline < 6.5`), agua (`h < LakeLevel+0.6`) y pendientes
  > 40° (`GetInterpolatedNormal`). Grupo sibling `TunnelTerrainGrass`. Loguea la
  posición de la entrada para verificar. Ajustar `R`/densidades si el área no cuadra.

---

## 2026-07-06 — Vegetación sobre el montículo + posiciones portal actualizadas

- **Posiciones re-horneadas** (el owner movió los 3 elementos otra vez):
  `PortalFacade (-31.13,-0.39,10.56)`, `PortalCornice (-31.13,-0.5,10.69)`,
  `TunnelMound (-32.4,-1.38,13.34)` (escalas iguales). El grupo Tunnel y el
  TunnelMesh no cambiaron (el scale 0.801 del mesh es el auto-rescale de
  `PlaceFbxTunnel`, determinístico).
- **Vegetación** (`BuildMoundVegetation`): pasto + arbolitos + arbustos sobre el
  montículo. Como el montículo cuelga del grupo con escala NO uniforme (deformaría
  los árboles), la vegetación va en un grupo SIBLING `TunnelVegetation` en espacio-
  mundo. Las posiciones se calculan con `MoundLocalPoint` (refactor compartido con
  el mesh del montículo) transformado por la matriz `groupTRS * moundTRS` (mismas
  constantes horneadas), así caen sobre la superficie real.
  - Árboles: instancia `Generated/ALanTree.prefab` (~12, 2.4–3.9m), escala relativa
    a su normalización (RealTreeTargetHeight). Solo en zonas con pendiente < 33°.
  - Arbustos: `Generated/YughuesBush_P_Bush0[1-5].prefab` (~24, 0.9–1.7m) — scrub
    seco tipo la foto de referencia.
  - Pasto: 1 malla combinada de cross-quads (`TunnelGrass`) con textura de blades
    generada por código (`tex_tunnelgrass`, alpha-cutout, doble cara).
  - `SurfacePoint` descarta pendientes muy empinadas. `ForestBuilder` corre ANTES
    que `TunnelBuilder`, por eso los prefabs ya existen en Generated.
  - OJO: la vegetación es sibling (no sigue al grupo si se mueve a mano en editor);
    se re-sincroniza al regenerar (usa las constantes horneadas).

---

## 2026-07-06 — Montículo: fix de normales (caras miraban para abajo)

La textura del montículo no se veía porque la triangulación generaba normales
apuntando hacia ABAJO (winding invertido) → la cara texturizada/iluminada quedaba
en la parte de abajo. El owner lo había parcheado a mano poniendo el material en
"Render Face = Back". Fix en código:
- Invertido el winding de `BuildMountainMound` (`a,c,b` / `c,d,b`) → normales arriba.
- `MoundMat` ahora fuerza `_Cull = 0` (doble cara) + `doubleSidedGI`, así se ve
  desde arriba sí o sí y sobreescribe el "Back" que quedó en el `.mat`.
(El tiling 0.45 del cambio anterior sigue.)

---

## 2026-07-06 — Portal: posiciones a mano horneadas + fix textura del montículo

- **Textura del montículo:** el material `mat_portal_mound` SÍ tenía Soil_Rocks en
  `_BaseMap`, pero el tiling de UV era 0.08 → con la escala del grupo cada tile
  medía ~22m, se veía como color plano. Subido a **0.45** (~4m/tile) → ahora se ve
  la textura. (La fachada usaba 0.5 por eso sí se le notaba.)
- **Posiciones a mano:** el owner reubicó `PortalFacade`, `PortalCornice` y
  `TunnelMound` a mano. Se leyeron del `.unity` guardado y se hornearon como
  transforms locales (relativos al grupo Tunnel), aplicados con `ApplyLocal` justo
  después de crear cada pieza:
  ```
  PortalFacade  pos(-32.87,-0.5,10.69)  scale(1, 0.9461, 0.8319)
  PortalCornice pos(-32.87,-0.5,10.69)  scale(1, 0.9461, 0.8319)
  TunnelMound   pos(-32.58,-0.5,10.79)  scale(1, 0.9461, 0.8319)
  ```
  (Todas con rotación identidad.) `MakeMeshObject` ahora devuelve el GameObject
  para poder setear su transform. Si se re-mueven, releer y actualizar las consts.

---

## 2026-07-06 — Portal de piedra + montículo de montaña sobre el túnel

Pedido: arco de piedra alrededor de la boca + terreno encima, tipo entrada a la
montaña (ref: portal de ferrocarril de ladrillo en una loma). Reemplaza el intento
viejo (cajas rectangulares que quedaba mal). En `TunnelBuilder.cs`, construido en el
frame pre-transform (antes de aplicar el group transform) para quedar pegado a la
boca del FBX:
- **`BuildStonePortal`**: fachada de piedra como MALLA con hueco arqueado real
  (rect + semicírculo, muestreado en 72 columnas), con espesor (cara frontal +
  trasera + intradós del arco + tapa superior + laterales). Arriba: cornisa
  (`BuildCombinedStatic`) + 7 almenas. Dims por consts `OpenHalfW/OpenRectH/OpenArchR/
  FacWingW/FacParapet/FacDepth` — ajustables si no calza con el tubo.
- **`BuildMountainMound`**: malla heightfield (34×44) tipo domo con ruido Perlin,
  alto en el centro/atrás, cae al piso en los bordes; borde frontal ≈ altura de la
  fachada, así el tubo se lee metido en una loma.
- Materiales: `StonePortalMat` (ForestPack Rock2 color, o Yughues StonesRough) y
  `MoundMat` (TerrainSampleAssets Soil_Rocks, o Rock3). No hay textura de ladrillo
  en el proyecto → se usó piedra (que es lo que pidió: "arco de piedra").
- Como cuelga del grupo Tunnel, hereda el offset/rotación/**escala no uniforme**
  (1.70/1.94/2.19) → el arco se estira igual que el tubo, así siguen calzando.
  Si se re-escala mucho el grupo, revisar que el portal no quede muy deformado.

---

## 2026-07-06 — Persistencia de ediciones manuales del terreno (Smooth Height)

Problema: el heightmap se recalcula 100% desde `HeightAt()` en cada Generate, así
que el smooth/raise/lower que el owner pinta a mano se borra al regenerar.

Solución (`TerrainEditPersistence.cs`, nuevo) — sistema de diff:
- **Menú nuevo:** `Tools > Folklore Archives > Save Terrain Edits`. Lee el heightmap
  actual (con las ediciones a mano), recalcula el procedural puro
  (`TerrainBuilder.ComputeProceduralHeights`, refactor extraído del Build), y guarda
  la **diferencia** (actual − procedural) en `Assets/_FolkloreArchives/terrain_edits.bytes`
  (fuera de Generated para que persista). Solo guarda celdas con diff > 1e-5.
- **En `TerrainBuilder.Build`:** tras calcular el heightmap procedural, llama
  `TerrainEditPersistence.ApplyTerrainEdits(h, res)` que suma el diff guardado antes
  de `SetHeights`. No-op si no existe el archivo o si cambió la resolución (513).
- Como es diff, las celdas no tocadas quedan en 0 → el terreno procedural sigue
  mandando ahí (mover la ruta, etc. sigue funcionando); solo las celdas suavizadas
  llevan corrección.
- **FLUJO PARA EL OWNER Y EL COMPAÑERO:** 1) editar terreno a mano, 2) clic en
  `Save Terrain Edits`, 3) regenerar cuando quieras. Si se pinta MÁS terreno, hay
  que volver a clickear `Save Terrain Edits` (recaptura todo desde el procedural puro).
- El archivo `.bytes` (~1MB a 513²) se versiona en el repo, así el compañero recibe
  las mismas ediciones.

---

## 2026-07-06 — Túnel: quitado el surround procedural (solo queda el FBX)

El owner borró de la escena `PortalFrame`, `TunnelCliff` y `TunnelFarCap` (el marco
de piedra, el acantilado de 3 cajas y la tapa negra del fondo). Se sacaron de
`TunnelBuilder.Build` las llamadas `BuildPortalFrame/BuildCliff/BuildFarCap` y se
borraron esos métodos + helpers muertos (`CI`, `StoneMat`, consts `TubeHalfWidth/
FrameThick/FrameDepth`). Ahora el grupo Tunnel = **solo** el FBX (`PlaceFbxTunnel`)
+ 3 luces puntuales interiores (`AddInteriorLights`). El resto del túnel
(posición/escala del grupo, `KeepParts`) sigue igual.

---

## 2026-07-06 — Túnel: posición ajustada a mano, horneada en el código

El owner movió/escaló el grupo "Tunnel" a mano en el editor para alinearlo con la
ruta. Ese transform se leyó del `.unity` guardado y se horneó en `MapLayout.cs`
para que sobreviva a un regenerate completo. **Valores actuales (2do ajuste):**
```
TunnelGroupOffset = (0.2, -15.9, -77.7)                  // localPosition
TunnelGroupYaw    = 2.777°                                // rotación Y
TunnelGroupScale  = (1.7035, 1.9401107, 2.1910574)       // escala NO uniforme
```
`TunnelBuilder.Build` construye todo en el origen (world coords) y al final aplica
`group.localPosition` + `localRotation` + `localScale` con esos valores — equivale
a mover/escalar el objeto "Tunnel" en el Inspector. **Si se vuelve a ajustar el
túnel a mano, releer el Transform de "Tunnel" y actualizar estas 3 constantes.**

**Partes del FBX recortadas a mano:** el owner borró varios sub-meshes del
`TunnelMesh` en la escena. Se leyó del `.unity` la lista exacta de las 54 partes
que quedaron y se horneó como `KeepParts` (HashSet) en `TunnelBuilder.cs`. En
`PlaceFbxTunnel`, tras el snap, se destruye cualquier hijo del FBX que NO esté en
`KeepParts` — así el túnel recortado se reproduce al regenerar. **Si se borran o
restauran más partes en el editor, re-leer los hijos de TunnelMesh y actualizar
`KeepParts`.** (El recorte es DESPUÉS del centrado, así no mueve lo que queda.)

⚠️ PENDIENTE: `LandmarkBuilder` spawnea `SPAWN_CAR_START` en world
`(0, 17.5, PavedRouteZAt(30)≈80)`, que es la posición ANTES del transform del túnel.
Con el offset actual (−15.9 en Y, −77.7 en Z) + escala ~1.7–2.2 + yaw 2.78°, el
spawn ya no cae dentro del tubo. Cuando se implemente el manejo, recalcular el
spawn relativo al grupo Tunnel (aplicar su localPosition/rotation/scale). Por ahora
no afecta (no hay driving todavía).

---

## 2026-07-06 — Túnel v3: FBX real analizado y colocado correctamente

Los intentos anteriores fallaban porque colocábamos el FBX a ciegas. Esta vez se
analizó el OBJ (misma geometría) con un script: el modelo tiene 13 partes, y una
de ellas — `Cube.002`, una caja de 29×29×255 m que envuelve todo — era el
"cubo blanco" que tapaba la entrada. `TunnelBuilder.cs` reescrito:

- **Borra la caja envolvente** al instanciar (regla: mesh con bounds >15 m en X e Y).
- **Medidas reales del tubo** (`Tunnel_walls`): ±5.46 m ancho, 6.93 m alto,
  ~193 m largo sobre +Z local, piso (`Road_Plane`) en y=0 local.
- Rotación **Y=-90°** (local +Z → mundo -X): el tubo corre hacia el OESTE.
- Snap por bounds: cara este del tubo → `TunnelEntranceX`(30)+0.3, piso → y=17,
  centro del tubo → z de la ruta. Auto-reescala si el largo importado difiere
  ±10% de 204 m (por si Unity importa el FBX en cm).
- **MeshCollider en cada parte** — la ruta no tiene collider y el terreno
  termina en x=0, así que el `Road_Plane` del FBX es lo que hace el túnel
  manejable (spawn del auto en x=0, 30 m adentro).
- Materiales por nombre de nodo: asfalto oscuro (road), hormigón (walls),
  veredas gris claro, tiras de luz EMISIVAS (Mat emission 1.6), semáforos oscuros.
- Alrededor, procedural: marco de portal rectangular estilo noruego
  (pilares + dintel, hueco 5.7×7.0), acantilado de 3 cajas con el hueco a
  la medida del marco, tapa negra + collider a 2 m del extremo oeste
  (el auto no puede caerse al vacío), y 3 luces puntuales cálidas interiores.
- OJO: la escena tenía DOS roots `FOLKLORE_MAP` (quedó uno duplicado de una
  edición manual). Borrar ambos antes de regenerar.

---

## 2026-07-06 — Posiciones actualizadas del mapa (snapshot para sincronización)

Owner ajustó posiciones de puntos clave del terreno. Estado actual completo
de `MapLayout.cs` al momento de este log — **referencia canónica para el compañero**:

### Mapa general
| Constante         | Valor                  | Notas                          |
|-------------------|------------------------|--------------------------------|
| `MapSize`         | 1000f (eje Z)          | extensión norte-sur            |
| `MapSizeX`        | 1400f (eje X)          | extensión este-oeste           |
| `MaxHeight`       | 60f                    | altura máxima del terreno      |
| `RoadSurfaceHeight` | 17f                  | Y fijo de la superficie de ruta|
| `LakeLevel`       | 13f                    | plano de agua (~4m bajo ruta)  |

### Ubicaciones clave (x, z) en `MapLayout.cs`
| Nombre              | x    | z    | Descripción                          |
|---------------------|------|------|--------------------------------------|
| `Campsite`          | 710  | 335  | Campamento jugadores, al lado del río |
| `OldLadyRanch`      | 620  | 600  | "VIEJA" — sobre Path A               |
| `HuntingField`      | 540  | 480  | Campo seco abierto (Acto 2)          |
| `Grave`             | 700  | 850  | "TUMBA" — esquina superior derecha   |
| `MainCriminalCamp`  | 250  | 840  | "DELINCUENTES PRINCIPAL" — col. izq. |
| `SecondaryCamp`     | 200  | 560  | "CAMPAMENTO SECUNDARIO" — medio izq. |
| `HostageArea`       | 330  | 790  | Área rehenes (Acto 3)                |
| `RiverBeach`        | 730  | 335  | Playita de pesca junto al campamento |
| `DirtTurnoff`       | 620  | ~82  | Desvío tierra (calculado del spline) |
| `TunnelEntranceX`   | 30   | —    | Portal del túnel (cara este del acantilado) |

### Ruta pavimentada — puntos de control Catmull-Rom (`PavedControls`)
```
(-260, 86) → (150, 70) → (520, 92) → (880, 72) → (1180, 90) → (1500, 74) → (1660, 82)
```
Curva suave, espaciado ~22m. Norte = bosque, Sur = lago/guardarrail.

### Río — puntos de control (`RiverControls`)
```
(825,-60) → (800,120) → (768,250) → (756,335) → (772,430)
→ (815,545) → (828,665) → (800,785) → (820,905) → (805,1060)
```
Espaciado ~18m. Giro máximo hacia el campamento en z=335 (playa de pesca).

### Senderos
| Sendero               | Waypoints                                              |
|-----------------------|--------------------------------------------------------|
| `DirtRoad`            | DirtTurnoff → (650,200) → Campsite                     |
| `PathA`               | Campsite → (670,450) → OldLadyRanch → (660,720) → Grave |
| `GraveToCriminals`    | Grave → (480,880) → MainCriminalCamp                   |
| `CriminalsToSecondary`| MainCriminalCamp → (180,700) → SecondaryCamp           |
| `PathB`               | SecondaryCamp → (330,480) → (500,400) → Campsite       |
| `BeachPath`           | Campsite → RiverBeach                                  |
`ScaryPaths` = PathB + CriminalsToSecondary + GraveToCriminals (bosque denso y oscuro).

### Túnel de entrada (oeste, x=30)
| Constante            | Valor  |
|----------------------|--------|
| `TunnelEntranceX`    | 30f    |
| `TunnelHalfWidth`    | 5.5f   |
| `TunnelRectHeight`   | 4.5f   |
| `TunnelLength`       | 55f    |
| `TunnelPortalDepth`  | 3.0f   |
| `TunnelFrameWidth`   | 3.5f   |

---

## 2026-07-06 — Tunnel: CGTrader FBX asset replaces procedural interior

Owner downloaded "Road Tunnel" (free, royalty-free) from CGTrader (judefelix),
placed at `Assets/ExternalAssets/TunnelAsset/Tunnel.fbx`.

`TunnelBuilder.cs` updated to use `TryBuildFromAsset()`:
- Loads `Tunnel.fbx` via `AssetDatabase.LoadAssetAtPath<GameObject>` and
  instantiates it at the centre of the tunnel tube
  `(x = TunnelEntranceX - TunnelLength*0.5, y = RoadSurfaceHeight, z = roadZ)`.
- Rotation `TunnelAssetRotY = 90°` so FBX +Z aligns with world +X (road axis).
- `TunnelAssetScale = 1.0` and `TunnelAssetOffsetY = 0.0` — tune these constants
  after first regenerate if the mesh doesn't sit flush on the road floor.
- The procedural portal face + interior tube are now the **fallback only**
  (used if the FBX is missing); the cliff box stays procedural regardless.
- **Needs a fresh regenerate** then visual check: does the FBX opening align
  with the road width (±5.5m), is the floor at the right height, does the asset
  tunnel mesh length (~55m) fill the cliff box?  Adjust the three tuning
  constants at the top of `TunnelBuilder.cs` without re-running the full generator.

---

## 2026-07-06 — Tunnel corrected to WEST end (TunnelEntranceX = 30f)

Previous session had the tunnel at the east end (x=1380). Owner confirmed:
"te confundiste de lado es al otro lado de la ruta el tunel" → west end.
`MapLayout.TunnelEntranceX` changed from 1380f to **30f**.
All geometry/spawn directions inverted (tube/cliff go west = decreasing X,
player faces east = `Vector3.right`). `TerrainBuilder` cliff term changed from
raising `wx > TunnelEntranceX - 20` (east edge) to raising `wx < TunnelEntranceX + 20`
(west edge). Old DEV_LOG entry below is stale on the entrance side and the X values.

---

## 2026-07-06 — Tunnel portal at east entrance (new TunnelBuilder.cs)

Owner sent a reference photo of a Norwegian road tunnel cut into a stone cliff
and asked for the same at the east end of the map where the game begins.
Added `TunnelBuilder.cs` (new file, wired into `MapGenerator.cs`):
- **Portal face**: procedural arch ring mesh (`MakeArchRingMesh`) - a half-torus
  in the Y-Z plane at `TunnelEntranceX=1380f`, inner radius = `TunnelHalfWidth=5.5m`,
  outer radius = inner + `TunnelFrameWidth=3.5m`, depth `TunnelPortalDepth=3m`.
  Plus south/north pillar boxes and top beam as `BuildCombinedStatic`.
- **Interior tube**: arch-shaped mesh strip (floor + walls + curved ceiling) 55m
  long going east past the terrain edge. Double-sided concrete material so all
  faces are visible from inside.
- **Cliff box**: one large `BuildCombinedStatic` stone box behind the portal
  representing the mountain the tunnel cuts into (wider and taller than the frame).
- **Terrain cliff**: added a `tunnelCliffT` term to `TerrainBuilder.HeightAt()` that
  raises ground by up to 36m for `wx > TunnelEntranceX - 20`. Applies at ALL z
  values (unlike the regular east ridge which only fires for wz > 150), so the
  mountain rises to both sides of the road at the tunnel opening. The road flatten
  zone (dPav < 13m) keeps the road opening itself flat.
- **Spawn moved inside tunnel**: `LandmarkBuilder` SPAWN_CAR_START moved from
  `MapSizeX-60=1340f` (outside) to `TunnelEntranceX+30=1410f` (30m inside).
  Y is fixed at `RoadSurfaceHeight+0.5` (not terrain-sampled: x=1410 is past the
  terrain edge into the procedural tunnel volume).
- Dim warm point light inside the tunnel so the player can see the arch silhouette
  without the interior being completely black at spawn.
- All constants in `MapLayout.cs`: `TunnelEntranceX`, `TunnelHalfWidth`,
  `TunnelRectHeight`, `TunnelLength`, `TunnelPortalDepth`, `TunnelFrameWidth`.
- Stone and concrete materials are double-sided (`_Cull=0`) to avoid winding
  order issues on the arch ring inner face and cliff box interior.
- **Needs a fresh regenerate + owner visual check** on portal proportions and
  whether the cliff looks like a believable mountain face.

---

## 2026-07-06 — Road mesh UV fix + road mesh height independent of terrain

Two follow-up fixes to the paved road mesh (`RoadsideBuilder.BuildPavedRoadMesh`):
- **UV centering**: center vertex was hardcoded to U=0.5 but the road is asymmetric
  (southHalf=4.5m, northHalf=12m), so Kajaman's centre dash appeared at the spline
  rather than the physical road centre. Fixed to `centerU = southHalf/(southHalf+northHalf)
  ≈ 0.273` so U=0.5 in the texture lands at the physical road mid-point.
- **Fixed-height road mesh**: road vertices were placed with `BuilderUtils.Ground(terrain)`
  + small lift, so terrain bumps caused the mesh to follow the terrain and poke through
  it. Replaced with fixed `roadY = RoadSurfaceHeight + lift` for all top-surface verts.
  Added skirt faces (2.5m deep on each side) so the mesh has real volume and covers any
  remaining seam between road edge and terrain berm.
- `TerrainBuilder` flat zone widened from 8m to 13m (`dPav - 13f`) so the terrain
  under the full 12m-wide north half is genuinely flat at road level.

---

## 2026-07-05 — Performance pass: batched guardrail + ground clutter, static flags

Owner asked to "optimize the whole map." Ran a full survey of every script in
`Assets/editor/MapGenerator/` before touching anything. Conclusion: the prior
134M-tri/42FPS incident fix (tree billboarding, render/detail distances -
already tuned in `MapLayout.cs`) never got extended to non-tree geometry, so
the SAME "thousands of unbatched shadow-casting primitives" pattern was still
present in two places:

- **`RoadsideBuilder.BuildGuardrail`**: was one `Cube` primitive GameObject
  per post AND per beam every 6m along the ~1390m road - about **464
  separate draw calls**, no static batching. Rewritten to collect
  `CombineInstance` transforms and bake into 2 combined static meshes
  (posts, beams) via the new `BuilderUtils.BuildCombinedStatic()` -
  464 draw calls -> 2. The beam mesh keeps a `MeshCollider` (one, combined)
  so the player still can't walk off the road into the lake.
- **`ForestBuilder.ScatterClutter`**: was one `Cylinder`/`Sphere` primitive
  GameObject per fallen log / rock (up to 3 rocks per cluster) across ~6600
  grid candidates at 0.55 density - likely thousands of unbatched, shadow-
  casting GameObjects. Same fix: collected into `CombineInstance` lists,
  baked into 2 combined static meshes (`ClutterLogs`, `ClutterRocks`).
- New shared helpers in `BuilderUtils.cs`: `PrimitiveMesh(type)` (caches
  Unity's built-in primitive meshes instead of spawning/destroying a temp
  GameObject per instance) and `BuildCombinedStatic()` (bakes a
  `List<CombineInstance>` into one static, shadow-off mesh + optional
  collider - shared by both fixes above).
- `EnvironmentBuilder`'s river water plane was the one water/road surface in
  the project NOT disabling `shadowCastingMode` (lake/road-mesh/puddles all
  already did) - fixed to match, plus marked `isStatic`.
- `LandmarkBuilder.Build()` now calls `BuilderUtils.MarkStaticRecursive(poi)`
  at the end - all the shacks/tents/campfires/car/grave props are fixed set-
  dressing, so marking them static lets Unity's automatic static batching
  merge them. New `MarkStaticRecursive()` helper added for this - **do not**
  call it on `TEST_PLAYER` or anything meant to move.
- **Not touched / confirmed already fine** (per the survey): tree/grass
  density and render distances (already tuned post-incident, don't cut
  further without visual sign-off), terrain alphamap/heightmap resolution
  (2048/513, deliberate per earlier wheel-rut fix), material/texture caching
  via `BuilderUtils.Mat`/`MatTextured` (already consistent everywhere), and
  the tree/bush prototype baking pipeline (`BakeExternalTree` already
  combines FBX meshes correctly - this was the template the guardrail/
  clutter fixes above followed).
- **Skipped as lower-impact**: the O(n) `DistToPolyline` scans inside
  `ScatterTrees`/`ScatterBushes`/`TerrainBuilder`'s per-texel loops only cost
  EDITOR generation time (slower "regenerate map" clicks), not runtime FPS -
  not worth the added complexity of a spatial-hash pre-check right now.
- **Needs a fresh regenerate + owner FPS check** (Stats window / profiler) to
  confirm the actual improvement.

---

## 2026-07-05 — Real road-surface mesh added (lane lines now actually follow the curve)

Owner first accepted the "lines drift slightly off-angle through bends"
limitation (inherent to painting the road as a terrain LAYER, which tiles in
fixed world X/Z), then changed their mind and asked to actually fix it rather
than live with it.

Realized the earlier "dead end" conclusions about a road MESH (EasyRoads3D
needs interactive Editor drag/drop; Kajaman's pack is 2 giant pre-baked
meshes, not modular pieces) only rule out using SOMEONE ELSE'S mesh tool/
asset - they don't rule out generating our OWN simple mesh procedurally,
which is exactly what `RoadsideBuilder.BuildLake`/`BuildGuardrail` already do
(strip geometry sampled along `MapLayout.PavedRoute`). Added
`RoadsideBuilder.BuildPavedRoadMesh()` following the same pattern:
- Walks `MapLayout.PavedRoute` (the fine Catmull-Rom polyline), and at each
  point builds 3 vertices (south edge at -4.5m, centre, north edge at +12m -
  matching `TerrainBuilder`'s Strip "full" widths) offset along the local
  perpendicular (`side = rotate90(tangent)`), sampling terrain height at each.
- **The key fix**: V (the along-road UV) is driven by ACTUAL ARC LENGTH
  accumulated along the polyline, not raw world X - so the dash/edge-line
  pattern is physically glued to the curve and tracks it exactly, sharp bends
  included, unlike the terrain-layer approach. Uses the Kajaman texture
  UN-rotated (U=0 south edge, 0.5 centre, 1 north edge) since here WE define
  which texture axis means "along" vs "across" via the UVs, so none of the
  earlier `Rotate90`/tileSize juggling applies to this mesh.
- Sits 0.05m above the terrain (`lift`) to avoid z-fighting with the
  still-in-place terrain-layer asphalt paint underneath, which now mostly
  just shows through as a slightly-wider "shoulder" in the fade zone past the
  mesh's hard edge (12-14m north / 4.5-6.2m south) - harmless, reads as a
  gravel shoulder before the treeline/guardrail.
- `BuilderUtils.MatTextured()` gained an optional `normalMap` param (backward
  compatible - existing callers unaffected) so this mesh's material can use
  Kajaman's real normal map too.
- Wired into `RoadsideBuilder.Build()`, runs after the guardrail.
- **Needs a fresh regenerate + owner confirmation** that the lines now
  visibly hug the curve instead of drifting.

---

## 2026-07-05 — Paved texture was rotated 90° (lines ran across the road, not along it)

Follow-up to the widening below: owner still saw no proper grey asphalt+lane-
line look after regenerating - instead, faint white dashes repeating several
times "sideways" across the road width, looking washed out/translucent.
Confirmed via the Console (no "texture not found" warning) that Kajaman's
`Road_2lane_dark02.png` WAS loading correctly, so the bug wasn't a missing
asset - it was **UV orientation**: the source texture is authored with its
lane markings running along the image's V axis, but Unity terrain layers map
U->world X and V->world Z, and our paved route runs mostly along X. So the
"along the road" dash pattern was landing on the WIDTH axis instead - showing
as repeated lines across the road (repeating every `tileSize.y=8m` across an
~18m-wide paved corridor) rather than one clean line running down the middle.
- `BuilderUtils.Rotate90()` (new): bakes a 90-degree-rotated copy of a texture
  as a cached generated asset (same pattern as the other procedural textures
  in `Generated/`), forcing the source's `isReadable` import flag on first so
  `GetPixels32` works. For normal maps it also rotates the encoded
  tangent-space X/Y (R/G channels), not just the pixel grid, so the bump
  lighting stays correct instead of looking lit from the wrong side.
- `TerrainBuilder.PavedRoadLayer()` now uses the rotated diffuse + normal, and
  `tileSize` changed from `(8, 8)` to `(9, 20)` - x (along-road) keeps roughly
  the original dash spacing; y (across-road) is set past the widest paved
  section (~18m combining the north/south shoulder widths above) so exactly
  ONE tile spans the whole width instead of repeating the edge/centre lines
  several times across it.
- **Still a known limitation** (unchanged from before): terrain layers can't
  rotate per-segment to follow the road's curve, so lines will still be very
  slightly off-angle through bends - this fix only corrects the gross
  along-vs-across orientation, not curve-following.
- **Follow-up correction**: after regenerating, owner saw the centre/edge
  lines duplicated (~2 parallel dashed lines instead of one) - the
  `tileSize.x`/`tileSize.y` split above had it backwards. Empirically
  `tileSize.x` is the ACROSS-road repeat and `tileSize.y` the ALONG-road one
  here (opposite of the usual U->X/V->Z assumption) - swapped to
  `(26, 9)`. If this is still off, that x/y split is the first thing to
  flip again.
- **Needs a fresh regenerate + owner confirmation.**
- **Owner confirmed (2026-07-05)**: the lane-line pattern drifting slightly
  off-centre through the road's gentle bends (since `TerrainLayer` tiling is
  world-axis-locked and can't rotate per-segment to follow the curve) is
  accepted as-is for greybox stage - not worth chasing further given the only
  real fix is a curved road mesh, already a dead end this session (EasyRoads3D
  doesn't work in Unity 6.3 without Editor drag/drop; Kajaman's pack is
  giant pre-baked meshes, not modular pieces - see the EasyRoads3D entry
  below). The paved AREA/width still correctly hugs the curve either way
  (that part uses `DistToPolyline` against the real curve) - only the
  texture's internal line pattern doesn't rotate with it.
- Also widened the north/forest shoulder from 12m to 14m (owner: wants each
  lane roomy enough for a car on either side of the centre line) and bumped
  `tileSize` from `(26, 9)` to `(29, 9)` to keep the same edge-line-to-width
  proportion.

---

## 2026-07-05 — Paved texture widened on the forest side (was leaving a bare gap)

Owner reported the road still didn't look paved along most of its length
("agregá pavimento en toda la ruta") despite the Kajaman asphalt layer already
being wired in. Root cause found in `TerrainBuilder.PaintTextures`: the
asphalt alpha strip was a **symmetric** `Strip(dist, 4.5, 6.2)` around the
route centreline (~6.2m half-width), but `ForestBuilder`'s tree-exclusion
radius on the north/forest side is ~12-13m (`DistToPolyline(..., PavedRoute) <
12f/13f` in `ScatterTrees`/`ScatterBushes`/grass passes) - so there was a
~6-7m band of bare dirt/grass-colored terrain between the asphalt's edge and
the treeline, on the side away from the lake, which is most of what a
forest-driving camera actually sees. That gap is what read as "unpaved."
- `PaintTextures` now computes the paved weight **asymmetrically**: south
  (lake side, `wz < PavedRouteZAt(wx)`) keeps the old narrow `Strip(4.5, 6.2)`
  so asphalt still ends right at the guardrail (`GuardrailOffset=5.5m`)
  without bleeding onto the embankment/shore vegetation; north (forest side)
  widened to `Strip(10, 12)` so the paved shoulder now reaches almost exactly
  to where trees start, leaving only ~1m of gravel-shoulder feel instead of a
  wide dirt/grass no-man's-land.
- Also hardened `PavedRoadLayer()`: it now calls `AssetDatabase.ImportAsset`
  on the Kajaman diffuse texture before loading it, in case the asset wasn't
  indexed yet, and the fallback warning is more explicit that landing on the
  `Rock_TerrainLayer` fallback (real sandy/rock texture, no lane markings) is
  what makes the road look like bare tan rock instead of dark asphalt. If the
  road still doesn't look paved after regenerating, check the Console for
  that warning - it means `Assets/KajamansRoads/Textures/Road_2lane_dark02.png`
  isn't loading for some reason and needs investigating directly.
- **Needs a fresh regenerate + owner confirmation**: run
  `Tools > Folklore Archives > Generate Greybox Map` again to see this; the
  terrain alphamap is baked at generation time, so old bakes won't update on
  their own.

---

## 2026-07-05 — EasyRoads3D abandoned; painted road kept + width tightened

Tried to add a real road MESH with EasyRoads3D Free (owner imported it) so lane
markings would follow the curve. Dead end: **EasyRoads3D Free's marker placement
does not work in Unity 6.3** — Shift+Click in the scene is swallowed by Unity's
default add-to-selection (it selects the Terrain) instead of being consumed by
ER3D's OnSceneGUI, so no markers can be placed. (The scripting API that could
place a road from code is Pro-only.) Spent several rounds on it; not worth more.
Decision: **stay with the terrain-painted road** (Kajaman `Road_2lane_dark02`
asphalt on terrain layer[2], already following the smooth spline across the
enlarged map). Its only downside is the lane lines don't rotate through curves
(terrain layers tile world-aligned) — minor, accepted.
- Tightened the painted road width: `PaintTextures` concrete `Strip(7,10)` →
  `Strip(4.5, 6.2)` so the ~9m 2-lane asphalt ends right at the guardrail
  (GuardrailOffset 5.5m) instead of bleeding onto the embankment.
- The empty "Road Network" GameObject ER3D created can be deleted from the scene.

---

## 2026-07-05 — Paved route redone as a smooth spline (killed the zig-zag)

Owner: the S-curve waypoints read as a zig-zag ("no como el zigzag que
hiciste"). They alternated z 95/55/105/60/... every ~140m = a tight wavy
pattern. Replaced with:
- `MapLayout.PavedControls`: 5 gentle, widely-spaced (~370m) control points with
  small amplitude, running past both map edges (x -260 → 1260) so the road
  enters/leaves the terrain mid-curve.
- `MapLayout.BuildSmoothRoute` + `CatmullRom`: sample a Catmull-Rom spline
  through the controls into a fine (~22m) x-monotonic polyline, assigned to
  `PavedRoute`. Everything downstream (texture, guardrail, lake edge, veg
  exclusions, PavedRouteZAt) follows the smooth curve automatically.
- `DirtTurnoff` is no longer a hardcoded (620,70) waypoint; it's now derived as
  `(620, PavedRouteZAt(620))` so the dirt-road junction always sits exactly on
  the road wherever the curve puts it.
- Perf note: PavedRoute went from 9 pts to ~75, so every DistToPolyline/
  PavedRouteZAt over it is ~8x more segment checks. Regen is a bit slower but
  fine; if it ever matters, raise the `spacing` arg or make PavedRouteZAt O(log n).
- Owner then confirmed they want the terrain enlarged for more drivable road, so:

## 2026-07-05 — Map enlarged in X (non-square terrain) for a long road approach

Made the map **non-square**: `MapSizeX = 1400`, z extent still `MapSize = 1000`.
The extra 400m is road-approach on the EAST. Rather than shift all the existing
content coordinates (error-prone), the road entrance was moved to the new east
end and the player now drives WEST into the map.
- `MapLayout`: added `MapSizeX`; `PavedControls` extended east to x=1660 so the
  smooth road spans the wider map.
- Every X-axis use of `MapSize` was switched to `MapSizeX` (terrain `size.x`, the
  `wx = xi/(res-1)*...` sampling in TerrainBuilder + ForestBuilder, all the
  `for x < MapSize` scatter loops, the `p.x / MapSize` TreeInstance
  normalisations, RoadsideBuilder guardrail end + lake xEnd). Z-axis uses (`wz`,
  `for z`, `p.y / MapSize`, `size.z`) deliberately left as `MapSize`. So the
  terrain, forest, grass, guardrail and lake all fill the new width automatically.
- `TerrainBuilder` east ridge moved from a hardcoded 940 to `MapSizeX - 60` so it
  walls the new east edge, not the middle of the new approach.
- `LandmarkBuilder`: START + `SPAWN_CAR_START` moved to `x = MapSizeX-60` (~1340)
  on the road, car now faces WEST (`Vector3.left`). TEST_PLAYER still spawns at
  the campsite (unchanged, still valid).
- The heightmap/alphamap grids stay square (513 / 2048) stretched over the
  1400×1000 terrain — slightly anisotropic texel size, negligible for greybox.
- If this ever needs to instead extend on the WEST keeping the drive-east feel,
  that requires shifting all content +X (locations AND the hardcoded path
  intermediates + river points) — not done, noted here as the alternative.

---

## 2026-07-05 — Shore vegetation on the lakeside embankment

Follow-up to the lakeside work below: owner wanted the strip BETWEEN the
guardrail and the water (previously bare) to have grass, some bushes, and a few
small pines. Added a "shore band" = south-distance from the road centre in
`[ShoreVegNear=6, ShoreVegFar=16]` (constants in `MapLayout`; the true waterline
sits a bit past 16 so the very edge stays a bare wet margin):
- `ForestBuilder.ScatterTrees` / `ScatterBushes`: new early shore branch (before
  the road exclusion, so they can grow right behind the guardrail) that sparsely
  adds small young pines (scale 0.26–0.46) at `ShorePineDensity` and bushes at
  `ShoreBushDensity`, then `continue`s. Beyond `ShoreVegFar` = skip (water).
- Grass passes: exclusion relaxed from 8m to `ShoreVegFar` so grass covers the
  embankment down to near the waterline. Then (owner: "que el pasto llegue hasta
  la barrera") the road's `PavedRoute < 10f` grass exclusion was made
  forest-side-only (`southD <= ShoreVegNear && ...`) so lakeside grass grows
  right up to the guardrail (~6m) instead of stopping 10m short of it.
- `TerrainBuilder.PaintTextures`: the bare gravel/dirt paint now starts at
  `ShoreVegFar-4` instead of the shoulder, so the upper embankment stays green
  and only the last few metres by the water go gravel.

---

## 2026-07-05 — Lakeside: guardrail + lake south of the paved route

Owner (with RN40 / Neuquén Street View photos as reference) wanted the side of
the paved route AWAY from the forest (the south side) to be: a metal road
guardrail, then a lake, with mountains in the background.

- **Which side / geometry helper**: the paved route's waypoints strictly
  increase in x, so it's a function z = f(x). Added `MapLayout.PavedRouteZAt(x)`.
  "Lake side" = any point with `z < PavedRouteZAt(x)` (south, toward the map
  edge); "forest side" = north. New lakeside constants in `MapLayout`
  (RoadSurfaceHeight, LakeLevel, LakeBedHeight, LakeShoulderWidth,
  LakeSlopeWidth, GuardrailOffset, GuardrailPostStep).
- **Mountains** were already handled: `EnvironmentBuilder.BuildDuskSky()` paints
  mountain-ridge silhouettes into the skybox all around the horizon, so the far
  shore of the lake reads as mountains for free. Nothing new needed there.
- **Terrain carve** (`TerrainBuilder.HeightAt`): south of the road, past a ~10m
  shoulder, the ground ramps down over ~26m to a lakebed floor (7m) below the
  waterline (13m), with a little Perlin wobble on the shore. `Mathf.Min` so it
  only lowers ground, never fights the road flatten. `PaintTextures` paints that
  embankment/bed as bare dirt/gravel, not grass.
- **Lake** (`RoadsideBuilder.BuildLake`): a procedural flat water strip mesh
  (saved to Generated/mesh_LakeSurface.asset) whose NORTH edge follows the road
  curve (offset ~6m south) and whose far edge runs to z=-380 (past the map
  edge). Key trick: the water sits at a fixed y=13 and wherever the uncarved
  ground is higher, the terrain just hides it — so the mesh can start right
  behind the guardrail without ever poking onto the road, and the visible
  waterline is the carved shoreline. Same dark water material as the river;
  forced double-sided (_Cull=0) so a flipped normal can't make it invisible.
- **Guardrail** (`RoadsideBuilder.BuildGuardrail`): posts + W-beam boxes every
  ~6m along the road's south side, following the curve and terrain height,
  textured with Kajaman's `Guardrails01.png` (galvanised W-beam). Beams keep
  their box collider (stops the player walking off into the lake); posts don't.
  All shadow-casting off (night perf).
- **Vegetation excluded** on the lake side: added a `PavedRouteZAt(p.x) - p.y >
  N` guard to all five scatter passes in `ForestBuilder` (trees/bushes/clutter
  at >5m south, grass at >8m south) so the embankment/lake is bare.
- Wired into `MapGenerator.Generate` as `RoadsideBuilder.Build(root, terrain)`
  after ForestBuilder.
- **Known minor issue, left for later**: near the SE corner the new lake (y=13)
  overlaps the existing river (y=7); the lake plane is 6m higher so it just
  visually covers the river there (a small height step where they meet). Not
  worth solving at greybox stage; note it if the corner ever matters.
- **Night visibility caveat**: the map is designed pitch-black past the
  flashlight, so at true night the lake/guardrail mostly read as silhouettes
  against the dim blue sky. Inspect in Day preview to judge composition.

---

## 2026-07-05 — Paved route: curves, potholes, and a road-asset dead end

`MapLayout.PavedRoute` was a dead-straight 2-point line. Owner wanted curves
and slight potholes, plus a real road texture (was using a flat "concrete"
color/Rock_TerrainLayer before).

- **Curves**: `PavedRoute` (`MapLayout.cs`) is now a 9-waypoint gentle S-curve
  instead of 2 points, wandering roughly +/-40m in z. `DirtTurnoff` (620,70)
  is kept as one of the waypoints so the dirt road junction still sits
  exactly on the curve. Fit comfortably inside the existing 1000m map -
  nothing else lives in that z<150 strip, so the map did NOT need to be
  enlarged despite the owner offering that as an option.
- **Potholes**: `TerrainBuilder.HeightAt()`'s "keep the paved route level"
  block now blends in +/-0.25m of Perlin-noise unevenness near the
  centerline, fading out toward the shoulders (see the `potholes` local var).
- **Texture, two false starts**:
  1. First tried `Assets/YughuesFreePavementsMaterials` (owner-provided asset
     link) - turned out this pack has **no plain asphalt**, only stone/paver
     patterns (herringbone brick, terrazzo, cobblestone). Used "Rough01"
     (cracked grey stone with moss) as the closest fit.
  2. Owner then added **Kajaman's Roads - Free**
     (`Assets/KajamansRoads/`) expecting modular road *pieces* to place along
     the curve. **Important discovery**: this pack is NOT a modular kit -
     it's exactly 2 single, giant, pre-generated meshes (a 10km 2-lane road
     and a 20km 6-lane highway), each with its own fixed pseudo-random curve
     baked into the geometry by Kajaman's road-generator tool. There's no way
     to procedurally align one of these to our custom `PavedRoute` waypoint
     shape from code - that'd need interactive drag/rotate/trim in the Unity
     Editor by a human looking at the scene, which isn't something an AI
     without Editor access can do reliably. **Decision: don't attempt mesh
     placement.** Ended up just using this pack's real diffuse+normal asphalt
     texture (`Textures/Road_2lane_dark02.png` / `_n.png`, has lane markings)
     as the `TerrainLayer` in `TerrainBuilder.PavedRoadLayer()` instead - same
     texture-splat approach as the dirt road, just a much better texture than
     Yughues' stone patterns.
  - **Known limitation, accepted on purpose**: terrain layers tile in world
    X/Z and can't rotate per-segment to follow a curve, so the lane markings
    will be slightly off-angle through the bends. Owner was told this
    explicitly and chose the texture approach anyway over building a full
    mesh-based road system.
- If a **paid** road-meshes need ever comes up again: Kajaman sells a
  "Megapack" with 80+ roads of various types/lengths per the free pack's
  ReadMe - still likely single big meshes per road, not modular tiles, so the
  same "needs manual Editor alignment" caveat would probably still apply.

---

## 2026-07-05 — Lower tree density, wider size range, added Yughues bushes

Three small requests together: density felt like a wall of trunks, trees
next to each other looked like identical copies, and owner added the
"Yughues Free Bushes" pack (5 bush prefabs) to mix in as undergrowth.

**Density eased down** (`MapLayout.cs`): `TreeGridStep` 2.0→2.6,
`ForestTreeDensity`/`PathATreeDensity` 0.9→0.65, `ScaryPathTreeDensity`
1.0→0.85, `FieldTreeDensity` 0.30→0.20. Moderate reduction, not a redesign -
if it's still too dense/sparse, these are the knobs.

**Size variance widened** (`ForestBuilder.cs` `ScatterTrees`): the per-instance
scale `s` was `Random.Range(0.75f, 1.4f)` (deliberately tight, per the
previous entry, to avoid "16m giants"). Widened to `Random.Range(0.65f, 1.8f)`
(~4.5m saplings to ~12.5m old growth off the 7m `RealTreeTargetHeight`
baseline) so neighboring trees are visibly different sizes, not clones.

**Yughues Free Bushes added** (`Assets/YughuesFreeBushes2018/Prefabs/P_Bush01..05.prefab`):
- Same non-URP-shader problem as klen/Dream Tree 2 (Built-in Standard) -
  rewired via `WireYughuesBushMaterial`, reusing `ApplyLeafOrBarkSurface`
  (bushes are treated as 100% foliage, no bark half).
- Each bush prefab also has its own `LODGroup` (same overlap risk as Dream
  Tree 2) - already handled generically since `BakeExternalTree` now takes
  the LOD-detection logic as shared code, not something special-cased per pack.
- `BakeExternalTree` gained a `targetHeight` parameter (was hardcoded to
  `MapLayout.RealTreeTargetHeight`) so bushes can normalize to their own
  `MapLayout.BushTargetHeight` (1.3m) instead of tree height.
- Bushes are **not** mixed into the tree pool/density above - they get their
  own prototype index range and their own scatter pass (`ScatterBushes`,
  `MapLayout.BushGridStep`/`BushDensity`). This required changing
  `ScatterTrees` to return its `List<TreeInstance>` instead of calling
  `TerrainData.SetTreeInstances` itself, so `Build()` can append the bush
  instances to the same list before the one, final `SetTreeInstances` call
  (calling it twice would have each call overwrite/replace the previous one's
  instances, not append).
- **Not yet confirmed by owner**: bush appearance/density in-Editor.

---

## 2026-07-05 — Mixed in klen Maple + Dream Tree 2, and a note on this log going stale

**Heads up for whoever reads this next (AI or human):** `ForestBuilder.cs` and
`MapLayout.cs` had drifted a LOT from what this log described (ForestPack's
tree system was fully replaced by a single-tree `AlanTree.fbx` setup, night
lighting was completely redone to near-total darkness + flashlight-driven
render distances, density/grass logic was rewritten for performance after a
134M-tri/42FPS profiling incident) without any of it being written here. If
you're an AI picking up this project, **read the actual current code before
trusting this log's older entries** - treat everything below the previous
"2026-07-05 — Fixed compile errors" entry as reflecting an earlier state that
the code has since moved past. Whoever/whatever made those changes: please
keep adding entries here so this doesn't happen again - that's the entire
point of this file (see the top of this doc).

**What changed this entry** (`ForestBuilder.cs`): owner added two more tree
asset packs and asked to mix them with the current trees rather than replace them:
- `Assets/klen/` — "HQ Autumn Dry Maple Trees" (10 prefab variants, only 5
  spread across the poly range are used: 468/1952/5423/8631/12338-poly, to
  avoid pooling near-duplicate LODs of the same tree as if they were 5x the
  variety).
- `Assets/DreamTree2/` — "Dream Tree 2 (HDRP)", `Prefab/DreamTree.prefab`
  only (its bundled `grass plant 01/02/03` prefabs are unused).
- Both ship with non-URP shaders (klen: Built-in Standard + a custom
  Built-in-only vegetation shader; Dream Tree 2: HDRP/Lit) that render solid
  magenta as-is in this URP project. Rewired via `WireKlenMapleMaterial` /
  `WireDreamTreeMaterial`, same pattern as the existing `WireALanTreeMaterial`
  (force URP/Lit, assign the pack's own bark/leaf texture by material name,
  alpha-cutout for leaves). The shared cutout/opaque setup logic was factored
  out into `ApplyLeafOrBarkSurface` so it isn't tripled across three near-
  identical functions.
- `Dream Tree 2`'s prefab has an `LODGroup` (lod0/lod1/lod2 all exist as
  simultaneous child renderers) - naively combining every child mesh would
  have stacked all 3 LOD levels into one overlapping mesh. `BakeExternalTree`
  now checks for an `LODGroup` and only combines LOD0's renderers when present.
- All three real-tree sources (AlanTree + 5 klen Maple + 1 Dream Tree 2 = 7
  prototypes) are pooled into one array and picked from uniformly in
  `ScatterTrees` (already-existing `Random.Range(0, realTreeCount)` logic
  needed no changes) - AlanTree's share of the mix drops from 100% to ~1/7
  automatically as a result, satisfying "take some out and mix with the new
  ones" without an explicit removal step.
- Console logs the resulting mix counts on every regenerate
  (`ForestBuilder: tree prototype mix = ...`) - check that if the ratio ever
  needs hand-adjusting (e.g. give AlanTree more weight than 1/7).

---

## 2026-07-05 — Fixed compile errors from Nature Starter Kit 2

Importing "Nature Starter Kit 2" (see below) for its `ground02.tga` dirt
texture also pulled in its old Built-in-Render-Pipeline post-processing
scripts (from ~2016), which don't compile under Unity 6 and blocked Play
mode project-wide. We don't use any of that pack's image-effects system
(project uses URP + a Global Volume instead), so the broken files were just
deleted rather than fixed:
- `Assets/NatureStarterKit2/Standard Assets/Effects/ImageEffects/Scripts/DepthOfField.cs`
  (used the removed `Graphics.DrawProceduralIndirect`)
- `Assets/NatureStarterKit2/Editor/ImageEffects/ColorCorrectionLookupEditor.cs`
  (used the removed `TextureImporterFormat.AutomaticTruecolor`)
- `Assets/NatureStarterKit2/Editor/ImageEffects/DepthOfFieldEditor.cs`
  (orphaned custom-inspector for the `DepthOfField.cs` deleted above)

Remaining CS0618 warnings in `Bloom.cs`/`Tonemapping.cs` (same pack,
`RenderTexture.MarkRestoreExpected` obsolete) are warnings only, not errors —
left alone since they don't block compilation and we don't use those effects.
If more NatureStarterKit2 legacy scripts throw new compile errors later,
same story: check whether we actually use that script (we almost certainly
don't, we only need `Textures/ground02.tga` from this pack) before spending
time "fixing" 2016-era Built-in-RP code instead of just deleting it.

---

## 2026-07-04 — Forest density, real tree/ground assets, path textures

Started from the original map generator (see `MAP_README.md`), which used
procedural primitive trees (sphere+cylinder) and flat-color terrain layers.
Iterated on the look with the project owner. Net state after this session:

**Forest density** (`MapLayout.cs`)
- `TreeGridStep` and the `*TreeDensity` constants were tuned back and forth
  several times (too dense → looked like overlapping balls; too sparse →
  looked bare). Current values: `TreeGridStep = 6f`, `ForestTreeDensity =
  0.30f`, `PathATreeDensity = 0.40f`, `ScaryPathTreeDensity = 0.70f`,
  `FieldTreeDensity = 0.06f`. Adjust here first if density feels off again.

**Real trees** (`ForestBuilder.cs`)
- Added `Assets/ExternalAssets/ForestPack/` (a purchased/downloaded FBX forest
  pack: `ForestPack.fbx` + `Texture/` bark & branch textures + `textures/`
  mat0/mat1 Sketchfab-style ground+grass textures).
- `BuildForestPackTreePrototypes()` instantiates the FBX once, classifies each
  mesh by bounding-box shape (tall & narrow = tree; excludes anything whose
  name contains rock/stone/boulder/ground/terrain/grass/plane, and requires
  `height > width * 1.2`) and bakes each qualifying mesh into its own
  standalone prefab with a `CapsuleCollider`, saved under
  `Assets/_FolkloreArchives/Generated/ForestPackTree_N.prefab`.
- Falls back to the old procedural sphere/cylinder trees if the FBX or no
  qualifying meshes are found (`GreenTreePrefab`/`DryTreePrefab`, still in
  `ForestBuilder.cs`).
- The FBX's own materials point at the original author's disk paths and
  import blank/white. `WireForestPackMaterial()` fills in any material with
  no base texture using `Texture/Bark Texture/Bark01/Bark001_diffuse.png` (+
  normal) for trunks, or `Texture/Branch Texture/Branch1/Branch_albedo.png`
  for anything named leaf/branch/foliage/canopy, and lowers `_Smoothness` to
  kill a "frosty plastic" moonlight sheen on unlit-looking white materials.
- **Not yet confirmed working**: whether the extracted trees render with
  correct bark texture in-Editor (was white/frosty before the material wiring
  fix; last regenerate result not yet reported back).
- Removed the old primitive-sphere riverbank rocks entirely (owner didn't
  want loose rock props).

**Dirt road texture** (`TerrainBuilder.cs`)
- The dirt road (`MapLayout.DirtRoad`, connects the paved route to the
  campsite) got its own 5th terrain layer (`TrailLayer()`), separate from the
  general "Muddy" dirt layer (which is now only used for river banks).
- Tried literal tire-track lines (two thin dirt strips + grass median) via
  alphamap painting — **abandoned**: sub-meter features don't survive
  terrain alphamap resolution or mip-mapping at any real viewing distance, so
  it always read as a blurry uniform patch. Replaced with a single solid
  dirt corridor instead.
- Texture source changed twice: first `Assets/ExternalAssets/ForestPack/textures/mat0_c.jpg`
  (a "dirt+grass+pebbles" blend), then swapped to
  `Assets/NatureStarterKit2/Textures/ground02.tga` (owner imported the
  Unity Asset Store "Nature Starter Kit 2" package specifically for this).
  **Current source of truth: `MapLayout.NatureKitFolder` +
  `/ground02.tga`.**
- Found and fixed a radius mismatch bug: the terrain alphamap dirt band was
  only ~2.2-2.8m wide, but `ForestBuilder.cs`'s grass-detail trimming near
  the same road used a 6m radius — the 3-4m gap between them showed grass
  texture with merely-short grass on top, never any dirt, and that's likely
  what the owner was standing in when reporting "no dirt visible". Both
  radii now match at ~5.6m (`TerrainBuilder.cs` trail `Strip(...)` call and
  `ForestBuilder.cs`'s `onBareDirt` threshold). **Not yet confirmed fixed by
  the owner.**

**Grass** (`ForestBuilder.cs`)
- Height was tripled per owner request (~4.2-8.4m for the tall wild grass
  everywhere except trails/roads).
- Trails (Path A + the "scary" tunnels: Path B, criminal↔secondary,
  grave↔criminals) get a separate short-grass detail prototype instead of
  being bare — only the dirt road itself goes bare-ish (sparse tufts).

**Open items / next to verify with the owner**
1. Confirm the dirt road texture is now visible after the radius fix above.
2. Confirm `ForestPackTree_N` prefabs render with real bark texture, not
   white/frosty.
3. If a console warning like `"... ground texture not found at ..."` ever
   shows up, it means an `AssetDatabase.LoadAssetAtPath` path is wrong for
   that machine/import state — check the exact path in the warning against
   what actually exists on disk before touching anything else.
4. Roadmap from `MAP_README.md` ("Next steps I can generate") is still
   entirely undone: Player 1/Rufus controllers, Luz Mala AI, story/act
   manager, co-op. This session was 100% environment/greybox art polish.

---

## Río curvo + playa de pesca junto al campamento

**Río** (`MapLayout.cs`)
- Era una polilínea de 5 puntos casi recta. Ahora es una curva Catmull-Rom
  suave (misma técnica que `PavedRoute`, vía `BuildSmoothRoute`) con S-bends
  marcadas a partir de `RiverControls`. El agua se ve curva sola porque el
  plano de agua rectangular queda recortado visualmente por el cauce tallado
  en el terreno (solo se ve agua donde el terreno < y=7).
- Hace un acercamiento al oeste hasta x=756 en z=335 (antes 770) para pegar
  la orilla al campamento.

**Playa + sendero** (`MapLayout.cs`, `TerrainBuilder.cs`, `ForestBuilder.cs`)
- Nuevos: `RiverBeach = (730,335)` y `BeachPath = {Campsite, RiverBeach}`.
- `TerrainBuilder.HeightAt`: plataforma arenosa plana a 8.2m (~1m sobre el
  agua) con repecho suave desde el campamento. Usa `Min()` para que solo baje
  el lado de tierra y nunca rellene el cauce.
- `TerrainBuilder.PaintTextures`: sendero de tierra pisada a lo largo de
  `BeachPath` (capa trail/ground02) + arena (capa dirt) en la playa.
- `ForestBuilder`: árboles, arbustos y clutter excluidos del sendero y la
  playa; el pasto del sendero es corto/ralo y la playa queda sin pasto.

**Para verificar tras regenerar**
- Que el agua siga cubierta por el plano (río min x=756, plano cubre 710-890).
- Que la playa quede caminable (rampa campamento 12m → playa 8.2m → agua 7m).

---

## Créditos de assets (atribución obligatoria)

- **PS1 Dog** by *Jo_Zinn5632* — licencia **CC-BY (Creative Commons Attribution)**.
  Fuente: Sketchfab. Uso comercial permitido **acreditando al autor**. El crédito
  debe figurar en los créditos del juego (Steam). Archivo: `Assets/ExternalAssets/Dog/PS1_Dog.glb`.

- **Simple Character PSX** by *JashiPSX* — licencia **CC-BY 4.0**.
  Fuente: itch.io. Uso comercial permitido **acreditando al autor** (crédito en los
  créditos del juego). Archivo: `Assets/ExternalAssets/Player/SimpleCharacterPSX.fbx`.
