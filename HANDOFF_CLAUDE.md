# HANDOFF — Folklore Archives: La Luz Mala

Este archivo es para que una sesión NUEVA de Claude Code (otra suscripción/cuenta)
retome el trabajo sin perder contexto. Lo escribió Claude Code el 2026-09-25,
al terminar una sesión larga. Leelo entero antes de tocar código.

**Repo**: `C:\Users\f\Folklore Archives - The Evil Light` (Windows). Remote:
`https://github.com/FidelGenre/Folklore-Archives-La-Luz-Mala.git` (rama `main`).
Último commit de esta sesión: `db0abdd`.

---

## 1. Convenciones del proyecto (NO romperlas)

- **Dos VCS en paralelo**: git (todo el código `.cs`) + Unity Version Control /
  Plastic (la escena `.unity` y TODO lo demás — assets, materiales, layout). La
  escena **NO está en git**. Esto significa: el código que escribas se pushea a
  git, pero los cambios en la escena de Unity (posiciones movidas a mano, objetos
  creados en el Editor) el owner los maneja con Unity VCS por su cuenta — vos NUNCA
  editás la escena `.unity` directamente como archivo de texto (riesgo de corromper
  un YAML complejo que está abierto en vivo en el Editor).
- **El owner corre Unity, vos no**. No tenés acceso al Editor de Unity. Todo lo
  que hacés es código C# (Editor scripts + scripts de runtime). El owner aprieta
  los botones/corre Play/corre Generate y te reporta con capturas de pantalla qué
  pasó.
- **"No adivinar" coordenadas**: NUNCA inventes una posición/rotación de mundo.
  Cuando hace falta un punto exacto (dónde va un NPC, una puerta, un prop), pedile
  al owner que pare `TEST_PLAYER` ahí en el Editor y te pase Position/Rotation del
  Inspector. Así se construyó TODO lo que está puesto en el mapa hasta ahora.
- **Push SIEMPRE** después de cada cambio que compile: `git add <archivos
  puntuales> && git commit -m "..." && git push origin main`. El remote a veces
  tira "This repository moved..." (aviso, no error) — sigue pusheando bien.
- **Nunca stagear archivos que el owner ya tenía modificados localmente sin
  relación con tu tarea**. Antes de cada commit corré `git status --short` y
  agregá SOLO los archivos que vos tocaste. Hay una lista larga de archivos que
  el owner tiene con cambios locales propios, sin commitear, de cosas no
  relacionadas — **NUNCA los toques ni los incluyas en un commit**:
  `ASSET_CREDITS.md`, `DEV_LOG.md`, varios `.mat` en `Assets/Settings/`,
  `Assets/_FolkloreArchives/layout_FullMap.json`,
  `ProjectSettings/QualitySettings.asset`, y varios builders en
  `Assets/editor/MapGenerator/` (`AreaPoiBuilder.cs`, `CampsiteBuilder.cs`,
  `ChickenCoopBuilder.cs`, `EnvironmentBuilder.cs`,
  `PsxDerelictFurnitureBuilder.cs`). Si `git status` los muestra modificados, son
  del owner — ignoralos.
- **Verificar balance de llaves** después de cada edit en un `.cs`:
  `grep -o "{" archivo.cs | wc -l` vs `grep -o "}" archivo.cs | wc -l` deben ser
  iguales. Barato y atrapa errores de edición antes de pedirle al owner que
  compile.
- **Atribución de commits**: terminan con
  `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- **Idioma**: el owner escribe en español (voseo argentino), con errores de
  tipeo/sin tildes seguido. Los comentarios de código y las respuestas van en
  español. El juego es PSX-horror ambientado en Argentina.

---

## 2. Patrón "Internal(bool interactive)" — CLAVE para todo lo nuevo

Repetido en TODA la sesión porque el owner insistió mucho: **"no quiero tener que
tocar botones a mano, quiero que todo se arme solo con Generate"**.

Cada pieza que se construye desde el Editor (puertas, ovejas, caja de
herramientas, etc.) sigue este patrón en `Assets/editor/RanchoNpcSetup.cs`:

```csharp
[MenuItem("Folklore/Nombre del botón")]
static void BotonManual() => AlgoInternal(interactive: true);

