# DEV LOG — LA LUZ MALA map generator

Running log of AI-assisted changes to the greybox map generator, kept in this
folder so any AI session (Claude, etc.) working on this project can catch up
on recent context without re-deriving it from scratch. Newest entries on top.
See `MAP_README.md` for the static architecture reference.

---

## 2026-07-28 (30) — Corrección: flySpeed no tomaba el valor nuevo sin Regenerar (**necesita regenerar**)

Owner: "no aumento la velocidad". Mismo bug que ya pasó con
`CarAutoDrive.cruiseSpeedKmh`: `MapExplorer`/`DogController` se agregan a
TEST_PLAYER/DOG (y a los objetos de red) en `TestPlayerBuilder.cs`/
`NetworkBuilder.cs` en el momento de Generate -- si el jugador/perro ya
existían en la escena de una generación anterior, el `flySpeed` quedó
serializado con el valor VIEJO (8), y subir el default en el código a 30
no actualiza ese valor ya guardado.

Fix: `flySpeed = 30f` asignado EXPLÍCITO en los 4 lugares donde se
agregan estos componentes (`TestPlayerBuilder.cs` x2,
`NetworkBuilder.cs` x2), en vez de depender del default de C#. **Requiere
Regenerar** para que el jugador/perro tomen el valor nuevo.

---

## 2026-07-28 (29) — Ajuste: vuelo de debug más rápido

Owner: "va muy lento cuando vuela". `flySpeed` 8 → 30 en `MapExplorer.cs`
y `DogController.cs` (casi 4x). No toca datos horneados -- no hace falta
Regenerar.

---

## 2026-07-28 (28) — Vuelo de debug (doble Espacio, modo creativo Minecraft) para jugador y perro

Owner: "hace que dando doble click con el espacio pueda volar como modo
creativo de minecraft, esto es solo por ahora para recorrer mientras
pruebo el mapa, tanto en perro como jugador 1" -- feature de debug
explícitamente temporal, no de gameplay final.

`MapExplorer.cs` (jugador) y `DogController.cs` (perro, solo
`Mode.Player` -- no tiene sentido que la IA de `Follow` vuele sola):
doble-tap de Espacio (dos `wasPressedThisFrame` dentro de
`doubleTapWindow`=0.3s) prende/apaga `flying`. Volando: sin gravedad,
Espacio MANTENIDO sube, Ctrl/C MANTENIDO baja (esas teclas ya no agachan
mientras se vuela, no tiene sentido en el aire), WASD sigue siendo
horizontal puro a `flySpeed` (8, más rápido que correr) -- mismo
comportamiento que el vuelo creativo de Minecraft. En el perro, si el
modo cambia de `Player` a otra cosa con el vuelo prendido, se apaga solo
(por si se pasa a controlar a la persona sin acordarse de aterrizar).

No toca datos horneados -- no hace falta Regenerar.

---

## 2026-07-28 (27) — Ajuste: altura de los 5 asesinos igualada a la de los amigos (**necesita regenerar**)

Owner: "los asesinos deben medir lo mismo que los personajes no?". Los 5
estaban en 2.2-2.25 (sin comentario que explique por qué), mientras que
los 3 amigos decorativos (`FriendNpcBuilder`) usan 2.3 parejo -- parece
un descuido, no una decisión a propósito. `targetHeight` de los 5
unificado a 2.3. **Requiere Regenerar.**

---

## 2026-07-28 (26) — Movilidad para los 5 asesinos del campamento (**necesita regenerar**)

Owner: "sigamos con movilidad de los asesinos" -- los 5 criminales
enmascarados de `MainCriminalCamp` estaban 100% estáticos desde que se
armaron (sin IA/animación, el comentario del archivo ya lo decía).
Exploramos bajar un asset nuevo con animaciones reales (Sackhead Killer /
Hockey Mask Killer de itch.io) pero el owner eligió la opción sin
descargas: reusar el mismo sistema procedural que ya usan los 3 amigos
decorativos.

Verificado ANTES de tocar código (los FBX son binarios, pero los nombres
de hueso quedan legibles adentro -- grep directo al archivo): los 5
killers del pack "Characters PSX" de Elbolilloduro SON rig Mixamo real
(`mixamorig:Hips/LeftArm/LeftUpLeg/...`, confirmado en los 5 archivos por
igual) -- mismo Limb[] que ya usa `FriendNpcBuilder.MixamoLimbs`.

Fix en `CriminalNpcBuilder.cs`: cada uno de los 5 recibe ahora
`HumanWalkAnim` (corrige la T-pose + anima el ciclo de caminata) +
`FriendWander` (deambulan de a poco cerca de donde arrancan -- no es IA
real, mismo criterio ya usado con los amigos). A diferencia de
`FriendNpcBuilder`, NO se les puso `minGroundY` -- ese piso mínimo existe
ahí por un bug puntual del lado este del mapa (cerca del auto), no
aplica necesariamente al campamento de los ladrones; forzarlo podría
flotarlos si el terreno ahí es más bajo.

**Requiere Regenerar** (agrega componentes en el momento de Build).

---

## 2026-07-28 (25) — Revertida la extensión del mapa 200m al sur

Owner: "volvelo atras como estaba". Revertida por completo la entrada
anterior (extensión del mapa hacia el sur para alejar la ruta del
campamento) -- `MapLayout.cs`, `TerrainBuilder.cs`,
`TerrainPaintPersistence.cs`, `TerrainEditPersistence.cs`,
`ForestBuilder.cs`, `TunnelBuilder.cs`, `EnvironmentBuilder.cs`,
`MountainRingBuilder.cs`, `SilhouetteMountainBuilder.cs` vuelven a su
estado de antes de esa entrada (`MapSize` de nuevo 413, sin
`OriginalMapSize`/`MapOriginZ`, `PavedControls` sin el corrimiento de
-200, Terrain de vuelta en `Vector3.zero`).

Ojo: `MapLayout.cs` tenía además un cambio SIN COMMITEAR hecho aparte
(`YpfPadHalfX` 14→28, `YpfPadFarZ` 34→58 -- lote de la YPF al doble de
tamaño) -- revertido a mano solo lo de la extensión del mapa, sin tocar
ese cambio del lote de la YPF, que sigue en pie.

Si el terreno cacheado ya se había regenerado con la extensión (Rebuild
Terrain forzado de la entrada anterior), hace falta correr **Tools >
Folklore Archives > Rebuild Terrain (forzar)** de nuevo + Regenerar para
que el mapa vuelva a su tamaño/forma original -- este revert de código
no deshace un heightmap ya cacheado en disco.

---

## 2026-07-28 (24) — Mapa extendido 200m al sur: la ruta se aleja del campamento (**necesita Rebuild Terrain completo + regenerar**)

Owner: "necesito alargar la distancia desde la ruta hasta el campamento...
prefiero empujar la ruta mas lejos y agregar terreno etc" -- explícitamente
sin mover el campamento ni nada anclado a él. Planificado con Plan Mode
antes de tocar código (ver `C:\Users\f\.claude\plans\swirling-drifting-patterson.md`)
por el tamaño real del cambio: toda la generación procedural de terreno
asumía implícitamente que el mundo arranca en Z=0 (mismo origen que el
GameObject del `Terrain`) -- extender 200m al sur rompe ese supuesto en
~20 lugares repartidos en 7 archivos.

**3 constantes nuevas en `MapLayout.cs`:**
- `OriginalMapSize = 413f` -- el Z-extent VIEJO, fijo, para fórmulas que
  NO deben estirarse con la extensión (cresta de montañas del borde
  norte, elementos de fondo lejano: anillo de montañas, silueta, agua de
  fondo del río).
- `MapOriginZ = -200f` -- dónde cae la grilla Z=0 del terreno en
  coordenadas de mundo (antes 0, ahora 200m más al sur).
- `MapSize = OriginalMapSize + 200f` (613) -- el Z-extent NUEVO total,
  usado para el tamaño real del `TerrainData` y límites de loop que
  deben cubrir TODO el terreno nuevo (para que la extensión quede
  decorada de verdad, no vacía).

**`PavedControls`** (los 7 puntos de control de la curva de la ruta):
todos los Z corridos -200 -- este es el único lever que mueve la ruta;
todo lo demás que depende de ella (banquina, guardarail, costa del lago,
YPF, puente, túnel, marcadores DirtTurnoff/DifuntaCorrea/GauchitoGil) se
recalcula solo, porque lee `PavedRouteZAt`/`PavedRoute` en el momento, no
cachea una Z vieja.

**Terreno (`TerrainBuilder.cs`):** el GameObject del `Terrain` pasa de
`Vector3.zero` a `(0,0,MapOriginZ)`. Todas las conversiones
índice-de-grilla↔mundo (`ComputeProceduralHeights`, `PaintTextures`,
`ClearGrassOnMud`) suman `MapOriginZ`. `HeightAt`: el umbral de la cresta
norte y el gate oeste/este pasan de `MapSize` a `OriginalMapSize` (fijo)
para no correrse.

**Mismo patrón de conversión** (sumar/restar `MapOriginZ` según sea
índice→mundo o mundo→normalizado) aplicado en: `TerrainPaintPersistence.cs`,
`TerrainEditPersistence.cs`, `ForestBuilder.cs` (4 límites de loop, 4
posiciones normalizadas de `TreeInstance`, 1 `GetInterpolatedHeight`, 2
conversiones de grilla), `TunnelBuilder.cs` (1 `GetInterpolatedNormal`
para el pasto de la entrada). **Fondo lejano fijo** (`OriginalMapSize`,
no debe seguir la extensión): `EnvironmentBuilder.cs` (plano de agua del
río), `MountainRingBuilder.cs`, `SilhouetteMountainBuilder.cs`.

**`CarBuilder.cs` no necesitó tocarse** -- el spawn del auto ya usa
`PavedRoute[^1].x`/`PavedRouteZAt` dinámicamente (fix de la entrada
anterior sobre "la punta real de la ruta"), así que sigue automáticamente
a la ruta reubicada.

**Riesgo real, sin verificar (no tengo acceso visual a Unity):**
`TerrainPaintPersistence`/`TerrainEditPersistence` aplican datos
GUARDADOS (pintado de barro a mano, ediciones de altura a mano) sobre
índices de grilla -- esos índices se guardaron cuando la grilla
representaba el mapa VIEJO (413, origen 0). Con la grilla nueva
(613, origen -200) la escala índice→mundo cambió, así que cualquier
pintado/edición manual guardada previamente puede aparecer desplazada
del lugar donde se dibujó originalmente (mismo tipo de problema que ya
pasó una vez con el lago, ver comentario en `TerrainEditPersistence.cs`
sobre "diffs viejos desalineados contra la base nueva"). Si el barro de
los caminos o alguna edición de altura a mano se ve rara después de
Regenerar, probablemente haga falta re-pintar/re-editar esa parte.

**Sin tocar, puede necesitar ajuste manual:** `MapLayout.TunnelGroupOffset`
es un nudge a mano capturado desde la escena -- el comentario ya
advertía "puede necesitar re-nudge manual"; con la ruta movida, revisar
si el túnel sigue bien alineado.

**Pasos para probar (en este orden):**
1. `Tools > Folklore Archives > Rebuild Terrain (forzar)` -- OBLIGATORIO,
   el terreno está cacheado y un Generate normal NO recalcula tamaño/
   heightmap aunque cambie el código.
2. Generate (tarda ~3.6 min por el rebuild forzado).
3. Revisar en la vista Scene: cresta norte en el mismo lugar de siempre,
   hueco nuevo al sur con terreno/bosque real (no vacío), ruta arrancando
   200m más lejos del campamento.
4. Play: la secuencia de apertura completa (auto+jugador+perro+3 amigos)
   con el tramo más largo hasta la YPF.

---

## 2026-07-28 (23) — Ajuste: crucero a 50 km/h (**necesita regenerar**)

Owner: "pone el auto a 50 kmh". `cruiseSpeedKmh` 40 → 50 en
`CarAutoDrive.cs` (default) y en `CarBuilder.cs` (valor horneado
explícito -- ver entrada de "no esta yendo a 40" más arriba sobre por qué
hace falta tocar los dos). **Requiere Regenerar.**

---

## 2026-07-28 (22) — La ruta sigue mucho más allá del "mapa" -- spawn en la punta real (**necesita regenerar**)

Owner, mostrando la vista Scene: "queda muchisimo espacio hacia atras
fijate la ruta es mas larga que el mapa ponelo en la punta". Tenía mal el
supuesto de fondo: pensé que `MapLayout.MapSizeX` (600, el ancho del
`Terrain`) era el límite físico duro del mundo jugable, pero
`MapLayout.PavedRoute` (la curva real de la ruta, generada por
`RoadsideBuilder.BuildPavedRoadMesh` con su PROPIO `MeshCollider`,
independiente del terreno) tiene puntos de control hasta X=872 -- la
ruta sigue, con colisión real, mucho más allá del terreno "decorado".

