# LA LUZ MALA — Greybox Map for Unity 6 (URP)

Modular, all in English, laid out to match the hand-drawn plan:

```
              [MAIN CRIMINAL CAMP]────────[GRAVE]
                 (hill, top-left)            │
                     │                    (Path A)
              [SECONDARY CAMP]               │
                     │                 [OLD LADY'S RANCH]
                  (Path B — dark,            │
                   closed tunnel)         (Path A)
                     └────────────────►[CAMPSITE]··(beach path)··[BEACH]≈[RIVER]
                                             │                    (fishing)
                                        (dirt road)              curvy S-bends
                                             │
              ═══════════════[PAVED ROUTE]═══╧══════════════
                              (asphalt, lakeside + guardrail)
```

## Install

1. Open your Unity 6 project (Universal 3D / URP template).
2. In the Project window create these folders inside `Assets`:
   - `Assets/Editor/MapGenerator`
   - `Assets/Scripts`
3. For each `.cs` file below, create a C# script with the **exact same name** in the folder indicated, open it, delete everything and paste the file's content:

| File | Unity folder |
|---|---|
| MapLayout.cs | Assets/Editor/MapGenerator |
| BuilderUtils.cs | Assets/Editor/MapGenerator |
| TerrainBuilder.cs | Assets/Editor/MapGenerator |
| EnvironmentBuilder.cs | Assets/Editor/MapGenerator |
| ForestBuilder.cs | Assets/Editor/MapGenerator |
| LandmarkBuilder.cs | Assets/Editor/MapGenerator |
| StoryTriggerBuilder.cs | Assets/Editor/MapGenerator |
| TestPlayerBuilder.cs | Assets/Editor/MapGenerator |
| MapGenerator.cs | Assets/Editor/MapGenerator |
| MapExplorer.cs | **Assets/Scripts** (NOT inside Editor!) |
| DayNightController.cs | **Assets/Scripts** (NOT inside Editor!) |
| VhsPostFx.cs / TorchFlicker.cs / VhsOverlay.cs | **Assets/Scripts** |

4. Wait for Unity to compile, then use the menu **Tools > Folklore Archives > Generate Greybox Map**.
5. Press **Play**: WASD move, mouse look, Shift run, F flashlight, **Tab day/night**, Esc release cursor.

> If Play throws an Input error: **Edit > Project Settings > Player > Active Input Handling** → set to **Both**, restart Unity if asked.

If you pasted the older Spanish scripts, delete them first (`GeneradorMapaLuzMala.cs`, `ExploradorMapa.cs`).

## Architecture (why it scales)