public static void AlgoInternal(bool interactive)
{
    // ... lógica ...
    // dentro: diálogos/Selection/Undo SOLO si (interactive), si no, Debug.Log/LogWarning
    if (!interactive) return;
    Undo.RegisterCreatedObjectUndo(...);
    Selection.activeGameObject = ...;
}
```

Todas las llamadas "Internal(false)" se disparan solas desde
**`RanchoNpcSetup.EnsureAllRanchoDoors(bool interactive)`**, que a su vez la
llama **`HouseBuilder.BuildBarn()`** (en `Assets/editor/MapGenerator/HouseBuilder.cs`)
automáticamente al final de cada "Generate Greybox Map", justo después de
`AbandonedFarmBuilder.Build()` + `ChickenCoopBuilder.Build()` + `CorralBuilder.Build()`.

`EnsureAllRanchoDoors` llama, en orden, con try/catch individual (si una falla no
frena a las demás):
1. `BuildHouseDoorInternal(false)` — puerta de la casa (`PuertaCasa`)
2. `BuildLetrinaDoorInternal(false)` — puerta de la letrina (llama a
   `LetrinaFixer.ReplaceLetrinaInternal` primero)
3. `BuildGateInternal(false)` — tranquera del corral (`TranqueraCorral`)
4. `PlaceSheepInternal(false)` — ovejas (`Ovejas` con hijos `Oveja_0..3`)
5. `PlaceToolboxInternal(false)` — caja de herramientas (`CajaHerramientas`)

**Si agregás algo nuevo que el owner necesite que se arme solo en cada Generate,
seguí este mismo patrón y sumalo a `EnsureAllRanchoDoors`.**

---

## 3. Bug recurrente #1: nombres auto-generados de Unity NO son estables

Aprendido a las patadas en esta sesión, varias veces: cuando un `.fbx` grande
(`AbandonedFarm.fbx`, 478 objetos) se instancia de nuevo en cada Generate, Unity
le pone nombres tipo `Cube.184`, `letrina.007`, `Object_8` a piezas sin nombre
propio — **y esos nombres/índices NO son confiables entre una corrida y la
siguiente** (ni su posición, ni a veces ni su tamaño/forma real). Pasó con:
- `letrina.007`/`.006` (posición) → se cayó en una tranquera de mundo FIJA.
- `Cube.184` (posición, y DESPUÉS tamaño/forma también) → tranquera armada a
  60+ unidades de donde iba, después "diminuta y de lado".

**Regla**: para cualquier pieza nueva que necesite posición/tamaño/rotación
consistente, **NO leas Transform/bounds de un objeto encontrado por nombre
dentro de un FBX grande instanciado de nuevo cada vez**. Pedile al owner una
coordenada de `TEST_PLAYER` y hardcodeala en una constante, como
`LetrinaAnchorPos`/`GateAnchorPos`/`ToolboxPos` (ver sección 5).

## 4. Bug recurrente #2: `GroundY()` puede "lanzar" personajes por aire

En `Assets/Scripts/CampsiteSequence.cs`, la función `GroundY(p, fallbackY, self)`
tira un raycast hacia abajo para saber a qué altura debe pisar un personaje. Si
ese rayo pega contra OTRO collider que está pasando por arriba en ese instante
(ej. la tranquera abriéndose, o cualquier puerta/objeto animado), la altura salta
de golpe — el personaje "vuela". Ya se agregó un clamp (si el resultado difiere
más de 1m del fallback, se ignora) pero **si aparece este bug de nuevo con OTRO
objeto animado**, es la misma causa: algo se movió por encima de un NPC justo
cuando se recalculó su altura.

## 5. Coordenadas ya establecidas (NO volver a pedirlas, ya están puestas)

### `Assets/Scripts/CampsiteSequence.cs` (campos públicos del componente, cerca del inicio)
```
houseDoorPos      = (136.1347, 27.24684, 125.4351)   yaw -178.982   // tocar la puerta de la casa
corralGateStand   = (116.6176, 26.97,    149.8931)                 // pararse para abrir la tranquera
sheepPasturePos   = (124.6704, 26.02135, 165.3348)                 // dónde pastan las ovejas
sheepGatePath[]   = 5 puntos (111.79,150.14) → (116.22,150.04) → (118.92,150.11)
                     → (126.73,161.68) → (123.53,164.96)           // cruce por la tranquera