Fix en `CarBuilder.cs`: `carX` ahora es
`MapLayout.PavedRoute[^1].x - 15f` (la punta real de la ruta, con un
pequeño margen), en vez de una fórmula atada a `MapSizeX`. Como X ya
queda bien afuera del ancho del `Terrain` (600), `terrain.SampleHeight()`
ya no es confiable ahí (clampea al borde del heightmap, no representa la
altura real) -- reemplazado por `MapLayout.RoadSurfaceHeight` directo,
la misma altura fija que usa el propio mesh de la ruta (independiente del
terreno de abajo, por diseño). `LandmarkBuilder.friendsX` actualizado
igual.

**Requiere Regenerar.** Zona sin decorar (fuera del margen donde
`ForestBuilder`/etc. generan bosque/props) -- esperable que se vea pelada
ahí, el owner ya lo vio en la vista Scene (niebla, agua, montañas
lejanas) y lo pidió así de todos modos.

---

## 2026-07-28 (21) — Corrección 2: "más atrás" iba en la dirección contraria + tope real del mapa (**necesita regenerar**)

Owner: "lo necesito mucho mas para atras unos 200 metros mas". El ajuste
anterior (`MapSizeX - 80f`) iba en la dirección CONTRARIA a lo pedido --
restar MÁS de `MapSizeX` da un X más CHICO, que queda más CERCA de la YPF
(x=449), no más lejos. Confirmado con el owner por las dudas: el mapa
mide 600m y la YPF está en x=449, así que de este lado (este) quedan como
mucho ~80m antes de salirse del terreno generado -- los "200m más" que
pedía no entran de este lado sin cruzar al oeste de la estación
(invertiría la dirección del viaje, lo mismo que el intento fallido de
antes). El owner eligió quedarse del mismo lado, lo más lejos posible sin
salirse: `MapLayout.MapSizeX - 10f` (antes `-30f`, después mal corregido
a `-80f`) -- bien pegado al borde del mapa generado. `LandmarkBuilder.
friendsX` actualizado igual.

**Requiere Regenerar.** Nota: a `MapSizeX - 10f` el spawn queda un poco
afuera del margen donde `ForestBuilder` genera bosque (`[30,
MapSizeX-30]`) -- puede verse más pelado/sin árboles ahí cerca; avisame
si se ve raro.

---

## 2026-07-28 (20) — Corrección: el spawn "oeste" estaba mal entendido, revertido + movido más atrás en el ESTE (**necesita regenerar**)

Owner: "eh? no nada que ver es del lado contrario que lo necesito, osea
donde staba pero mas para atras el tunel ya no sera el principio". La
coordenada X=-22.5 que pasó en el mensaje anterior (leída del Inspector
en la vista Scene) probablemente era posición LOCAL relativa a algún
padre, no la posición MUNDIAL real -- terminé mandando el spawn casi al
extremo opuesto del mapa (cerca del túnel, oeste) en vez de "más atrás"
del lado este donde ya estaba. Revertido `CarBuilder.cs`/
`LandmarkBuilder.cs` al commit anterior (37862d8) con `git checkout`.

Fix real: mismo lado ESTE de siempre, pero más lejos del final del mapa
(`MapLayout.MapSizeX - 80f` en vez de `-30f`) -- viaje más largo hasta la
YPF, sin tocar el túnel ni la dirección del recorrido (yaw/waypoints
vuelven a la versión original, sin invertir nada). 80 es un primer número
a ojo, mismo criterio que el resto de la sesión -- a ajustar en vivo.
`LandmarkBuilder.friendsX` actualizado igual, mismo criterio de siempre
(al lado del auto donde arranca la historia).

**Requiere Regenerar.**

---

## 2026-07-28 (19) — Cambio de spawn del auto: ahora arranca del lado OESTE (**necesita regenerar**)

Owner: "podes hcer que el auto arranque desde ahi?: con todos los
personajes y lo mismo obvio" -- posición elegida a mano en la vista Scene
(Transform del auto en el Inspector: X=-22.5, Z=-295.01), muy lejos del
spawn anterior (borde ESTE del mapa, X≈570, decisión de una sesión
anterior). Este nuevo punto queda cerca/antes del túnel (oeste), casi al
otro extremo del mapa respecto a la estación YPF (x=449).

Esto invierte la dirección de todo el viaje: antes la YPF quedaba al
OESTE del spawn (el auto manejaba hacia X decreciente); ahora queda al
ESTE (X creciente). Cambios en `CarBuilder.cs`:
- `carX` fijo en -22.5 (antes `MapSizeX - 30f`); `carZ` sigue viniendo de
  `PavedRouteZAt(carX)` (no del Z literal del Inspector) para quedar
  pegado a la MISMA curva de ruta que usa el resto del código.
- `yaw`: sacado el `+180°` que compensaba el spawn ESTE -- la fórmula sin
  ese offset ya apunta hacia +X, exactamente lo que hace falta ahora
  (mismo caso por el que se escribió originalmente, cerca del túnel).
- Loop de waypoints de la ruta: invertido de `x -= stepX` a `x += stepX`
  (samplea subiendo en vez de bajando).

`LandmarkBuilder.cs`: `friendsX` (dónde spawnean los 3 amigos decorativos
antes de sentarse en el auto) actualizado al mismo -22.5, mismo criterio
que antes ("al lado del auto donde arranca la historia").

"y lo mismo obvio" -- el resto de la secuencia (jugador/perro
teletransportados a los asientos, auto manejando solo hasta la YPF,
amigos sentados) no necesitó tocarse: todo eso ya es relativo al auto o
se dispara en Play, así que sigue el spawn nuevo automáticamente.

**Requiere Regenerar** (carX es dato horneado en el mapa). Nota aparte:
no verifiqué si la ruta pavimentada (`PavedRouteZAt`) pasa físicamente
por/cerca de un túnel en ese tramo (`TunnelBuilder.cs`, portal en
X=16) -- si el auto choca con algo ahí, avisame.

---

## 2026-07-28 (18) — Ajuste: piernas de FemaleSec atravesaban el auto sentada de adelante (**necesita regenerar, sin confirmar**)

Owner: "a la female cuando va adelanta hay que subirle un poco las
piernas y moverla apenitas para adelante asi no atravieza" (viendo el
modelo atravesando geometría del auto en la vista Scene). Ajuste a ojo,
SIN confirmar en vivo todavía (a diferencia de la mayoría de las poses de
esta sesión, que quedaron horneadas recién después de que el owner
probara en Play y dijera "toma"):

- `seatedThighAngleOverride`: -61° → -72° (muslos más doblados hacia
  arriba).
- `seatPosOverride.z`: 0.3031 → 0.38 (empujón chico hacia +Z, que es
  "adelante" hacia el tablero/parabrisas -- ver `CarBuilder.cs`: `paxBase`
  resta Z para tirar los asientos hacia ATRÁS del volante, así que +Z es
  la dirección contraria, hacia adelante).

**Necesita Regenerar** (son datos horneados en `FriendNpcBuilder.cs`) Y
necesita confirmación del owner en Play -- primer número a ojo, no una
posición ya probada.

---

## 2026-07-28 (17) — Hornea posición confirmada del perro sentado de acompañante

Owner probó al perro sentado de acompañante (`frontPassenger`, el asiento
que le toca después de la gasolinera) en vivo y confirmó la posición
local exacta vía el Inspector: `(0.25001, -1.1808, 0)`. `PlayerVehicleInteractor.
SitRoutine` ahora, después de reparentar al perro al asiento, si el
asiento es `car.frontPassenger`, pisa la fórmula genérica con este valor
horneado directo (mismo criterio que `Seat_RearMid` en `CarBuilder.cs`:
un asiento con geometría propia es más confiable horneado a mano que
persiguiendo la fórmula general del resto de los asientos). Puro código,
sin datos horneados en el mapa -- no hace falta Regenerar.

---

## 2026-07-28 (16) — Fix: las puertas quedaban abiertas para siempre tras el 2do embarque

Owner: "no se me esta dejando cerrar la puierta luego de que se suben
todos de nuevo al auto, y cuando se suben todos de nuevo al auto las
puertas traseras deben cerrarse solas". Las 5 puertas se abren TODAS en
el paso 3 de `OpeningDriveSequence` (para que jugador+perro+3 amigos se
bajen en la YPF) -- pero las 3 traseras nunca se volvían a cerrar,
porque los amigos se re-sientan con un teleport/reparent directo
(`ReseatFriend`), sin pasar por ninguna interacción de puerta que las
cierre.

Fix: al final del paso 6 (cuando jugador+perro ya están sentados de
verdad como conductor+acompañante y los 3 amigos se re-sentaron atrás),
`OpeningDriveSequence` cierra las 5 puertas del auto directamente vía
`carDoors.SetDoor(d, false)`. Puro código, sin datos horneados -- no hace
falta Regenerar.

---

## 2026-07-28 (15) — Fix: no aparecía la opción de cerrar la puerta durante la secuencia de apertura

Owner: "no me esta saliendo la opcion de cerrar la puerta" (sentado
durante el viaje en autopiloto hacia la YPF). Causa: `OpeningDriveSequence`
llama `SitRoutine(car, seat, null)` -- sienta directo, sin pasar por abrir
una puerta con E. `myDoor` quedaba en `null`, y `LookingAtDoor(myDoor)`
chequea explícitamente `door == null` y devuelve `false` siempre -- nunca
ofrece abrir/cerrar, sin importar hacia dónde mires, y `[E]` solo baja.

Fix: si `SitRoutine` no recibe una puerta explícita, usa `NearestDoor(c,
seat.position)` (la misma función que ya usaba `PreferredDoor` en otro
lado) para tener igual una referencia razonable -- así abrir/cerrar la
puerta funciona aunque te hayan sentado sin pasar por el flujo manual.
Puro código, sin datos horneados -- no hace falta Regenerar.

---

## 2026-07-28 (14) — Fix real: un cordón invisible en el asfalto de la YPF trababa al auto (**necesita regenerar**)

Owner: "sigue frenandose en la entrada" -- y al preguntar específicamente
qué hacía, confirmó: "Choca/queda trabado contra algo físico" (no que
decidía frenar). Todos los ajustes de las últimas ~6 entradas (velocidad,
steerGain, piso de velocidad, geometría del giro) estaban afinando un
sistema de waypoints que en realidad NUNCA fue el problema -- el auto se
la pasó chocando contra un collider invisible.

Causa real: `AreaPoiBuilder.YpfStation()` arma el "PlayonAsfalto" (el
piso de asfalto visual del lote) con `CreatePrimitive(Cube)`, que deja
puesto un `BoxCollider` por default -- a diferencia de otros props
decorativos de este mismo archivo que sí llaman `DestroyCol()` después
(la vidriera de la tienda, la boya, etc.), acá nunca se sacó. Ese
collider cubre TODO el lote (X 437-461, Z roadZ+11 a roadZ+33) con un
borde de ~0.3m de alto justo en la línea que el auto tiene que cruzar
para entrar desde la ruta -- un cordón invisible actuando de tope físico,
exactamente donde está el waypoint de "entrada al lote". El terreno de
abajo ya está aplanado a esa altura por `TerrainBuilder.HeightAt()`, así
que el cubo es puramente visual -- no necesitaba colisión propia.

Fix: `DestroyCol(playon)` después de crearlo, mismo patrón que ya usan
los demás props no-sólidos de este archivo. **Esto rehornea geometría del
mapa -- hace falta Regenerar.**

Lección para no repetir: cuando algo se describe como "choca" o "queda
trabado", conviene preguntar primero si es un problema de LÓGICA (índice
de ruta, velocidad, frenado) o de COLISIÓN FÍSICA con algo en la escena,
antes de seguir afinando constantes de comportamiento que puede que nunca
hayan sido la causa.

---

## 2026-07-28 (13) — Fix: piso de velocidad durante el giro (el steerGain solo no alcanzaba)

Owner (3ra vuelta): "sigue trabandose". El steerGain triplicado (entrada
anterior) no sirve de nada si la velocidad cae por debajo de 0.3 m/s --
`CarController.FixedUpdate()` ni siquiera intenta girar bajo ese umbral
(`if (Mathf.Abs(speed) > 0.3f)`), sin importar cuánto steer se le mande.
El tapering de `targetSpeed` baja hacia CERO a medida que `remaining` se
achica, pero `remaining` mide distancia hasta el FINAL (no si el auto ya
terminó de girar) -- así que la velocidad objetivo podía caer casi a cero
todavía a mitad del giro, mucho antes de necesitarlo, dejando al auto sin
velocidad Y sin poder girar al mismo tiempo.

Fix: piso de velocidad (4 m/s, ~14 km/h) mientras `braking` es cierto Y
todavía queda algún waypoint por delante que no sea el último -- no deja
caer la velocidad por debajo de lo necesario para retener autoridad de
giro hasta terminar el giro de verdad. Recién en el ÚLTIMO tramo (entrada
→ estacionar, ya en línea recta, sin más giros) el tapering baja del todo
hasta pararse. Puro código, sin datos horneados -- no hace falta
Regenerar.