- **MapLayout.cs** — pure data: every location, path and tuning value. Move anything here, regenerate, done. No other file hardcodes coordinates.
- **MapGenerator.cs** — entry point/menu; calls each builder in order.
- **TerrainBuilder** — heightmap (criminal-camp hill, riverbed, flat campsite, lakeside embankment, **fishing beach**) + ground painting (asphalt road via Kajaman's Roads, dirt, grass, dry grass, worn trail).
- **EnvironmentBuilder** — night: moon, fog, ambient, river water, procedural dusk skybox. Also builds the **day skybox** (warm golden-hour gradient with lit mountains + clouds) used by the day/night toggle.
- **ForestBuilder** — real trees are the **Conifers [BOTD]** URP prefabs (`Assets/Forst/`), used raw so they keep their wind + LODs + billboards (cheap far rendering). Falls back to AlanTree/klen Maple/Dream Tree 2, then to procedural sphere/cylinder trees if none are present. Density/type driven by distance to paths: Path B + criminal trails get a dense forest tunnel (scary), Path A gets a green tunnel, hunting field stays open. Ground clutter (logs/rocks) is baked into 2 combined static meshes. See `DEV_LOG.md` for material wiring + the batching/perf history.
- **RoadsideBuilder** — the paved route's real road mesh (arc-length UVs so the lane lines follow the curve), plus the lakeside guardrail (combined into 2 static meshes: posts + beam) and lake.
- **LandmarkBuilder** — props and markers for every story location, including item points (`RUFUS_DIG_POINT`, `CAR_PART_POINT_1..3`, `WATER_BOTTLE_POINT`, `JOURNAL_POINT`) and spawn points (`SPAWN_PLAYER1`, `SPAWN_RUFUS`, `SPAWN_CRIMINAL_1..3`, `SPAWN_CAR_START`). Every fixed prop is marked static for batching.
- **StoryTriggerBuilder** — invisible trigger boxes named `TRIGGER_ACT..._...` — hook your story/checkpoint manager to these.
- **TestPlayerBuilder + MapExplorer** — disposable first-person tester; replace with the real Player 1 / Rufus controllers later. Also attaches **DayNightController** (Tab toggles day/night at runtime).

Everything generated lives under one root object `FOLKLORE_MAP`; regenerating replaces it cleanly. Materials/terrain assets are saved in `Assets/_FolkloreArchives/Generated`.

### Day / Night preview

The scene is authored as a dark Fears-to-Fathom night, but there is also a
**golden-hour day** mode for daytime scenes:

- **In Play mode:** press **Tab** to toggle (handled by `DayNightController` on the test player).
- **In the Editor (not playing):** click the **☀/☽ button** in the top-right of the Scene view, or press **Ctrl+Shift+D**, or use **Tools > Folklore Archives > Toggle Day-Night Preview**.

Day mode swaps the skybox (warm gradient + lit mountains + clouds), warms the
sun, and uses **linear fog** so distant grass fades into haze instead of popping
at its cull distance. Camera far-clip, grass and tree render distances are also
pulled in for day so nothing renders past where the fog already hides it.

### External asset dependencies (optional but recommended)

The generator degrades gracefully (procedural placeholders) if these aren't present, but looks much better with them:

- `Assets/Forst/Conifers [BOTD]/` — the mountain-pine terrain trees (with wind + LODs + billboards). This is the primary tree source now.
- `Assets/KajamansRoads/` — dark asphalt + lane-marking textures for the paved route.
- `Assets/NatureStarterKit2/` — Asset Store "Nature Starter Kit 2"; `Textures/ground02.tga` textures the dirt road + beach path.
- Unity's "Terrain Sample Asset Pack" under `Assets/TerrainSampleAssets/` — grass/rock/dry-grass terrain layers and grass/fern/bush detail meshes.
- Optional extra tree packs (currently OFF, kept as fallbacks): `Assets/ExternalAssets/ALanTree/`, `Assets/klen/`, `Assets/DreamTree2/`, `Assets/YughuesFreeBushes2018/`.

See `DEV_LOG.md` for the running history of what changed, why, and what's still unverified.

## Tuning quick reference (all in MapLayout.cs)

- `Seed` — different tree/clutter layout.
- `FogDensity` (night) / `DayFogStart` + `DayFogEnd` (day, linear) — mood + how far you see.
- `ScaryPathTreeDensity` / `PathATreeDensity` / `ForestTreeDensity` — forest thickness.
- `TreeGridStep` — spacing between candidate tree slots (lower = denser, heavier).
- `TreeBillboardDistance` — full-mesh radius before cheap billboards kick in (perf lever).
- `DetailRenderDistance` / `DayDetailRenderDistance` — how far grass draws (biggest grass FPS lever).
- `RiverControls` — the river's shape (Catmull-Rom control points); `RiverBeach` — fishing-beach spot.
- Location `Vector2`s — move any zone; paths reference them so they follow along.

## Performance notes

- Terrain trees don't cast shadows (`terrain.castShadows = false`) — the dim moon's shadows were invisible but costly.
- Guardrail, ground clutter and fixed props are combined/marked static so they batch into a handful of draw calls instead of hundreds.
- Water surfaces disable shadow casting; realtime shadow distance is capped (`ShadowDistance`, on the URP asset).
- 44→? FPS work is ongoing; verify in a **standalone build** (File > Build Profiles > Build And Run), not just the Editor — the Editor adds ~30-40% overhead.

## Next steps I can generate

1. Player 1 + Rufus controllers (scent trails, digging, marking, wall-vision).
2. Luz Mala AI (white/passive ↔ red/aggressive) + criminals with flashlight detection.
3. Story/act manager wired to the generated triggers (checkpoints, scripted massacre, good/bad ending).
4. Online co-op with Netcode for GameObjects + microphone detection.