atticScreamerPos  = (104.0223, 34.14001, 140.9066)                 // dispara el susto de la gallina
toolboxPos        = (112.6553, 34.93675, 139.7323)                 // mesa del ático, caja de herramientas
ranchoViejoPos    = (91.59354, 27.66895, 137.5355)    yaw -92.435  // dónde aparece el viejo (letrina)
oldLadySleepPos   = (143.5203, 28.13925, 116.112)     yaw -86.835  // vieja acostada hasta que la despiertan
oldLadyGreetPath[]= 3 puntos, el último (136.2163,125.2371) yaw -3.715 es donde
                     la vieja se QUEDA PARADA el resto de la escena (puerta casa)
viejoKitchenPath[]= ~9 puntos, ViejoDoorPathIndex marca el tramo que cruza PuertaCasa
```

### `Assets/editor/LetrinaFixer.cs`
```
LetrinaAnchorPos     = (93.202, 29.185, 137.6882)   yaw -4.809   // grupo "Letrina_Fresca" entero
LetrinaDoorLocalPos  = (-0.992, -1.410509, -0.62)                // pose LOCAL cerrada de letrina.007
LetrinaDoorLocalRot  = Euler(-90.137, 4.798996, -96.54599)
```

### `Assets/editor/RanchoNpcSetup.cs`
```
PuertaCasa: wp = (135.324, 27.9111, 125.3751)  wr = Euler(0,88.318,0)  ws = 1.35 (uniforme)
GateAnchorPos   = (115.6, 26.95807, 148.5784)   yaw -93.633
GateAnchorScale = (1.275079, 1, 1.3652)   // tranquera PROCEDURAL (2 parantes + 4 travesaños + refuerzo)
GateWidth=2.8  GateHeight=1.15  GateStile=0.09  GateRail=0.07
ToolboxPos = (112.6553, 34.93675+0.11, 139.7323)   yaw -5.715   // caja procedural (cajón + asa)
SheepHeight = 1.6   SheepModelYawOffset = 180 (AJUSTAR si caminan de costado, ver sección 6)
```

**Todas estas ya están en el código y se aplican solas en cada Generate.** No
hace falta volver a pedirlas — si algo se ve mal, es un tema de ajuste fino
(pedir una coordenada NUEVA, no repetir el proceso desde cero).

---

## 6. Estado del checkpoint 3 ("▶ 3 · Rancho (cañas)") — qué anda y qué falta

Flujo implementado y funcionando (confirmado por el owner tras varias rondas de
fixes), en `CampsiteSequence.RanchoBathroomScene()`:

1. Golpeás la puerta de la letrina 2 veces → "¡Ocupado!" → sale el viejo (susto,
   mismo clip `jumpscare` que Richard en la YPF).
2. Confrontación → pedís una caña → el viejo camina a despertar a la vieja
   (cruzando `PuertaCasa`, que se abre/cierra sola) MIENTRAS vos te movés libre.
3. La vieja se despierta, camina su `oldLadyGreetPath` hasta la puerta de la
   casa (se abre sola para ella) y ahí se queda parada.
4. Charla → pide sacar las ovejas a cambio de las cañas.
5. Abrís la tranquera (`[E] Abrir la tranquera`) → las 4 ovejas (grupo `Ovejas`)
   cruzan por `sheepGatePath` (escalonadas, fila india) hasta `sheepPasturePos`.
6. `_playerHint = "Volvé con la vieja"` → te acercás (proximidad, sin E) → te
   pide la caja de herramientas del granero, avisa de las gallinas.
7. Subís al ático → cerca de `atticScreamerPos` salta el susto de la gallina →
   llegás a `toolboxPos` → agarrás la caja (se desactiva el objeto).
8. Volvés con la vieja (proximidad) → se la entregás → **ACÁ TERMINA LO
   IMPLEMENTADO.**

### Pendiente de diseño (sin coordenadas, sin lógica — el owner no las dio):
1. **Arreglar el baño** — algún minigame con "la cadena" (¿tirar repetido con E?
   ¿QTE? ¿mantener apretado? — sin definir. ¿Es la cadena de la letrina que ya
   existe o algo nuevo del baño de la casa? — sin definir).
2. **Mates + historia de la Luz Mala** — escena sentados, la vieja/el viejo
   cuentan la leyenda de la Luz Mala (el fenómeno título del juego).
3. **Volver al campamento** — cierra el capítulo del rancho.

Comentario textual dejado en el código (buscar `// (sigue:` en
`CampsiteSequence.cs`, cerca del final de `RanchoBathroomScene()`) para ubicarlo
rápido.