---

## 2026-07-28 (12) — Fix: por qué frenar antes del giro también le sacaba fuerza para girar

Owner: "se sigue quedando trabado ahora si va a 40" (después de subir la
velocidad y frenar antes del giro cerrado, fix de la entrada anterior).

Causa real: `CarController.FixedUpdate()` escala la capacidad de GIRO con
la velocidad actual -- `turn = steer * turnRate *
Clamp01(Abs(speed)/maxSpeed) * dir`. A velocidad CERO, el auto no gira
NADA; a `maxSpeed`, gira a full. El fix anterior (frenar ANTES del giro
cerrado para no pasarlo de largo) reduce la velocidad -- pero eso mismo
le saca autoridad de giro justo en el momento en que más la necesita para
completar un giro cerrado de 5m. "Frenar para no pasarse de largo" y
"tener velocidad para poder girar" tiraban de la MISMA perilla en
direcciones opuestas -- por eso las últimas rondas de ajuste no
convergían pase lo que pase con la velocidad sola.

Fix: en vez de seguir peleando con la velocidad, `CarAutoDrive` ahora
TRIPLICA `steerGain` (compensación de autoridad de giro) mientras está
dentro de la zona del lote (`inLotZone`) -- así el auto puede cerrar el
giro apretado aunque venga frenando. Fuera del lote, `steerGain` normal
(1x) sin cambios. Puro código, sin datos horneados -- no hace falta
Regenerar.

---

## 2026-07-28 (11) — Corrección importante: cambiar defaults de CarAutoDrive SIEMPRE necesita Regenerar

Owner: "no esta yendo a 40". Encontrado el motivo: `cruiseSpeedKmh` (y
`arriveRadius`/`steerGain`/`slowdownDistance`) son campos públicos de
`CarAutoDrive` que `CarBuilder` NUNCA asigna explícitamente -- toman el
valor default de C# solo en el instante en que `Generate` los agrega al
auto (`AddComponent`). Una vez que el auto YA existe en la escena
(generado antes), recompilar el script NO actualiza ese valor -- el
componente serializado se queda con el número que tenía guardado desde la
última vez que se generó. **Corrijo entradas anteriores de HOY: decir
"puro código, no hace falta regenerar" para cambios de estos defaults
estuvo MAL** -- la lógica que los usa sí es código puro, pero el VALOR en
sí depende de cuándo se generó el auto por última vez.

Fix concreto: `cruiseSpeedKmh = 40f` ahora se asigna EXPLÍCITO en
`CarBuilder.cs` (no solo como default en `CarAutoDrive.cs`), para que
quede claro en el código que este número se hornea en Generate. **Regla
general para el resto de la sesión: cualquier cambio a un campo público
de `CarAutoDrive`/`CarController` usado por el auto autopiloteado
necesita Regenerar para tomar efecto, incluso si el cambio "es solo un
número".**

---

## 2026-07-28 (10) — Ajuste: 40 km/h de crucero + frenar ANTES del giro cerrado, no solo al entrar

Owner: "necesito que vaya a 40kmh y ahora se esta trabando de nuevo
contra la entrada de la ypf". `cruiseSpeedKmh` 20→40. Con la velocidad de
crucero más alta y la zona de frenado empezando recién en la entrada real
al pavimento, el auto llegaba al waypoint de GIRO (5m antes, cerrado)
todavía a 40 km/h -- muy rápido para completar un giro tan cerrado,
terminaba chocando/atascado contra la entrada de la estación. Fix:
`inLotZone` ahora incluye también el waypoint de giro (últimos 3:
giro + entrada real + estacionar), no solo los últimos 2 -- empieza a
soltar velocidad ANTES de encarar el giro cerrado, no recién al entrar al
pavimento. Puro código, sin datos horneados -- no hace falta Regenerar.

---

## 2026-07-28 (9) — Corrección: giro vuelto a 5m + fix real de la velocidad de crucero (**necesita regenerar**)

Owner: "lo hiciste mal deberia ser a los 5m no a los 30m" / "y de
velocidad sigue iguawl necesito que todo el trayecto desde donde arranca
hasta llegar a la ypf vaya mas lento el auto".

**Giro a 5m:** el intento anterior (30m, tres pasos) se pasó de largo --
vuelto a un giro cerca de la estación (5m, un solo punto intermedio a
mitad de camino) como pidió el owner explícitamente. Lo que hace que un
giro tan cerrado sea completable no es más anticipación de runtime (ya
demostrado frágil, ver entradas de arriba) sino que el auto ahora llega
mucho más lento a la YPF (ver el punto siguiente).

**La velocidad "sigue igual" -- causa real encontrada:** bajar
`cruiseThrottle` (0.55→0.4 en el ajuste anterior) no cambió NADA la
velocidad real observable, porque `CarController.Update()` NUNCA usa el
VALOR del throttle -- solo su signo: `if (throttle > 0.1f) speed =
MoveTowards(speed, maxSpeed, accel*dt)` acelera a FONDO hacia `maxSpeed`
sin importar si el throttle es 0.11 o 1.0. `cruiseThrottle` en
`CarAutoDrive` era un número completamente ignorado por la física en la
práctica -- moverlo de 0.55 a 0.4 no tenía forma de frenar nada.

Fix real: `CarAutoDrive` reemplaza `cruiseThrottle` por `cruiseSpeedKmh`
(20 km/h, primer número a ajustar en vivo) y un control de velocidad
objetivo -- acelera (throttle=1) mientras esté por debajo del objetivo,
CORTA el acelerador (throttle=0, no un número intermedio que igual
acelera a fondo) al alcanzarlo. Mismo patrón que ya usaba el frenado en
el lote (comparar contra una velocidad objetivo), ahora aplicado a TODO
el trayecto, no solo la zona de frenado final.

**Esto rehornea la ruta -- hace falta Regenerar.**

---

## 2026-07-28 (8) — Rediseño: giro hacia la YPF más temprano y gradual (**necesita regenerar**)

Owner: "nop. se sigue trabando hace dos commits funcionaba bien pero
dobla muy despues deberia doblar antes osea entrar antes a la ypf, y
frenar ni bien entra". Patrón detectado en los últimos commits: agrandar
el radio de anticipación de runtime (`nearForAim`, en `CarAutoDrive.cs`)
para que el auto "mire" el giro más temprano es frágil -- cuanto más
grande ese radio, más fácil que corte camino y el índice de ruta quede
trabado (2 rondas de esto ya, ver entradas de arriba). El problema real
no era cuándo el auto empieza a MIRAR el giro, sino que la geometría del
giro en sí (horneada en `CarBuilder.cs`) seguía siendo la misma, apretada
en 14m.

Fix:
- `CarAutoDrive.cs`: `nearForAim` vuelto a 2.5x `arriveRadius` (el valor
  que funcionaba sin trabarse). El "doblar antes" ahora se resuelve con
  geometría, no con más lookahead de runtime.
- `CarBuilder.cs`: el giro hacia el lote arranca 30m antes de la estación
  (antes 14m) y se reparte en TRES pasos graduales en vez de dos -- el
  auto empieza a desviarse de la ruta mucho más temprano y con ángulos
  más suaves en cada paso.
- `CarAutoDrive.cs`: "frenar ni bien entra" -- la zona de frenado
  (`inLotZone`) ahora son solo los ÚLTIMOS 2 waypoints (la entrada real al
  pavimento + el punto de estacionar), NO los 2 pasos previos del giro
  (que ahora están lejos, todavía acomodando el rumbo, no adentro del
  lote todavía) -- el auto sigue a velocidad crucero durante el giro y
  recién frena de verdad al llegar a la entrada real.

**Esto rehornea la ruta -- hace falta Regenerar el mapa.** Si los cambios
anteriores de esta sesión sobre `CarBuilder.cs` (giro en dos pasos, 14m)
tampoco se habían regenerado todavía, puede que parte de la rareza
reportada ("se sigue trabando") viniera de estar corriendo la lógica
nueva de `CarAutoDrive.cs` contra datos de ruta VIEJOS -- vale la pena
confirmar que se regeneró después de este cambio en particular.

---

## 2026-07-28 (7) — Fix: índice de ruta trabado otra vez (criterio "pasado" dependía del morro)

Owner: "ahora se esta quedando trabado nuevamente" -- después de agrandar
el radio de anticipación a 4x `arriveRadius` (~32m, ajuste anterior), el
criterio de "waypoint pasado" (`Vector3.Dot(transform.forward,
toTargetNow) < 0`) dejó de servir: con tanta anticipación el auto empieza
a curvar hacia el SIGUIENTE punto muy pronto, así que el waypoint actual
puede terminar bien al COSTADO en vez de atrás -- y como el morro gira
siguiendo el volante, puede seguir "mirando" hacia delante de él
indefinidamente sin que el producto punto se vuelva negativo nunca.
Índice trabado de nuevo, mismo síntoma que antes por una causa distinta.

Fix: criterio de "pasado" que no depende de hacia dónde apunta el auto en
este instante -- proyección sobre la dirección del TRAMO de ruta (waypoint
anterior → actual). El auto se considera que pasó el punto si, medido a
lo largo de esa línea (no del morro), ya lo dejó atrás. Puro código, sin
datos horneados -- no hace falta Regenerar.

---

## 2026-07-28 (6) — Ajuste: doblar/frenar antes en la YPF + crucero más lento

Owner: "ahora si frena etc pero deberia doblar antes y frenarse antes" /
"y de camino ir mas lento". Ya no era un bug -- la secuencia funciona,
solo afinando valores en vivo (mismo criterio que el resto del auto en
esta sesión):

- `cruiseThrottle`: 0.55 → 0.4 (más lento en la ruta principal).
- `slowdownDistance`: 25 → 45 (más margen para empezar a soltar
  velocidad).
- radio de anticipación para mirar al siguiente waypoint: 2.5x →
  4x `arriveRadius` (~32m, dobla con más margen antes del giro cerrado).
- `inLotZone` (dónde empieza a considerar el frenado): últimos 3
  waypoints → últimos 4 (incluye un tramo más de ruta antes del giro).

Puro código, sin datos horneados -- no hace falta Regenerar.

---

## 2026-07-28 (5) — Fix: el auto no frenaba nunca en la YPF (índice de ruta trabado) + doblaba tarde

Owner: "sigue yendose de largo el auto no frena nunca cuando entra a la
ypf" / "y dobla muy tarde deberia doblar antes".

**No frena nunca:** efecto secundario del fix anterior (mirar al
SIGUIENTE waypoint cerca del actual, para evitar el ángulo ruidoso). Con
eso, el auto podía empezar a girar hacia el siguiente punto ANTES de
cerrar la distancia al actual ("corner cutting" -- corta camino por
adentro de la curva) y terminar pasándolo de largo sin nunca entrar en su
`arriveRadius`. Como el avance de `_index` dependía SOLO de esa distancia,
quedaba trabado ahí para siempre -- nunca llegaba a `inLotZone` (que mira
el índice, no la posición real), así que la lógica de frenado nunca se
activaba por más que el auto físicamente ya hubiera pasado ese punto.
Fix: `_index` ahora también avanza si el waypoint quedó DETRÁS del auto
(producto punto negativo entre `transform.forward` y el vector hacia el
waypoint), sin importar la distancia -- garantiza que el índice siempre
progresa a medida que el auto efectivamente avanza por la ruta.

**Dobla muy tarde:** el auto recién empezaba a mirar hacia el siguiente
waypoint a 1.5x `arriveRadius` (12m) de distancia -- muy poco margen para
acomodar el rumbo antes de un giro cerrado como el de entrada al lote.
Separado en dos radios: uno más grande (2.5x, ~20m) solo para EMPEZAR a
mirar hacia el siguiente punto con más anticipación, y el chico (1.5x) se
guarda nomás para soltar el volante del todo bien cerca del waypoint
FINAL (ahí sí hace falta estar pegado, es donde frena de verdad).

Puro código, sin datos horneados -- no hace falta Regenerar.

---

## 2026-07-28 (4) — Fix: el auto se ponía a girar y atravesaba objetos en los giros nuevos

Owner (después de regenerar con el fix anterior): "ahora el auto sigue de
largo y atraviesa todo" / "y se pone a girar". El fix de "se pone a girar"
de esta misma sesión (más abajo) solo apagaba el steer cerca del ÚLTIMO
waypoint (`finalApproach`) -- correcto ahí porque ya no hay más a dónde
girar, solo frenar derecho. Pero la entrada anterior de HOY agregó DOS
waypoints de giro NUEVOS que NO son el último -- cerca de esos, la misma
lógica no se activaba, así que el ángulo hacia un punto pegado al auto
volvía a ser ruidosísimo (el bug original) y el auto giraba en el lugar,
rozando/atravesando lo que tuviera cerca.

