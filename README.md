# LA LUZ MALA

Horror en primera persona (estilo *Fears to Fathom*) ambientado en la Patagonia
argentina, sobre la leyenda folklórica de **La Luz Mala**. Unity 6 (URP).

Este repo versiona **solo el código y la configuración del proyecto** — el
generador procedural de mapa, los builders, los shaders y los scripts de runtime.
Los **asset packs de terceros NO están versionados** (son del Asset Store, pesados
y no redistribuibles). Hay que reimportarlos para que el proyecto compile y genere
el mapa.

## Requisitos

- **Unity 6000.3.18f1** (o compatible Unity 6 / URP 17.3)
- Render Pipeline: **URP 17.3**

## Assets a reimportar (Asset Store)

Instalar/importar en `Assets/` antes de generar el mapa:

| Pack | Uso |
|------|-----|
| **Polytope Studio – Lowpoly Environments (Free)** | Árboles/pasto/rocas low-poly. Importar además `PT_Nature_Free_URP_17.unitypackage` (shaders URP). |
| **AllSky Free** | Skyboxes día/noche. |
| **nappin – House Interior Pack** | Muebles de la casa (Fase 2). |
| **Terrain Sample Asset Pack** | Texturas/heightmaps de terreno. |
| **Nature Starter Kit 2** | Vegetación extra. |
| **Yughues Free Pavements / Free Bushes 2018** | Caminos y arbustos. |
| **Kajaman's Roads / EasyRoads3D** | Rutas. |
| **Conifers [BOTD] (Forst)** | Coníferas alternativas (no low-poly). |
| **klen – HQ Autumn Dry Maple / Dream Tree 2 / ALanTree** | Árboles alternativos. |

## Cómo generar el mapa

1. Abrir el proyecto en Unity 6.
2. Reimportar los packs de arriba.
3. `Tools > Folklore Archives > Importar Shaders Polytope URP` (una vez).
4. `Tools > Folklore Archives > Generate Greybox Map`.
5. Play: WASD + mouse, Shift = correr, F = linterna.

## Estructura del código

- `Assets/editor/MapGenerator/` — generador procedural (namespace `FolkloreArchives.MapGen`):
  `MapGenerator`, `MapLayout` (datos), y builders: `TerrainBuilder`, `ForestBuilder`,
  `EnvironmentBuilder`, `RoadsideBuilder`, `BridgeBuilder`, `TunnelBuilder`,
  `LandmarkBuilder`, `HouseBuilder`, `StoryTriggerBuilder`, `TestPlayerBuilder`.
- `Assets/Scripts/` — runtime (namespace `FolkloreArchives`): `MapExplorer`,
  `DayNightController`, `GameSettings`, `SettingsMenu`, `VhsPostFx`, y shaders
  (`GrassFade`, VHS, etc.).
- `Assets/Settings/` — assets de config de URP.