### Pendiente de ajuste fino (cosas que funcionan pero pueden necesitar un toque):
- **Orientación del mesh de las ovejas**: `SheepModelYawOffset = 180f` es una
  PRIMERA PRUEBA (el owner reportó que caminaban de costado/sin apuntar bien
  antes de este ajuste; no llegó a confirmar si 180 lo arregló del todo). Si
  siguen mal, es tanteo rápido: probar 90 / -90 / 0 en esa constante — no hace
  falta Blender, es solo el offset del "Model" (hijo) contra el root que mueve
  la IA, en `RanchoNpcSetup.BuildSheep()`.
- **Ovejas sin huesos reales**: `sheep.obj` es un OBJ (formato SIN esqueleto
  posible). Se armó `Assets/Scripts/SheepWalkAnim.cs` como animación PROCEDURAL
  (bambolea el mesh entero, sube-baja + vaivén) en vez de patas de verdad. Si
  alguna vez el owner consigue/importa un modelo de oveja CON esqueleto, hay que
  colgarle un sistema como `Assets/Scripts/DogWalkAnim.cs` (que sí anima huesos
  reales por nombre — `Bone.003`, etc. — del rig `PS1_Dog.glb`).
- **Tranquera y caja de herramientas son PROCEDURALES** (cubos armados por
  código, no un asset 3D real) — el owner las aceptó como placeholder
  ("hace un procedural nomás por ahora") después de que 2 intentos con assets
  reales (`wooden_fence_closed.fbx`, `PT_Modular_Gate_Wood_01`) salieron mal
  (mal escalados/orientados, o "feo"). Si en algún momento aparece un asset
  mejor, reemplazar es sencillo: son funciones autocontenidas
  (`BuildGateInternal`, `PlaceToolboxInternal` en `RanchoNpcSetup.cs`).

---

## 7. Mapa rápido de archivos tocados esta sesión

| Archivo | Qué hace |
|---|---|
| `Assets/Scripts/CampsiteSequence.cs` | Guion completo de la misión (mission script central). `RanchoBathroomScene()` es la corrutina de todo el capítulo del rancho. |
| `Assets/editor/RanchoNpcSetup.cs` | TODOS los botones de Editor `Folklore/...` para armar puertas/NPCs/ovejas/caja — y `EnsureAllRanchoDoors`, el punto de entrada automático. |
| `Assets/editor/LetrinaFixer.cs` | Repone la letrina "fresca" (sin static-batch) en `LetrinaAnchorPos`. |
| `Assets/editor/MapGenerator/HouseBuilder.cs` | `BuildBarn()` llama a `RanchoNpcSetup.EnsureAllRanchoDoors(false)` al final — el gancho real hacia Generate. |
| `Assets/Scripts/CorralGate.cs` | Componente genérico de puerta con bisagra (E para abrir/cerrar) — usado por casa, letrina y tranquera. |
| `Assets/Scripts/SheepWalkAnim.cs` | Animación procedural (bamboleo) de las ovejas. |
| `Assets/editor/MapGenerator/DebugCheckpointButtons.cs` | Botones de Scene View para arrancar Play desde un checkpoint — el 3 es "Rancho (cañas)". |
| `Assets/Scripts/OpeningDriveSequence.cs` | `cp >= 3` salta a `CampsiteSequence.BeginAtRancho(this)`. |

---

## 8. Si algo "no aparece" o "no funciona" después de Generate

Chequeá en este orden (ya pasó TODO esto al menos una vez esta sesión):
1. ¿El owner corrió **Generate Greybox Map** de nuevo después del último cambio
   de código? (a veces prueban sin regenerar).
2. ¿Unity terminó de **recompilar**? (`Assets → Refresh` / `Ctrl+R` si el
   cambio se hizo por fuera del Editor — Unity a veces no detecta el archivo
   tocado externamente).
3. Buscar en la Console (filtro por `[Rancho]`) las líneas de log de
   `EnsureAllRanchoDoors` — cada builder loguea su resultado (posición armada,
   piezas desactivadas, etc.) o un `Debug.LogError` si falló (con la excepción
   completa).
4. Si algo se ve invisible/diminuto/mal orientado: pedir a los VALORES DE
   BOUNDS/transform ya construidos (Inspector), no asumir — puede ser el mismo
   bug de nombres-no-estables (sección 3).