Fix en `CarAutoDrive.cs`: generalizado a un esquema tipo "pure pursuit" --
cerca de CUALQUIER waypoint que no sea el último, el auto mira hacia el
SIGUIENTE de una vez (ya va para allá) en lugar de fijar la mirada en el
punto que está a punto de pasar -- da un ángulo estable en vez de
ruidoso, y de paso el volante nunca se suelta a mitad de un giro (lo que
antes hacía que seguiera derecho sin doblar). El "soltar el volante del
todo" ahora es exclusivo del waypoint final, sin más a dónde girar. Puro
código, sin datos horneados -- no hace falta Regenerar.

---

## 2026-07-28 (3) — Fix: el auto pasaba de largo el giro hacia la YPF (**necesita regenerar**)

Owner: "SE SIGUE TRABANDO Y ANDANDO PARA DELANTE EL AUTO NO ESTA ENTRANDO
AL PAVIMENTO Y FRENANDO POR LO QUE NADIE BAJA" -- después del fix anterior
(frenado solo en los últimos waypoints, ya no en toda la ruta), apareció
un problema distinto: con UN SOLO waypoint de giro hacia el lote, el auto
tenía que saltar ~12m de lado (Z, `YpfPadNearZ+2`) en muy pocos metros de
avance (X, el tramo entre `turnInX` y `YpfStation.x`, apenas 6m) -- un
giro demasiado cerrado para completarlo a velocidad crucero con el steer
clampeado ±1. Lo pasaba de largo sin que `dist` bajara nunca de
`arriveRadius`, así que `_index` nunca avanzaba al waypoint del lote --
seguía "apuntando" para siempre a un punto que ya había pasado de largo,
de ahí el "trabado andando para delante" sin fin.

Fix en `CarBuilder.cs`: `turnInX` más lejos (`YpfStation.x - 14f` en vez
de `-6f`, más metros de avance disponibles para girar) + el giro partido
en DOS waypoints más suaves en vez de uno solo (giro 1 a mitad de camino,
giro 2 ya adentro del lote). `CarAutoDrive.cs`: la zona de frenado
(`inLotZone`) ahora cubre los últimos 3 waypoints (los 2 giros + el punto
de estacionar) en vez de 2, para que el auto entre más lento a la zona de
giro y el steer tenga margen de completarlo. **Esto rehornea la ruta en
`CarBuilder.Build()` -- hace falta Regenerar el mapa para que tome
efecto** (a diferencia del fix de throttle=0 anterior, que era código puro
sin datos horneados).

---

## 2026-07-28 (2) — Fix: frenado prematuro/interminable en YPF + cámara del perro atraviesa al humano

Owner: "cuando miro desde la camara del perro veo atravez del humano, capaz
que move apenias el perro para el lado del greenmale solo apenitas asi al
girar la camara no lo veo atravez" / "y al llegar a la estacion se frena
pero se frena antes de entrar al pavimento y no frena del todo el auto se
queda trancado andando y nadie se baja".

**Perro atraviesa al humano:** `Seat_RearMid` (x=-0.1558 local del auto)
quedó, horneado a mano, del lado del jugador (`rearLeft`, x=0) en vez de a
mitad de camino real entre los dos asientos traseros -- la cámara del
perro terminaba pegada al cuerpo del humano al girar hacia él. Fix
(siguiendo la sugerencia exacta del owner): `PlayerVehicleInteractor` nueva
constante `DogSeatedSideOffset` (0.15, primer número a ajustar en vivo) que
empuja tanto el CUERPO como el OJO de la cámara del perro hacia el lado de
`rearRight`/MaleGreenJkt (+X local del auto). Ojo: la cámara se reparenta
DIRECTO al asiento (no al transform del personaje), así que hubo que
correr su `localPosition` aparte del offset del cuerpo -- moviendo solo
uno de los dos no alcanzaba.

**Frenado en la YPF:** dos bugs en `CarAutoDrive.cs`. (1) "se frena antes
de entrar al pavimento": `remaining` sumaba TODOS los tramos que faltan,
incluido el último tramo de RUTA (antes de doblar hacia el lote) -- si ese
tramo + el giro + el estacionamiento ya sumaban menos de
`slowdownDistance`, el auto frenaba todavía en la ruta. Ahora la zona de
frenado solo se activa en los últimos 2 waypoints horneados por
`CarBuilder` (el giro hacia adentro del lote + el punto de estacionar);
en la ruta, siempre a velocidad crucero. (2) "no frena del todo... queda
trancado andando": el throttle de "acercamiento suave"
(`cruiseThrottle*0.3`) nunca llegaba a CERO, así que muy cerca del punto
final el auto seguía reptando para siempre sin entrar nunca en el radio de
"llegada" (`HasArrived`) -- por eso tampoco se abrían las puertas ni bajaba
nadie. Ahora, adentro de `arriveRadius` del último waypoint, el throttle
pasa a 0 directo (frena solo por resistencia) en vez de seguir empujando.

---

## 2026-07-28 — Fix: el perro "camina" con las patas mientras está sentado

Owner: "de la vista del humano veo al perro moviendo los pies como si
estuviera caminando mientras esta sentado". Efecto secundario directo del
fix anterior del reparentado del cuerpo (ver entrada del reparentado más
abajo): `DogWalkAnim.LateUpdate()` detecta "me estoy moviendo" únicamente
por el delta de `transform.position` cuadro a cuadro, sin ninguna noción
de "estoy sentado". Antes, el perro sentado se quedaba clavado en el
mundo (por eso nunca animaba caminar); ahora que el cuerpo viaja de
verdad con el asiento del auto (reparentado), esa posición cambia todo el
tiempo aunque esté quieto en la silla -- el script lo interpreta como
"caminando" y anima las 4 patas con el swing de siempre.

Fix: `PlayerVehicleInteractor` cachea `DogWalkAnim` en `EnsureInit()` y lo
apaga (`dogWalkAnim.enabled = false`) en `SitRoutine`, restaurándolo
(`= true`) en `ExitRoutine` -- mismo patrón ya usado para `DogController`
(`dog.enabled`). No hace falta regenerar el mapa, es cambio de código
puro.

Nota aparte (no arreglado todavía): el owner también reportó que "desde
la vista del perro atravieso el cuerpo cuando lo miro del humano" -- la
cámara en primera persona no tiene colisión propia contra otros
personajes, así que a corta distancia atraviesa el modelo del otro. Es
una limitación general de este sistema de cámara libre (no algo que rompió
ningún fix de esta sesión); no se tocó nada todavía, a la espera de que
el owner confirme si quiere que se le agregue colisión.

---

## 2026-07-26 — Fix: layer "SelfHidden" compartido ocultaba a los DOS entre sí

Owner: "sigo sin ver al perro desde la camara del humano y sigo sin ver al
humano desde la camara del perro" (y confirmó, aparte, que el fix anterior
del reparentado SÍ funcionó: "se ve que si se mueven con el auto porque al
bajarme luego si estan"). Bug real, no falta de regenerar: `SelfHiddenLayer`
era un layer ÚNICO Y COMPARTIDO -- cuando la persona Y el perro están
sentados a la vez, cada uno pone su PROPIO modelo en ESE MISMO layer para
ocultarse de su PROPIA cámara (`camComp.cullingMask &= ~(1 << hidden)`),
pero como el layer es el mismo número para los dos, la cámara de cada uno
termina excluyendo TODO ese layer -- oculta también al otro, no solo a sí
mismo.

Fix: `LayerSetup.SelfHiddenLayerDog` (layer nuevo, aparte del `SelfHidden`
que sigue usando la persona). `PlayerVehicleInteractor.
SelfHiddenLayerName` pasa de `const` COMPARTIDA a campo `public
selfHiddenLayerName` POR INSTANCIA -- `TestPlayerBuilder.cs` (modo solo,
el que se prueba esta sesión) y `NetworkBuilder.cs` (modo red) le asignan
el layer del perro a su interactor específicamente. `NetworkBuilder.
EnsureNet()` ahora crea los DOS layers (antes solo uno). Necesita
regenerar.

---

## 2026-07-26 — Fix real: el cuerpo nunca se reparentaba al auto (solo la cámara)

Owner: "se queda ahi parado el perro no se mueve con el auto... pero su
camara si". Bug de fondo, distinto de los anteriores: `SitRoutine`
reparenta la CÁMARA al asiento (`cam.SetParent(seat, false)`, más abajo en
la corrutina), pero el CUERPO (`transform` de la raíz) solo se
REPOSICIONA una vez (`transform.position = seat.position - ...`), nunca se
reparenta -- en cuanto el auto arranca a andar, el cuerpo queda clavado en
el mundo donde estaba el asiento en ESE instante, mientras la cámara (sí
reparentada) lo sigue perfecto. Nunca se había notado con el jugador
normal porque no ve su propio cuerpo sentado (self-hide) -- con el perro
visible desde afuera (la persona lo mira de lejos) sí se nota.

Fix en `PlayerVehicleInteractor.cs`: `SitRoutine` ahora también
`transform.SetParent(seat, true)` (el cuerpo entero pasa a ser hijo del
asiento, que sí es hijo del auto -- se mueve con él). `ExitRoutine`
desparenta (`SetParent(null, true)`) antes de calcular la posición de
bajada, para no quedar relativo al auto en el mundo.

⚠ No probado en modo RED (co-op/hosted) -- reparentar la raíz de un
objeto con NetworkTransform podría necesitar atención aparte si algún día
se prueba con host. Necesita regenerar.

---

## 2026-07-26 — Fix: perro que no se mueve del spawn + auto girando al llegar

Owner: "aparece el perro pero atraviesa el auto y se queda en la misma
posicion que spawneo... el auto al llegar a la gasolinera no frena se pone
a girar". Dos más:

1. **Perro sin moverse:** no era solo la cámara (fix anterior) -- CUALQUIER
   referencia cacheada en `Start()` (`cc`, `dog` controller, etc.) corría
   el mismo riesgo de no estar lista si `OpeningDriveSequence` llama a
   `SitRoutine` antes de que el propio `Start()` de ESE objeto corriera.
   Sacada toda la inicialización a `EnsureInit()` (idempotente), llamado
   desde `Start()` Y defensivamente al principio de `SitRoutine`.
2. **Auto girando al llegar:** con el waypoint final muy cerca, la
   dirección hacia él se vuelve ruidosa (un pasito de más y el ángulo
   salta) -- el steer clampeado a ±1 lo hacía girar en el lugar tratando
   de corregir sin parar. `arriveRadius` subido (5→8) y, MUY cerca del
   último waypoint, deja de perseguir el ángulo exacto (steer=0, solo
   frena derecho) en vez de perseguir un punto tan puntual.

Necesita regenerar.

---

## 2026-07-26 — Causa raíz real: un solo crash tumbaba TODA la secuencia

Owner mandó el error completo de la Console (mucho más útil que adivinar):
`NullReferenceException` en `PlayerVehicleInteractor+<Glide>d__54.MoveNext()`
(`PlayerVehicleInteractor.cs:544`), disparado desde
`dog.StartCoroutine(dog.SitRoutine(...))` en `OpeningDriveSequence.cs:75`.
El fix anterior (`GetComponentInChildren<Camera>(true)`) no alcanzó a
tiempo -- `cam` seguía null para el perro en ese momento.

**El hallazgo importante:** `dog.StartCoroutine(dog.SitRoutine(...))`
corre TODO el arranque de esa corrutina (hasta el primer yield adentro de
`Glide`) de forma SINCRÓNICA, como parte de la llamada -- confirmado por
el stack trace, que muestra `Run()` (de `OpeningDriveSequence`) como
llamador directo. Como no había ningún try/catch en el medio, la
excepción se propagaba hacia ARRIBA y tumbaba TODA la corrutina `Run()`,
no solo la del perro -- por eso el auto TAMPOCO frenaba: la secuencia
nunca llegaba a la parte que activa `autoPilot`/`CarAutoDrive`.

**Fix, dos capas:**
1. `SitRoutine` ahora vuelve a buscar la cámara (`GetComponentInChildren
   <Camera>(true)`) justo antes de necesitarla si `cam` sigue null a esa
   altura -- no depende de cuándo corrió `Start()`.
2. `Glide()` ahora chequea `tr == null` al entrar y sale con `yield break`
   + un `Debug.LogError` en vez de tirar la excepción -- así una falla
   puntual en UN personaje no puede tumbar toda la secuencia de apertura
   de nuevo.

Necesita regenerar.

---

## 2026-07-26 — Fix: linterna prendida adentro + perro que no se sienta + frenado débil

Owner: "tengo la linterna prendida dentro del auto no deberia pasar eso...
el perro no esta sentado en el medio... el auto al llega no frena y esta
yendo demasiado rapido". Tres bugs:

1. **Linterna:** solo se apagaba si te sentabas de CONDUCTOR (`c.driving`)
   -- como en la secuencia de apertura el jugador va atrás, nunca se
   apagaba. Ahora se apaga al sentarse en CUALQUIER asiento (y se restaura
   siempre al bajar, no solo si manejabas -- los faros del auto sí siguen
   siendo solo del conductor).
2. **Perro sin sentar -- causa real encontrada:** `PlayerVehicleInteractor.
   Start()` buscaba la cámara con `GetComponentInChildren<Camera>()`, que
   por defecto IGNORA objetos desactivados. La cámara del perro
   (`DogCamera`) arranca DESACTIVADA (el juego empieza controlando a la
   persona, ver `PartyController`) -- nunca la encontraba, `cam` quedaba
   `null`, y `SitRoutine` se rompía en silencio (`NullReferenceException`
   adentro de `Glide`) apenas intentaba sentar al perro. Cambiado a
   `GetComponentInChildren<Camera>(true)` (incluye inactivos). Este bug
   probablemente ya existía antes de esta feature (afecta también sentar
   al perro con E normal, no solo la secuencia de apertura).
3. **Frenado débil:** soltar el acelerador solo desacelera con
   `coastDecel` (4, suave) -- adentro de la zona de frenado ahora
   `CarAutoDrive` compara la velocidad ACTUAL contra una curva de
   velocidad objetivo (según distancia restante) y aplica throttle
   NEGATIVO (frenado real, `brakeDecel`=22, mucho más fuerte) si la va
   superando.

Necesita regenerar.

---

## 2026-07-26 — Fix: jugador "encima del malegreen" + auto que no frena y choca

Owner: "aparezco sentado encima del malegreen no como el orden que te habia
dado... y al llegar a la ypf no frena el auto choca". Dos bugs reales:

1. **Overlap real, no solo mal ajustado:** al reasignar los 3 amigos a
   asientos nuevos (entrada anterior), los `seatPosOverride` VIEJOS
   quedaron puestos -- son posiciones ABSOLUTAS, no relativas al asiento
   que se les pasa, así que cada uno seguía clavado en las coordenadas de
   SU asiento ANTERIOR sin importar a qué seat apuntara el código.
   `Friend_MaleGreenJkt` seguía en las coordenadas de `rearLeft` (su
   asiento viejo) -- exactamente donde ahora se sienta el jugador real.
   Encontrado el atajo: la posición VIEJA de `Friend_MaleCasual`
   (calibrada para `frontPassenger`) es justo la que necesita
   `Friend_FemaleSec` ahora, y la posición VIEJA de `Friend_FemaleSec`
   (calibrada para `rearRight`) es justo la que necesita
   `Friend_MaleGreenJkt` ahora -- las intercambié entre los dos.
   `Friend_MaleCasual` (nuevo, al conductor, sin precedente) se quedó sin
   override, cae al fallback de la fórmula.
2. **Frenado insuficiente:** `CarAutoDrive` solo medía la distancia de
   frenado contra el ÚLTIMO tramo (waypoint a waypoint) -- el giro nuevo
   hacia el lote de la YPF agrega un tramo final CORTO, y el auto llegaba
   ahí todavía a velocidad crucero sin espacio para frenar. Ahora suma la
   distancia TOTAL restante (todos los tramos que faltan) contra
   `slowdownDistance` (subida a 25).

Necesita regenerar.

---

## 2026-07-26 — Fix: no entraba a la YPF + bajaban antes de frenar del todo

Owner: "al llegar el auto no se mete dentro de la ypf y antes de frenar ya
saltan los personajes del auto" -- con el auto ya sentando bien a todos
(fix anterior), dos problemas más en la llegada:

1. **No entraba al lote:** la ruta horneada solo seguía la ruta PRINCIPAL
   y frenaba encima del asfalto -- la estación YPF tiene su propio lote
   aparte (`MapLayout.YpfPadNearZ/FarZ`, al NORTE de la ruta, no sobre
   ella). `CarBuilder.cs`: la ruta ahora sigue la ruta principal hasta un
   poco antes de `YpfStation.x`, y agrega 2 puntos más doblando hacia
   ADENTRO del lote (primera aproximación al centro del lote -- a ajustar
   en vivo si el giro queda muy cerrado/ancho).
2. **Bajaban con el auto todavía en movimiento:** `CarAutoDrive.HasArrived`
   se prende apenas está CERCA del último punto, pero el auto puede seguir
   con inercia (throttle en 0 no frena en seco). `OpeningDriveSequence`
   ahora espera ADEMÁS a que `car.SpeedKmh < 2f` antes de abrir puertas y
   bajar a todos.

Necesita regenerar.

---

## 2026-07-26 — Fix: "el auto se fue sin mi" (dos bugs de orden de ejecución)

Owner: "puse play y no spawnie dentro del auto se fue sin mi... y tambien
se fue sin el perro" -- el auto arrancó a manejar solo pero nadie quedó
sentado. Dos bugs de orden de ejecución en Unity, ambos reales:

1. `CarBuilder` activaba `CarAutoDrive`/`autoPilot` YA en Generate (bake) --
   el auto arrancaba a manejar desde el frame 1 de Play, antes de que
   `OpeningDriveSequence` terminara de sentar al jugador/perro (el glide de
   cámara tarda `enterDuration`). Ahora arrancan APAGADOS; `OpeningDriveSequence`
   los prende recién después de sentar a los dos.
2. `OpeningDriveSequence` vive en el auto; `PlayerVehicleInteractor` del
   jugador vive en `TEST_PLAYER` -- son objetos DISTINTOS, y el orden de
   `Start()` entre objetos distintos no está garantizado en Unity. Si
   `OpeningDriveSequence.Start()` corría antes que el `Start()` del jugador
   (que arma su referencia a la cámara), `SitRoutine` fallaba a mitad de
   camino. Agregado `yield return null;` (esperar un frame) al principio de
   la secuencia para asegurar que todos los `Start()` de la escena ya
   corrieron.

También: warning nuevo si `car`/`player`/`dog` no están conectados, para
diagnosticar más rápido si esto vuelve a fallar. Necesita regenerar.

---

## 2026-07-26 — Secuencia de apertura: el auto maneja solo hasta la gasolinera

Feature grande, planeada primero (ver `.claude/plans/swirling-drifting-
patterson.md` si sigue existiendo) antes de tocar código, dado el tamaño.
Owner: "vamos todos en el auto desde el inicio de mapa hasta la gasolinera...
el jugador 1 a la derecha del perro... el greenmale a la izquierda... la
female adelante de acompañante... el male azul [MaleCasual] conduciendo...
al llegar a la gasolinera se bajan todos... y el nuevo orden al subirse es
persona 1 manejando y perro de acompañante, y al subirse ambos spawnean los
otros 3 detrás". Confirmado con el owner: los 3 amigos decorativos quedan
parados quietos al lado del auto al bajarse (nada de caminar a un edificio,
por ahora), y la cámara del jugador puede mirar libre durante todo el viaje
(gratis, ya lo soporta el free-look sentado que ya existía).

**Ruta (`CarBuilder.cs` + nuevo `CarAutoDrive.cs`):** al final de
`CarBuilder.Build()` se hornea un `Vector2[]` de waypoints XZ cada 8m,
siguiendo `MapLayout.PavedRouteZAt(x)` desde el spawn del auto hasta
`MapLayout.YpfStation.x + 8f`. `MapLayout` es editor-only (no accesible
desde scripts runtime), así que la ruta se calcula UNA VEZ acá y se guarda
como datos simples en el nuevo componente runtime `CarAutoDrive`, que la
sigue en `Update()` (ángulo hacia el próximo punto → steer, frena suave en
el tramo final) y expone `HasArrived`.

**`CarController.cs`:** nuevo `autoPilot`/`externalThrottle`/
`externalSteer` -- si `autoPilot`, el throttle/steer vienen de ahí en vez
del teclado; el resto (FixedUpdate, velocidad, giro) queda IGUAL, mismo
camino de física ya probado estable con el manejo normal.

**`PlayerVehicleInteractor.cs`:** `SitRoutine`/`ExitRoutine` pasan a
`public` (sin cambiar lógica) para que un script externo pueda sentar/
bajar al jugador y al perro sin pasar por la mira/tecla E. Nuevo
`public static bool PastGasStation` y `public Transform CurrentSeat`. El
fallback del perro (siempre va a un asiento fijo, sin apuntar) ahora
calcula `dogSeat` dinámico: `rearMid` antes de la gasolinera, `frontPassenger`
después.

**`FriendNpcBuilder.cs`:** el array `seats` de `SeatInCar` pasa de
`{ frontPassenger, rearLeft, rearRight }` a `{ driverSeat, rearRight,
frontPassenger }` -- MaleCasual pasa a manejar, FemaleSec a acompañante,
MaleGreenJkt al asiento trasero libre. `rearLeft`/`rearMid` quedan sin
tocar, reservados para el jugador/perro reales. ⚠ Los `seatPosOverride` de
los 3 quedaron calibrados para sus asientos VIEJOS -- van a estar mal,
marcados en comentario, pendiente re-ajustar en vivo (mismo método de
siempre: Play, tocar Position/ángulos, pasar los números finales).

**Nuevo `OpeningDriveSequence.cs`** (orquestador, un componente en el
auto): sienta jugador+perro en rearLeft/rearMid al arrancar → espera
`autoDrive.HasArrived` → frena, abre las 5 puertas, baja a jugador+perro →
para a los 3 amigos cerca del auto (posiciones `standXLocal`, placeholders)
→ marca `PastGasStation=true` → espera a que jugador esté en driverSeat Y
perro en frontPassenger → reaparecen los 3 amigos sentados atrás
(posiciones `rearXLocal`, placeholders). Wireado por `MapGenerator.cs` al
final de `Generate()` (busca `TEST_PLAYER`/`DOG` por nombre, los 3
`Friend_X` como hijos del auto).

⚠ **Nada de esto se pudo probar visualmente** (sin Unity corriendo en esta
sesión) -- es la feature más grande de toda la sesión de trabajo sobre el
auto, construida en 7 pasos según el plan aprobado. Recomendado probarla
por partes en ese mismo orden (ver el plan) en vez de todo junto, para
encontrar más rápido en qué paso específico algo no anda. Esperables:
ruta que se sale del camino o frena mal (ajustar `cruiseThrottle`/
`steerGain`/`endX` en `CarAutoDrive`/`CarBuilder`), posiciones de los 3
amigos mal (paradas y sentadas, ambas son placeholders), y timing de la
secuencia (los `WaitForSeconds` son estimaciones).

---

## 2026-07-26 — Sigue "adelantado": empuja DogSeatedForwardOffset a negativo

Con el targeting ya forzado a rearMid siempre (entrada anterior), el owner
reportó que el perro SIGUE sentándose adelantado (muy pegado/superpuesto a
`Friend_MaleGreenJkt`). El targeting ya no debería ser la causa -- lo que
queda es el offset de posición propio del perro al sentarse:
`DogSeatedForwardOffset` (0.1) empuja la raíz hacia ADELANTE respecto al
asiento. Invertido a **-0.3** (empuja hacia ATRÁS en vez de adelante).
Sin verificar visualmente (sin Unity en esta sesión) -- necesita regenerar
y probar; si sigue mal, seguir ajustando ESTE número específico (no el
targeting, que ya está resuelto).

---

## 2026-07-26 — El perro SOLO puede sentarse en el medio (ignora la mira)

El fallback (entrada anterior) no alcanzaba: seguía terminando "adelantado"
encima de `Friend_MaleGreenJkt` porque a veces la mira SÍ encontraba algo
(otro asiento) sin querer, y el fallback solo actuaba cuando `target` daba
null. Owner: "haz que el perro solo pueda sentarse en el medio".

Reordenado en `PlayerVehicleInteractor.cs`: el chequeo `!canOpenDoors` ahora
va PRIMERO en la cadena de E (antes de mirar `target`), así el perro
siempre va directo a `rearMid` con solo estar cerca del auto, sin importar
a qué esté apuntando la mira -- ignora aim por completo, no es un
fallback. Mismo orden de prioridad en el cartel (`OnGUI`). Necesita
regenerar.

---

## 2026-07-26 — El perro sube sin apuntar (va directo al asiento del medio)

El fix de la mira por ángulo funcionó (owner: "se esta subiendo bien"), pero
pidió algo más: "al ser el perro necesito que no deba apuntar a donde se
quiero subir ya que no llega a ver, que se suba al que este vacio no mas" —
el perro es bajo y le cuesta apuntar la mira al asiento por la ventana.

`PlayerVehicleInteractor`: nuevo caso para `canOpenDoors=false` (el perro)
cuando la mira no encuentra nada apuntado (`target == null`) — nuevo
`NearestCarInRange(doorRange)` (auto más cercano por su transform raíz, no
un asiento puntual) y sube directo a `car.rearMid` sin necesitar apuntar,
con solo estar cerca del auto. Mismo criterio en el cartel (`OnGUI`) para
que muestre "[ E ] Subir" en esa situación. Si en algún momento SÍ apunta
bien a un asiento, sigue respetando eso (la mira por ángulo tiene
prioridad; este es solo el fallback).

---

## 2026-07-26 — Fix real de la mira: elegir por ÁNGULO, no por distancia del barrido

Angostar las hitboxes (entrada anterior) no alcanzó: el perro seguía
entrando al asiento de al lado (`Seat_RearL`, donde está `Friend_
MaleGreenJkt`) en vez del medio. Causa real: `RaycastTarget()` elegía el
`CarInteractable` cuyo collider tocaba PRIMERO el barrido del SphereCast
(por distancia a lo largo del rayo) -- con 3 asientos pegados, el vecino
puede tocar la esfera antes aunque no sea el que estás mirando, sin importar
qué tan angosta sea la hitbox.

Fix real en `PlayerVehicleInteractor.RaycastTarget()`: ahora elige por
ÁNGULO -- el `CarInteractable` cuyo anchor (`ci.part.position`) esté más
cerca del centro exacto de la mira (`Vector3.Angle` contra `cam.forward`),
sin importar cuál collider tocó primero. Debería apuntar de forma mucho más
confiable al asiento que estás mirando de verdad. Necesita regenerar.

---

## 2026-07-26 — Seat_RearMid: posición confirmada horneada (perro sentado, en vivo)

Con los fixes anteriores (seatDepth, hitboxes, PartyController) el perro por
fin se pudo sentar bien en el asiento del medio. Owner confirmó la posición
en Play ("toma") — en vez de leer el offset calculado por la fórmula
(`paxBase + seatSpread*0.5, seatDepth`), se le pidió el valor de
`Seat_RearMid` DIRECTO del Inspector (ya en coordenadas locales, al ser
hijo del auto — más confiable que convertir a mano la posición del perro en
MUNDO, que tiene mucho margen de error por redondeo). Horneado tal cual:
`(-0.1558, 2.11162, -0.4575)`, reemplazando el cálculo de la fórmula para
este asiento. Necesita regenerar.

---

## 2026-07-26 — Fix real: PartyController dejaba el interactor de la persona activo

Owner (con MAYÚSCULAS, plantado): "CAMBIE AL PERRO Y AHORA PUEDE ABRIR Y
CERRAR PUERTAS LO CUAL NO DEBERIA PODER HACER Y NO SE ESTA PODIENDO SUBIR".
El fix anterior (agregarle `PlayerVehicleInteractor` al perro) no alcanzaba
-- causa real distinta: `PartyController.Apply()` apagaba el `MapExplorer`
de la persona al cambiar al perro (`person.enabled = !controllingDog`),
pero **nunca tocaba su `PlayerVehicleInteractor`** -- ese componente seguía
activo TODO el tiempo (`Update`/`OnGUI` corren en cualquier script
`enabled`, sin importar a quién "controlás" vía cámara). Resultado: el
cartel de puerta que se veía era el de la PERSONA (congelada donde estaba
parada antes de cambiar), no el del perro -- y los dos interactores
competían por la misma tecla E al mismo tiempo, rompiendo también el
"subirse" del perro.

Fix en `PartyController.cs`: cachea `PlayerVehicleInteractor` de persona y
perro en `Start()`, y `Apply()` ahora también habilita/deshabilita el que
corresponda junto con la cámara -- solo el personaje que controlás
activamente puede interactuar con el auto. Necesita regenerar.

---

## 2026-07-26 — Fix: el perro (modo solo) no podía subir al auto

Owner: "al cambiar con la g al perro no me da la opcion de subirme siendo
perro". En modo SOLO (sin host, `PartyController` con G para tomar control
del perro), el DOG que arma `TestPlayerBuilder` nunca tuvo
`PlayerVehicleInteractor` — solo se le agregaba al jugador humano. El perro
de RED (`NetworkBuilder.cs`) sí lo tiene (`canOpenDoors = false`), pero el
perro de modo solo se armó en un archivo distinto y se pasó por alto.
Agregado con el mismo criterio (`canOpenDoors = false`, no abre/cierra
puertas, solo se sienta/baja). Necesita regenerar.

---

## 2026-07-26 — Fix: la mira agarraba el asiento vecino en vez del del medio

Con `Seat_RearMid` ya en buena posición, el owner reportó que al intentar
subirse ahí terminaba arriba de `Friend_MaleGreenJkt` (el asiento de al
lado) en vez del asiento vacío. Causa: los 3 hitboxes traseros (colliders
invisibles que usa la mira para elegir a qué asiento apuntás) se pisaban —
`rearLeft`/`rearMid`/`rearRight` están separados solo ~0.93m (`seatSpread`)
pero cada hitbox medía 0.85m de ancho (mitad 0.425), así que los vecinos se
superponían y la mira podía agarrar el equivocado. `CarBuilder.
SeatCollider`: ancho angostado a 0.35m (mitad 0.175, bien adentro de los
0.465m de separación a cada vecino). Necesita regenerar.

**Nota de diseño (script de la secuencia del auto, para más adelante — NO
implementado, solo anotado):**
1. Arranca manejando `Friend_MaleCasual` ("el male normal"), con
   `Friend_FemaleSec` de acompañante.
2. El perro va en el medio de atrás, junto con el jugador 1 (humano) y
   `Friend_MaleGreenJkt`.
3. El auto avanza hasta la gasolinera; al llegar frena y se bajan todos.
4. Al volver a subir: el jugador 1 pasa a manejar (chofer), el perro es el
   acompañante (adelante), y los otros 3 amigos van atrás.
Falta: lógica de quién controla el auto en cada tramo, el evento de parada
en la gasolinera, y reasignar los `seatPosOverride` de los amigos si
cambian de asiento entre tramos.

---

## 2026-07-26 — Fix cámara "detrás de los asientos" al sentarse en Seat_RearMid

Owner probó sentarse (como jugador real, no decorativo) en el asiento nuevo
del medio-atrás y la cámara quedó metida detrás de los asientos, no en el
asiento. Ese anchor nunca se había probado con un jugador real sentado ahí
-- solo se usaba antes como REFERENCIA para calcular la posición de los
amigos decorativos (que ahora tienen su propia posición horneada,
desconectada del anchor).

Comparando con los valores que sí terminaron sirviendo para los amigos
(ajustados 100% a mano): quedaron con Z ~-0.8, mucho menos que el
`seatDepth` que usa el anchor (-1.71, calculado como `TargetLength *
-0.2591`) -- la fórmula empuja los asientos traseros (rearLeft/rearRight/
rearMid) bien más atrás de la butaca real del auto. Reducido a la mitad
(`TargetLength * -0.13`, Z ~-0.86).

⚠ Afecta también a `Seat_RearL`/`Seat_RearR` (comparten el mismo
`seatDepth`) -- son los anchors que usa la CÁMARA de un jugador real (no
los amigos decorativos, que ya no dependen de esto), así que si alguien se
sienta ahí como jugador debería mejorar también. Sin confirmar
visualmente -- pendiente que el owner pruebe sentarse en el del medio de
nuevo.

**Nota de diseño (para más adelante, no implementado):** el owner quiere
que al arrancar el juego el auto vaya con perro en el medio-atrás y
Friend_MaleGreenJkt a un lado; después, en una parada en la gasolinera,
el jugador humano y el perro cambian a los asientos de adelante. Story/
gameplay logic pendiente, no es parte de este fix.

---

## 2026-07-26 — Friend_FemaleSec: último de los 3, valores finales horneados

Owner terminó de ajustar a mano en Play, "esa es la female, guardala":
posición propia, ángulo -61, drop -0.5, escala 0.76 (levemente distinta del
default global 0.77 -- nuevo `seatedScaleYOverride`, mismo patrón que los
otros 3 campos de override). Con esto, **los 3 amigos** tienen su pose/
posición individual horneada — nadie depende más de la fórmula genérica
(`SeatRootOffset`/paxBase), que costó muchísimo afinar a ciegas por captura
en las vueltas anteriores. `SeatRootOffset`/`paxBase` quedan como fallback
sin usar (por si se agrega un 4to personaje sin ajustar a mano todavía).

---

## 2026-07-26 — Friend_MaleGreenJkt: valores finales horneados

Owner terminó de ajustar `Friend_MaleGreenJkt` 100% en vivo (Play) hasta
"ahi esta, guardalo": ángulo del muslo +55 (el primer intento, +62, no era
el correcto todavía), posición propia del asiento, y `Seated Model Drop`
distinto del default global (-0.5, no -0.63 como `Friend_MaleCasual`).
Nuevo `FriendDef.seatedModelDropOverride` (mismo patrón que los otros dos
overrides) para que cada personaje pueda tener su propio drop además de su
propio ángulo/posición. Horneado en la entrada de `Friend_MaleGreenJkt`:
`seatPosOverride=(-0.7201, -0.1883, -0.8)`, `seatedThighAngleOverride=55`,
`seatedModelDropOverride=-0.5`.

Queda `Friend_FemaleSec` con la fórmula vieja (sin overrides) — mismo
método si el owner la quiere ajustar.

---

## 2026-07-26 — Fix piernas al revés de Friend_MaleGreenJkt (rig Mixamo)

Owner: "tiene las piernas alrevez daselas vuelta al malegreen". Causa: cada
amigo viene de un rig distinto (Vinrax, Mixamo, UE Mannequin) con el eje del
muslo orientado distinto — el mismo `seatedThighAngle` (compartido por
`HumanWalkAnim`, ahora -62 por default, calibrado para `Friend_MaleCasual`/
Vinrax) dobla la pierna para ADELANTE en un rig y para ATRÁS en otro.

Nuevo `FriendDef.seatedThighAngleOverride` (mismo patrón que
`seatPosOverride`): si está seteado, `BuildOne` lo aplica sobre el
`HumanWalkAnim` de ESE personaje en vez del default del componente.
`Friend_MaleGreenJkt` (Mixamo) → **+62** (signo invertido). Si sigue mal,
probar magnitud distinta además del signo (no necesariamente el mismo
62 en valor absoluto para un rig distinto).

---

## 2026-07-26 — Friend_MaleCasual: valores finales horneados (ajustado 100% a mano en vivo)

Owner terminó de ajustar todo EN VIVO con Play (arrastrando/tocando números
en el Inspector mientras miraba la vista Game) hasta que "quedó perfecto"
sentado en el asiento del acompañante:
- **Pose** (`HumanWalkAnim`, defaults nuevos — aplican a los 3 amigos y al
  jugador, comparten el mismo script): `seatedThighAngle` -75→**-62**,
  `seatedScaleY` 0.55→**0.77**, `seatedModelDrop` -0.8→**-0.63**.
- **Posición** (solo `Friend_MaleCasual`, específica de su asiento): en vez
  de seguir peleando con la fórmula (`seat.localPosition - SeatRootOffset`,
  que costó MUCHÍSIMO afinar a ciegas por captura en las vueltas
  anteriores), nuevo `FriendDef.seatPosOverride` — si está seteado,
  `SeatInCar` lo usa TAL CUAL en vez de calcularlo. Valor horneado:
  `(0.5999, -0.283, 0.3031)` local al auto.
- Los otros 2 (`Friend_MaleGreenJkt`, `Friend_FemaleSec`) siguen con la
  fórmula vieja (sin `seatPosOverride`) — mismo criterio si el owner los
  quiere ajustar: mover en Play, pasar los 3 números finales, hornear como
  `seatPosOverride` de esa entrada en el array `Friends`.
- **Necesita regenerar** para ver el resultado horneado (los cambios en vivo
  de Play no persisten solos al salir).

---

## 2026-07-26 — Ajuste en vivo (Play): seatedModelDrop=-0.8, seatedScaleY=0.8

Después de sacar el drop duplicado, el owner ajustó `Seated Model Drop` EN
VIVO con Play apretado (el campo se re-aplica cada frame en `LateUpdate`,
así que cambiarlo ahí da feedback inmediato sin regenerar) hasta encontrar
**-0.8** — con eso quedan bien ubicados. Quedó "achatados" (el achicado
0.55 muy agresivo una vez resuelta la posición) — subido a **0.8**.
Horneados los dos como default en `HumanWalkAnim.cs`.

Mucho más rápido que seguir iterando por captura+regenerar — si hace falta
un ajuste más, misma técnica: Play, tocar `Seated Scale Y`/`Seated Model
Drop` en el Inspector del `Friend_X`, ver el resultado al toque.

⚠ **Corrección:** subí `Seated Scale Y` solo (0.55→0.8) sin volver a probar
el combo con el drop, asumiendo que eran independientes -- NO lo son: el
achicado pivotea desde un punto que no es los pies, así que más escala
estira las piernas más hacia abajo desde ese pivote (no solo "menos
achatado"). Resultado: quedaron dentro del piso del auto. Vuelto al combo
CONFIRMADO (0.55 / -0.8). Si sigue "achatado", subir la escala de a poco y
re-chequear el drop en cada paso, no cambiar un valor solo.

---

## 2026-07-26 — El bug de fondo: SeatRootOffset + seatedModelDrop se sumaban

Con el "cayéndose" y el "no spawnearon" ya resueltos, el owner reportó que
seguían apareciendo "detrás y también debajo del auto" — esta vez SIN caer
con el tiempo, mal posicionados de entrada. Bug real que ya se había
identificado como riesgo al agregar el squash (ver entrada "causa real
encontrada") pero nunca se terminó de sacar: `FriendNpcBuilder.
SeatRootOffset = 2.3` YA baja la raíz para alinear la cabeza a la altura del
asiento (como si estuviera parado); `HumanWalkAnim.seatedModelDrop = 0.9`
bajaba el modelo OTRO METRO encima de eso — las dos correcciones se sumaban,
terminando bien por debajo del auto. `seatedModelDrop` default → **0**; el
achicado (`seatedScaleY=0.55`, alrededor del pivote del modelo) hace la
compactación sola, sin doble resta. Sigue existiendo el campo por si hace
falta un ajuste fino chico, pero ya no arranca en 0.9.

⚠ Pendiente confirmar si esto también resuelve el "detrás" (Z) — la
evaluación de profundidad que se hizo antes fue siempre en Edit mode (sin el
squash aplicado), así que no es dato confiable con este fix. Regenerar y
mirar en Play.

---

## 2026-07-26 — Encuentra la causa real de los amigos "cayéndose" al dar Play

Owner: en Edit mode los 3 amigos se veían bien sentados (posición ya
resuelta), pero al apretar Play **caían visiblemente** hasta terminar bajo
tierra, debajo del auto. Descartado que fuera el auto (Rigidbody) hundiéndose
-- su Position se queda fija, confirmado por el owner. Como los amigos NO
tienen física propia (sin Rigidbody/CharacterController), si se mueven con
el tiempo tiene que ser un SCRIPT.

**Causa:** `FriendWander` (el que los hacía caminar cerca de la ruta) se
supone que `SeatInCar` lo destruye (`DestroyImmediate`) al sentarlos -- pero
por lo que sea, esta vez no llegó a tiempo. Si sigue activo, `Update()` mueve
`transform.position` en coordenadas de MUNDO cada frame Y PISA la altura Y
contra el terreno real (`Terrain.SampleHeight`, clamp a
`RoadSurfaceHeight`) -- sin ninguna noción de "estoy sentado adentro de un
auto". Arranca desde donde el personaje YA está (adentro del auto, en alto)
y lo arrastra hacia el nivel del piso/ruta con el tiempo -- coincide EXACTO
con "cae gradualmente hasta abajo del auto".

**Fix (segunda capa de seguridad, no reemplaza el Destroy):**
`FriendWander.Start()` ahora chequea si hay un `HumanWalkAnim` con
`seated=true` en el mismo objeto -- si es así, se autodesactiva
(`enabled=false`) ANTES de calcular ningún punto de caminata, en vez de
depender 100% de que `SeatInCar` lo haya destruido a tiempo. Así, aunque el
`Destroy` falle por la razón que sea, el personaje sentado nunca camina.

---

## 2026-07-26 — Delete Map + Generate arregló el "no spawnearon"; ajuste fino de profundidad

Confirmado: **Delete Map + Generate limpio resolvió** que los amigos no
aparecían — era estado turbio del Editor acumulado tras muchos regenerados
seguidos en la misma sesión, no un bug de código (como se sospechaba en la
entrada anterior).

Con los personajes ya visibles y sentados, quedó un ajuste fino: el empuje
hacia atrás de `paxBase` (-0.45, de la vuelta anterior) se pasó para el otro
lado — ahora atraviesan el respaldo/asiento de ATRÁS. Bajado a **-0.30**, el
MISMO valor que ya usa el conductor (`dSeat`) — ya probado que ese no choca
contra nada.

---

## 2026-07-26 — "no spawnearon ahora": los 3 amigos sin Model (sin causa confirmada)

Después de la 4ta vuelta (squash), el owner reportó que los 3 amigos dejaron
de aparecer del todo en el auto — ni hundidos ni flotando, directamente
INVISIBLES. Diagnóstico por preguntas (consola limpia, sin duplicados, sin
"Model" en ningún lado de la Hierarchy, ni con el filtro de búsqueda vacío)
confirmó que el GameObject `Friend_MaleCasual` (y los otros 2) existen con
`Transform` + `HumanWalkAnim`, pero **sin ningún hijo** — el `Model`
(instancia del fbx) nunca quedó colgado, o se perdió.

`git diff` entre el commit que SÍ andaba (flotando sobre el techo, personajes
visibles) y el que no muestra que el único cambio fue puramente ADITIVO
dentro de `HumanWalkAnim.LateUpdate` (lee `_model`, lo modifica si no es
null) — no hay forma de que ese diff borre un hijo. **No se encontró la
causa real.** Se descartaron: `ManualLayoutPersistence` (no registra
amigos), `MapLayoutPersistence.ApplySavedLayout` (reposiciona, no borra
hijos), duplicados en escena (confirmado: uno solo de cada uno).

**Lo que se hizo (hardening, no un fix confirmado):**
- `LoadPointTex` (que llama `SaveAndReimport`, un reimport de asset a mitad
  de construir la jerarquía — terreno conocido para rarezas del Editor) se
  adelanta ahora al PRINCIPIO de `BuildOne`, antes de instanciar nada.
- Nuevo warning en `SeatInCar`: si un amigo no tiene hijo `Model`, lo grita
  en consola en vez de sentarlo invisible en silencio — la próxima vez que
  pase esto, el log lo va a decir directo, sin 10 preguntas de por medio.
- ⚠ Recomendado: **Delete Map** (Tools ▸ Folklore Archives ▸ Delete Map) +
  Generate de nuevo, para descartar estado turbio del Editor acumulado
  después de MUCHOS regenerados seguidos en la misma sesión de Unity. Si
  falla otra vez, el warning nuevo va a decir exactamente cuál.

---

## 2026-07-26 — 4ta vuelta: causa real encontrada (rotar el muslo no achica al personaje)

Owner confirmó que SÍ regeneraba entre cada prueba, y aun así 0.65 y 1.0
(`SeatHipHeight`) se veían casi IGUAL ("siguen por fuera") — eso descartó que
fuera un problema de "no regeneró" y apuntó a que estaba tocando la variable
equivocada.

**Causa real:** `HumanWalkAnim.seated` (rama `if (seated)` de `LateUpdate`)
SOLO rota los huesos de muslo — nunca traslada ni achica el modelo. La cabeza
de un personaje de 2.3m de altura queda SIEMPRE a `raíz + 2.3` sin importar
dónde se ubique la raíz (mover la raíz solo DESPLAZA el problema, no lo
arregla) — un personaje de pie entero no entra bajo el techo de un auto por
más que se reubique. Nunca se notó con el jugador porque su cuerpo sentado se
OCULTA de su propia cámara (`SelfHidden`) — puede que este mismo bug afecte
también cómo lo ven los DEMÁS clientes en red manejando/de acompañante, solo
que nadie lo había mirado de cerca.

**Fix real, en `HumanWalkAnim.cs`:** mismo mecanismo que ya usa el agachado
(`crouchScaleY`/`crouchDrop`) pero para "sentado": nuevos `seatedScaleY`
(0.55, achica el modelo) y `seatedModelDrop` (0.9, lo baja), aplicados fijos
(sin lerp) en la rama `seated`. `FriendNpcBuilder.SeatRootOffset` vuelve a
`2.3` (altura completa, como estaba en el primerísimo intento) — ahora la
raíz alinea la CABEZA a la altura del asiento COMO SI ESTUVIERA PARADO, y el
achicado nuevo de `HumanWalkAnim` es lo que la baja a una altura de sentado
razonable y evita que los pies quedaran colgando.

⚠ **Sin verificar visualmente** — los 3 intentos anteriores tocando SOLO la
posición de la raíz (sin este mecanismo) nunca convergieron, así que en vez
de arriesgar una 5ta vuelta a ciegas, mejor que el owner ajuste
`seatedScaleY`/`seatedModelDrop` EN VIVO: Play, pausar con un amigo sentado a
la vista, seleccionarlo en la Hierarchy (`Friend_X > Model`) y tantear
Scale/Position hasta que se vea bien, o tocar los campos `Seated Scale Y` /
`Seated Model Drop` en el `HumanWalkAnim` del padre (`Friend_X`) mientras está
en Play — los cambios se ven al toque, sin tener que Salir de Play/regenerar/
sacar captura por cada prueba.

---

## 2026-07-26 — 3ra vuelta: 0.65 se pasó de largo (sentados arriba del techo)

Captura del owner: con `SeatHipHeight=0.65` los 3 amigos quedaron sentados
ARRIBA del techo del auto (no adentro). El salto anterior (1.15→0.65, solo
0.5m) causó un cambio visual enorme -- de "hundido en el asiento" a "flotando
sobre el techo entero" -- lo que sugiere que el margen vertical real dentro
de la cabina (asiento↔techo) es angosto, y el ajuste se pasó de frenada.
Vuelta a un paso chico esta vez: `SeatHipHeight` 0.65 → **1.0** (entre el
valor que hundía de menos y el que voló por arriba).

⚠ Este método (ajuste a ciegas por captura, sin Unity corriendo en esta
sesión) está resultando lento e impreciso — los saltos grandes sobrecorrigen.
Alternativa más rápida para el owner: los 3 amigos son GameObjects normales
(`Friend_MaleCasual`/`Friend_MaleGreenJkt`/`Friend_FemaleSec`, hijos de
`Renault12` después de Generate) — se pueden arrastrar a mano con el gizmo de
mover en la Scene view hasta que se vean bien, y avisar CUÁNTO se movieron
(ej. "subí 0.3 en Y") para hornear ese número directo en `SeatHipHeight`, en
vez de seguir iterando por capturas.

---

## 2026-07-26 — 2da vuelta: siguen hundidos / muy adelante (ajuste más fuerte)

Nueva captura del owner tras la vuelta anterior: el brazo del de barba ya se
ve bien (fix de `Contains` funcionó), pero los de ATRÁS seguían atravesando
el asiento de abajo, y el de ADELANTE seguía muy pegado al tablero — los
primeros números elegidos (`SeatHipHeight=1.15`, empuje `-0.15`) se quedaron
cortos. Segunda pasada, más agresiva, en la misma dirección que pidió el
owner:
- `FriendNpcBuilder.SeatHipHeight`: 1.15 → **0.65** (menos resta = quedan
  más arriba).
- `CarBuilder.paxBase`: empuje -0.15 → **-0.45** (más atrás incluso que el
  conductor -0.30 — el acompañante no necesita llegar al volante/pedales).
- ⚠ Sigue siendo ajuste a ciegas por captura (sin Unity corriendo en esta
  sesión) — puede necesitar una 3ra vuelta si se pasó para el otro lado
  (demasiado arriba/atrás).

---

## 2026-07-26 — Fix: amigos sentados (brazo extendido + hundidos en el asiento)

Owner mandó capturas de los amigos ya sentados en el auto (feature anterior):
brazo del personaje de barba (Mixamo, `Friend_MaleGreenJkt`) extendido hacia
adelante, y los 3 atravesando el asiento/piso del auto (el de acompañante
además "muy adelante", pegado al tablero).

- **Brazo extendido — bug real, no solo de calibración:** `HumanWalkAnim`
  detecta huesos de brazo con `bone.Contains("arm")`, pero `Contains` es
  **case-sensitive** y el rig Mixamo nombra el hueso `"mixamorig:LeftArm"`
  (con "Arm" en mayúscula) — nunca matcheaba. Ese personaje NUNCA tuvo la
  corrección de T-pose del brazo, y sentado encima lo trataba como PIERNA
  (le aplicaba el ángulo de muslo -75°), de ahí el brazo "extendido" hacia
  adelante. Nunca se notó de pie (parado, con los brazos ya en la T-pose sin
  corregir, se veía raro pero no tan mal como sentado). Fix en
  `HumanWalkAnim.cs`: las 3 comparaciones pasan a `bone.ToLowerInvariant()
  .Contains("arm")`.
- **Hundidos en el asiento:** `FriendNpcBuilder.SeatInCar` restaba la altura
  de OJO completa (2.3, la misma que usa el jugador PARADO) para ubicar la
  raíz del personaje desde el asiento — pero `HumanWalkAnim.seated` solo
  ROTA los muslos, no traslada el modelo, así que la cadera quedaba a 2.3m
  por DEBAJO del asiento (bajo el piso del auto). Nunca se notó con el
  jugador porque su propio cuerpo sentado se OCULTA de su propia cámara
  (layer SelfHidden) — nadie mira su propia cadera hundida. Cambiado a restar
  solo la altura de CADERA aprox. (`SeatHipHeight = 1.15`, mitad de
  `targetHeight`).
- **Acompañante muy pegado al tablero:** `seatBase` (base de acompañante/
  traseros) nunca tenía el empuje hacia atrás (-0.30) que sí tiene el
  conductor (`dSeat`) — quedaban a la profundidad del VOLANTE. Nuevo
  `paxBase` con un empuje más chico (-0.15) solo para
  acompañante/traseros/rearMid (el conductor no se toca).
- ⚠ **Sigue sin poder probarse visualmente** (sin Unity corriendo en esta
  sesión) — son ajustes razonados a partir de la captura, no medidos. Mandá
  otra captura después de regenerar para afinar `SeatHipHeight`/el -0.15 si
  hace falta.

---

## 2026-07-26 — 5to asiento (Seat_RearMid) + fix faros del auto (casi no se veían)

Dos pedidos en la misma tanda:

**Asiento extra atrás, al medio.** Owner: primero preguntó por sentar al perro
en un asiento del medio, después lo simplificó a "que haya un asiento más al
medio". Con los 3 amigos ahora ocupando `frontPassenger`/`rearLeft`/`rearRight`
de forma decorativa, no quedaba ningún asiento libre para un 2º jugador (o el
perro) en co-op. Nuevo `CarController.rearMid` (banco trasero apretado a 3, a
mitad de camino entre `rearLeft` y `rearRight`), armado en `CarBuilder.Build`
igual que los demás (con su collider-trigger) y sumado a la lista `Seats()` de
`PlayerVehicleInteractor` (asiento libre normal, con interacción E como
cualquier otro — no es decorativo).

**Faros que casi no se veían.** Owner: "deberian iluminar mucho las luces del
auto, casi no se ven, enfocan debajo del auto no mas". Causa encontrada: la
altura (Y) de los faros estaba HARDCODEADA en 0.55m desde que se armaron —
nunca se actualizó cuando el auto creció (`TargetLength` 4.4→6.6 +
`HeightBoost` 1.15, el mismo patrón de bug ya visto antes en `dSeat`/
`seatBase`), así que terminaban muy abajo para un auto mucho más grande.
Fix en `CarBuilder`:
- `carHeight` ahora se MIDE del auto ya escalado (mismo bounds que se usa
  para recentrar el modelo) y se pasa a `BuildHeadlights`; la altura del faro
  es una FRACCIÓN de esa medida (`HeadlightHeightFrac = 0.35`) en vez de un
  número fijo — se autoescala si el auto vuelve a cambiar de tamaño.
- Intensidad 20→55, rango ×1.4→×1.6, + una leve inclinación hacia abajo (6°)
  para que el cono pegue en el piso adelante (antes apuntaba perfectamente
  horizontal).
- ⚠ **No se pudo confirmar visualmente** (sin Unity corriendo en esta
  sesión) — el valor de `HeadlightHeightFrac` es una estimación geométrica
  razonable, no una medición exacta del modelo. Regenerar y ajustar esa
  constante si los faros quedan muy altos/bajos.

---

## 2026-07-26 — Los 3 amigos arrancan sentados en el auto (decorativo)

Owner: "hace que se puedan sentar no mas en el auto decorativos" (después de
pensar en voz alta si sumar un asiento más al medio para el perro — se dejó
para más adelante, esto es solo los 3 amigos). El auto tiene 4 asientos
(`driverSeat`/`frontPassenger`/`rearLeft`/`rearRight`); el conductor es
siempre el jugador, así que quedan justo 3 libres para los 3 amigos.

Problema de orden de construcción: `FriendNpcBuilder.Build` (parados junto a
la ruta) corre desde `LandmarkBuilder`, ANTES de que `CarBuilder` arme el
auto — no había auto todavía para sentarlos ahí directamente.

- Nuevo `FriendNpcBuilder.SeatInCar(root, car)`, llamado desde
  `MapGenerator.cs` **después** de `CarBuilder.Build` (captura el
  `GameObject` que antes se descartaba). Busca `PointsOfInterest/FriendsNPC`
  (ya construido), y para cada amigo: le saca `FriendWander` (ya no camina),
  lo reparenta bajo el auto (`frontPassenger`/`rearLeft`/`rearRight`, mismo
  orden que la tabla `Friends`) y prende `HumanWalkAnim.seated = true` —
  MISMA pose sentada que ya usa el jugador/perro.
- Posición: como `Seat()` (CarBuilder) pone `localRotation = identity`,
  alcanza con trabajar en espacio LOCAL del auto: `friend.localPosition =
  seat.localPosition - (0, 2.3, 0)` — 2.3 es el mismo offset ojo-a-pies que
  usa la cámara del jugador (`NetworkBuilder`/`TestPlayerBuilder`), ya que
  los amigos miden lo mismo (2.3m).
- **Puramente decorativo**: sin interacción, sin tecla E — quedan fijos
  desde que arranca el mapa. Los colliders-trigger de esos 3 asientos siguen
  activos (por si en co-op otro jugador quiere sentarse ahí igual — quedaría
  superpuesto con el amigo decorativo; no se resolvió, es un caso de borde).
- **Necesita regenerar** + revisar en el Editor (sin Unity corriendo en esta
  sesión, no se pudo confirmar visualmente que los 3 queden bien sentados,
  sin atravesar el asiento/tablero).

---

## 2026-07-26 — Cartel + E para prender/apagar los faros mirando al frente

Owner: "al mirar hacia delante estando en el auto deberia darme la opcion de
prender y apagar las luces del auto". Los faros ya se podían prender/apagar
con **F** mientras manejás (`CarController.Update`), pero no había ningún
cartel que lo indicara — y el pedido es que use el mismo patrón mira+E que ya
usan las puertas.

`PlayerVehicleInteractor.cs`: nuevo `LookingForward(CarController)` (mismo
criterio angular que `LookingAtDoor`, pero contra `car.transform.forward` en
vez de la posición de la puerta, para no pisarse — la puerta queda ~90° al
costado). En la cadena de prioridad de E estando sentado (mirando tu puerta →
tocarla; si no...): ahora, **si sos el conductor y mirás al frente**, E
alterna `SetHeadlights` en vez de bajarte; cualquier otro lado sigue bajando
como antes. Mismo criterio en `OnGUI` para el cartel ("[ E ] Prender/Apagar
luces"). Solo para el conductor (los pasajeros no tienen faros que tocar) y
no le saca la tecla F que ya andaba.

---

## 2026-07-26 — Friend_FemaleSec: reemplazo del asset (secretaria → chica retro)

Owner: "descargue esa chica descomprimila y reemplazala por la que ya esta" —
bajó `girl-game-character-retro-style.zip` a Downloads (modelo "a_lowpoly",
sin readme/licencia adentro del zip). Descomprimido (tenía un zip anidado,
`source/a_lowpoly.zip`, con el fbx real) y copiado a
`Assets/ExternalAssets/FriendNPCs/GirlRetro/` (`girl_retro.fbx` +
`Textures/body_tex.png`, `hair_tex.png`, `shoes_tex.png`).

- **Rig distinto:** este modelo usa nomenclatura tipo **UE Mannequin**
  (`thigh_l`/`thigh_r`, `arm_l`/`arm_r`/`forearm_l`/`hand_l`, `spine_01`...),
  confirmado leyendo los strings crudos del fbx binario (no había Unity
  corriendo para inspeccionarlo directo). Nuevo `GirlRetroLimbs` en
  `FriendNpcBuilder.cs`.
- **Multi-material (novedad):** a diferencia de los otros 2 amigos (una sola
  textura para todo el modelo), este trae **3 materiales separados**
  (`body`/`hair`/`shoes`) en vez de un atlas único. `FriendNpcBuilder` ahora
  soporta ese caso (`TexPart[]`/`FriendDef.texParts`): matchea cada submaterial
  del fbx por nombre contra `body`/`hair`/`shoes` y le arma su propio material
  URP (cacheado por textura, no por submalla). El camino viejo (una textura
  para todo) se mantiene intacto para `Friend_MaleCasual`/`Friend_MaleGreenJkt`.
- Misma posición/altura/yaw que la secretaria vieja (2.3f, offset -8/0.2, yaw
  90°) — solo cambia el modelo. Se borró el asset viejo
  (`FemaleSecretary/`, de Vinrax) y su material huérfano.
- ⚠ **Sin crédito/licencia todavía** — el zip no traía readme; anotar la
  fuente cuando el owner la tenga (probablemente Sketchfab, por el nombre
  interno `a_lowpoly_sketchfab.fbx`).
- **Necesita regenerar** + revisar en el Editor: no se pudo verificar visualmente
  (sin Unity corriendo en esta sesión) — confirmar que la textura de body/hair/
  shoes cae en la parte correcta y que la pose/caminata salen derechas como con
  los otros 2 amigos.

---

## 2026-07-26 — Amigos: salen de T-pose, caminan, y dejan de hundirse

Owner: "vamos con la movilidad de los otros personajes que se muevan igual que el
principal, que no esten todos duros en pose de t" → varios pasos encadenados sobre
los 3 NPCs "amigos" (`Friend_MaleCasual`, `Friend_MaleGreenJkt`, `Friend_FemaleSec`):

1. **T-pose → pose de reposo**: se les agrega `HumanWalkAnim` (mismo componente
   procedural del protagonista). Los defaults solo calzaban con el rig de
   `FemaleSec` (notación Blender `thigh.L`); los otros dos rigs (`thigh_left` /
   `mixamorig:LeftUpLeg`) necesitan un `Limb[]` a medida por personaje.
2. **Brazos en "V"**: el fix de hombro (rotar `upper_arm` hacia abajo) no tocaba
   el doblez local de codo/mano. Se agrega `StraightenArmChain`: mismo truco
   (`FromToRotation`) hueso por hueso bajando el brazo.
3. **`FriendWander.cs`** (nuevo, runtime): cada amigo camina 2.5m ida y vuelta
   cerca de donde arranca, apoyado en `Terrain.SampleHeight` (sin
   CharacterController — son ambientación de fondo).
4. **Brazos "torcidos" al caminar**: el `_rest[]` de un brazo queda con una
   rotación arbitraria por la corrección de T-pose (paso 1), así que balancear
   en el eje LOCAL del hueso ya no caía en un plano adelante-atrás predecible.
   Ahora el vaivén gira en el eje "derecha" del PERSONAJE (mundo), no del hueso.
5. **Pies hundidos cerca del auto/ruta**: `FriendWander` sampleaba terreno crudo
   sin piso mínimo — mismo bug ya visto en `CarBuilder`/`TestPlayerBuilder`/el
   perro. `FriendNpcBuilder` ahora le pasa `MapLayout.RoadSurfaceHeight` como
   piso mínimo.

De paso, altura del perro: se sacó el margen manual `-0.06` (calibrado para un
`Renderer.bounds` viejo) y se horneó `ManualLiftCalibrated = 0.567` como
constante para que la posición ajustada a mano por el owner sobreviva a un
Generate.

⚠ **Pendiente / en curso**: el owner pidió reemplazar el asset de
`Friend_FemaleSec` — la "secretaria" (ropa de oficina) no encaja con la
ambientación rural ("el campo"). Buscando alternativa low-poly femenina en
itch.io, todavía sin elegir.

---

## 2026-07-25 — Fix pasto borrado que reaparecía (GrassPersistence)

El pasto borrado a mano volvía al regenerar. Causa: `TerrainPaintPersistence.SaveDetailDiff`
comparaba el pasto vivo contra un baseline RECALCULADO con `SetupGrass`, pero SetupGrass
reparte el pasto con **Random no-determinístico** → el baseline salía con el pasto en otras
celdas → el "diff" capturaba RUIDO (celdas con pasto, no el borrado) y al re-aplicarlo
**reintroducía** pasto. (Se vio en el archivo: valores guardados casi todos v>0.)
- `GrassPersistence.cs` (mismo patrón que `TreePersistence`): el baseline es el pasto REAL
  capturado en `ForestBuilder` justo tras `SetupGrass` (`grass_baseline.bytes`, en Generated).
  `SaveRemovals` guarda solo las celdas donde `live < baseline` (lo borrado) →
  `grass_removals.bytes`. `ApplyRemovals` baja esas celdas a su valor guardado — **solo
  reduce, nunca agrega** → no puede reintroducir lo borrado.
- Hooks: `ForestBuilder` (tras SetupGrass: CaptureBaseline + ApplyRemovals; y always-apply
  fuera del cache). `SaveTerrainPaint` usa `GrassPersistence.SaveRemovals` (ya no
  `SaveDetailDiff`, ni `SetupGrass(baseline)`). `ClearTerrainPaint` limpia también el pasto.
- Alpha (texturas) sigue con diff contra baseline recalculado (PaintTextures SÍ es
  determinístico) — eso andaba bien.
- ⚠ Flujo: **Rebuild Forest (forzar) + Generate UNA vez** (captura el baseline) → borrar
  pasto → Save Terrain Paint → Generate. Si guardás sin baseline avisa "Rebuild Forest".

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
