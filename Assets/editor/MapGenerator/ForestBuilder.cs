// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  ForestBuilder.cs — REAL dense forest using Unity's terrain
//  tree system (thousands of instanced trees) + waving grass
//  details. Path B & criminal trails = closed dry tunnel,
//  Path A = green tunnel, hunting field = open dry grassland.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class ForestBuilder
    {
        // Borra los árboles/pasto guardados en el terreno → el próximo Generate los
        // regenera. Usar tras cambiar árboles, pasto, densidad, o flags PSX.
        [MenuItem("Tools/Folklore Archives/Rebuild Forest (forzar)")]
        public static void ForceRebuildForest()
        {
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBuilder.TerrainAssetPath);
            if (td == null) { Debug.LogWarning("No hay terreno cacheado (regenerá primero)."); return; }
            td.SetTreeInstances(new TreeInstance[0], true);
            td.detailPrototypes = new DetailPrototype[0];
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=lime>Bosque borrado del terreno — el próximo Generate lo regenera (~80s).</color>");
        }

        public static void Build(Transform parent, Terrain terrain)
        {
            var td = terrain.terrainData;

            // CACHE del bosque: los árboles y el pasto quedan guardados DENTRO del
            // terreno. Si el terreno vino cacheado (ya tiene árboles de un build
            // anterior), saltamos todo el scatter (~79s). Para rehacerlo tras cambiar
            // árboles/pasto/densidad: Tools > Folklore Archives > Rebuild Forest.
            bool forestCached = td.treeInstanceCount > 0;
            if (!forestCached)
            {
            if (MapLayout.UseLowPolyTrees) ConvertLowPolyMaterialsToURP(); // arregla el rosa (Built-in → URP)

            // ForestPack.fbx's tree mesh (~88k tris, only 1 qualifying prototype)
            // was a dead end: too heavy raw, broke visually when decimated, and
            // came out oversized/wrong-shaded when used undecimated. Replaced with
            // AlanTree.fbx (Assets/ExternalAssets/ALanTree) - a normal, lightweight
            // single-tree asset (~1.2MB fbx) meant for exactly this kind of use.
            // Owner then added two more tree packs (klen "HQ Autumn Dry Maple Trees"
            // + Dream Tree 2) specifically to mix in with AlanTree for variety, so all
            // three real-tree sources get pooled together and picked from uniformly -
            // AlanTree's share of the mix just naturally shrinks as more get added.
            var realTreeList = new List<GameObject>();
            // 100% Conifers [BOTD] (Assets/Forst) - the Fears-to-Fathom mountain-pine
            // forest. Going conifer-ONLY is both the right look AND a big optimization:
            // the BOTD conifers ship terrain-tree BILLBOARDS (cheap far-distance
            // rendering), which we can now enable because there are no baked broadleaf
            // trees left in the mix to render broken as billboards. Used raw (not
            // baked) so the CTI shader keeps its wind + LODs + billboards.
            // Árboles: PSX (StarkCrafts) con sus texturas REALES extraídas del FBX. Las
            // texturas venían embebidas en el FBX y Unity no las extrajo (por eso salían
            // blancos). Ya extraídas a PSX_ExtractedTex. Pinos (PSX_Tree1/4) + frondosos
            // (PSX_Tree2/3, reactivados para el lado campo — ver BuildPsxTreePrototypes).
            int psxPineCount = 0;
            GameObject[] psxTrees = MapLayout.UsePsxTrees ? BuildPsxTreePrototypes(out psxPineCount) : null;
            if (psxTrees != null)
            {
                realTreeList.AddRange(psxTrees);
            }
            else if (MapLayout.UseLowPolyTrees)
            {
                var lp = BuildLowPolyTreePrototypes();
                if (lp != null) realTreeList.AddRange(lp);
                else { var c = BuildConiferPrototypes(); if (c != null) realTreeList.AddRange(c); } // fallback
            }
            else
            {
                var conifers = BuildConiferPrototypes();
                if (conifers != null) realTreeList.AddRange(conifers);
            }

            // Broadleaf trees (AlanTree / klen maple / Dream Tree 2) are left OUT of the
            // mix for now - they have no billboard so they'd force billboarding off for
            // the whole terrain. Re-add these lines to bring them back:
            //   var alanTrees = BuildALanTreePrototypes();   if (alanTrees  != null) realTreeList.AddRange(alanTrees);
            //   var mapleTrees = BuildKlenMapleTreePrototypes(); if (mapleTrees != null) realTreeList.AddRange(mapleTrees);
            //   var dreamTrees = BuildDreamTreePrototypes(); if (dreamTrees != null) realTreeList.AddRange(dreamTrees);

            var realTrees = realTreeList.Count > 0 ? realTreeList.ToArray() : null;
            int realTreeCount = realTrees != null ? realTrees.Length : 0;
            // El split OESTE(campo/frondoso)/ESTE(bosque/pino) solo aplica cuando el pool
            // activo es justo el de PSX (pino+frondoso); para cualquier otro pool
            // (fallback low-poly/conífera) no hay noción de "frondoso" -> sin split.
            int pineCountForSplit = (psxTrees != null) ? psxPineCount : realTreeCount;

            var protos = new List<TreePrototype>();
            int greenIndex, dryIndex;
            if (realTrees != null)
            {
                // real trees only - no procedural trees mixed in (owner's request)
                foreach (var t in realTrees) protos.Add(new TreePrototype { prefab = t, bendFactor = 0f });
                greenIndex = dryIndex = 0; // unused when realTreeCount > 0
            }
            else
            {
                // fallback only if every real tree asset is missing entirely
                Debug.LogWarning("No real tree assets found (AlanTree/klen Maple/Dream Tree 2) - falling back to procedural trees.");
                greenIndex = protos.Count;
                protos.Add(new TreePrototype { prefab = GreenTreePrefab(), bendFactor = 0f });
                dryIndex = protos.Count;
                protos.Add(new TreePrototype { prefab = DryTreePrefab(), bendFactor = 0f });
            }
            // Bushes (Yughues Free Bushes) get their own prototype slots after the
            // trees, and their own independent scatter pass/density - undergrowth,
            // not part of the tree density/mix above.
            int bushProtoStart = protos.Count;
            var bushes = BuildYughuesBushPrototypes();
            int bushProtoCount = bushes != null ? bushes.Length : 0;
            if (bushes != null) foreach (var b in bushes) protos.Add(new TreePrototype { prefab = b, bendFactor = 0f });

            td.treePrototypes = protos.ToArray();
            Debug.Log("ForestBuilder: tree prototype mix = " + realTreeCount + " real tree(s) ("
                + pineCountForSplit + " pino / " + (realTreeCount - pineCountForSplit) + " frondoso), "
                + bushProtoCount + " bush(es).");

            var instances = ScatterTrees(realTreeCount, pineCountForSplit, greenIndex, dryIndex);
            ScatterBushes(bushProtoStart, bushProtoCount, instances);
            td.SetTreeInstances(instances.ToArray(), true);
            Debug.Log("Forest: " + instances.Count + " tree/bush instances planted.");
            SetupGrass(td);
            }
            else Debug.Log("Bosque cacheado (árboles/pasto ya en el terreno) — Rebuild Forest para rehacer.");

            // fade del césped: por defecto (noche) el corte es DetailRenderDistance.
            // El toggle día/noche re-setea estas globales para seguir su distancia.
            SetGrassFadeGlobals(MapLayout.DetailRenderDistance);

            terrain.treeDistance = MapLayout.TreeRenderDistance;
            // low-poly no tiene billboards → billboard distance = render distance (siempre malla)
            terrain.treeBillboardDistance = MapLayout.UseLowPolyTrees ? MapLayout.TreeRenderDistance : MapLayout.TreeBillboardDistance;
            // menos árboles a malla completa a la vez -> más billboards baratos ->
            // descarga el dibujado del bosque denso (el mayor pico de FPS mínimo).
            // El crossfade evita el "salto" malla<->billboard.
            terrain.treeMaximumFullLODCount = MapLayout.TreeMaxFullLOD;
            terrain.treeCrossFadeLength = MapLayout.TreeCrossFade;
            ApplyTreeLeafSettings(); // translucidez on/off según MapLayout.TreeLeafTranslucency
            terrain.detailObjectDistance = MapLayout.DetailRenderDistance;
            terrain.detailObjectDensity = MapLayout.DetailDensity;
            terrain.basemapDistance = MapLayout.TerrainBasemapDistance;
            terrain.castShadows = false; // sombras de luna (0.16 intensidad) son invisibles en negro; eliminar el shadow map pass de ~66k árboles
            terrain.Flush();
            AddTreeWind(parent); // driver de viento CTI para que las hojas se muevan (casi gratis)

            ScatterClutter(parent, terrain);
            // ScatterPuddles(parent, terrain); // disabled: flat quads can't fake wet
            // reflective puddles at night without planar reflection / SSR - they just
            // read as floating grey tiles. Re-enable if we later add a puddle decal
            // asset or a proper wet-ground shader.
        }

        // ---------------- MUD PUDDLES ----------------
        // Flat, dark, glossy water quads that collect in the dirt-road ruts and low
        // spots. A high-smoothness Lit material makes them reflect the dusk sky and
        // catch a bright specular glint from the flashlight - the wet, muddy FtF road.
        static void ScatterPuddles(Transform parent, Terrain terrain)
        {
            var root = new GameObject("Puddles").transform;
            root.SetParent(parent);

            // transparent, soft-edged, very glossy water. The radial-alpha texture kills
            // the hard square edge; high smoothness makes it glint under the flashlight
            // and reflect the (dark blue) sky = a wet puddle, not a black tile.
            string matPath = MapLayout.GeneratedFolder + "/mat_puddle.mat";
            AssetDatabase.DeleteAsset(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "puddle_water" };
            var tex = PuddleTexture();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            var col = new Color(0.06f, 0.09f, 0.13f, 1f);
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
            // transparent surface (soft edges from the texture alpha)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.enableInstancing = true;
            AssetDatabase.CreateAsset(mat, matPath);

            Random.InitState(MapLayout.Seed + 999);
            int placed = 0;

            // 1) puddles sitting in the two worn wheel ruts along the dirt road
            var pts = MapLayout.DirtRoad;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                int steps = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / 3f));
                for (int s = 0; s < steps; s++)
                {
                    if (Random.value > MapLayout.PuddleRoadChance) continue;
                    Vector2 c = Vector2.Lerp(a, b, s / (float)steps);
                    Vector2 t = (b - a).normalized;
                    Vector2 n = new Vector2(-t.y, t.x);
                    float rut = (Random.value < 0.5f ? -1.1f : 1.1f) + Random.Range(-0.3f, 0.3f);
                    Vector2 pp = c + n * rut + t * Random.Range(-1f, 1f);
                    PlacePuddle(root, terrain, mat, pp, Random.Range(0.8f, 1.8f), Random.Range(0.5f, 0.9f));
                    placed++;
                }
            }

            // 2) a few scattered puddles in low forest spots
            float step = MapLayout.PuddleGridStep;
            for (float x = 30f; x < MapLayout.MapSizeX - 30f; x += step)
                for (float z = 30f; z < MapLayout.MapSize - 30f; z += step)
                {
                    var p = new Vector2(x + Random.Range(-step * 0.4f, step * 0.4f), z + Random.Range(-step * 0.4f, step * 0.4f));
                    if (BuilderUtils.DistToRivers(p) < 26f) continue;
                    if (Random.value > MapLayout.PuddleForestChance) continue;
                    PlacePuddle(root, terrain, mat, p, Random.Range(1.0f, 2.6f), Random.Range(0.6f, 1.0f));
                    placed++;
                }

            Debug.Log("Puddles: " + placed + " placed.");
        }

        // soft radial alpha (opaque centre, fades to transparent at the edge, with a
        // slightly ragged/noisy border) so puddles blend into the ground.
        static Texture2D PuddleTexture()
        {
            string path = MapLayout.GeneratedFolder + "/tex_puddle.asset";
            AssetDatabase.DeleteAsset(path); // always rebuild so tweaks to the alpha apply

            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (S * 0.5f); // 0 centre -> 1 edge
                    float ragged = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.15f;
                    // fully OPAQUE across most of the puddle, only the outer rim fades -
                    // (was too see-through). Solid dark water surface.
                    float a = 1f - Mathf.SmoothStep(0.78f - ragged, 1f, d);
                    tex.SetPixel(x, y, new Color(0.04f, 0.06f, 0.09f, Mathf.Clamp01(a)));
                }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static void PlacePuddle(Transform root, Terrain terrain, Material mat, Vector2 p, float size, float aspect)
        {
            // a flat quad can't follow bumpy ground, so only place on fairly FLAT spots
            // (sample the height at the puddle's four edges; skip if it's too sloped).
            float r = size * 0.5f;
            float hC = BuilderUtils.Ground(terrain, p.x, p.y).y;
            float hMin = hC, hMax = hC;
            foreach (var o in new[] { new Vector2(r, 0), new Vector2(-r, 0), new Vector2(0, r), new Vector2(0, -r) })
            {
                float hh = BuilderUtils.Ground(terrain, p.x + o.x, p.y + o.y).y;
                hMin = Mathf.Min(hMin, hh); hMax = Mathf.Max(hMax, hh);
            }
            if (hMax - hMin > 0.35f) return; // too sloped for a flat puddle - skip

            var q = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10x10, normal up
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Puddle";
            q.transform.SetParent(root);
            // sit at the LOW point + a hair, so the whole quad is at/just under the
            // surrounding ground (soft edges tuck into the mud/grass, no floating)
            q.transform.position = new Vector3(p.x, hMin + 0.015f, p.y);
            q.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            q.transform.localScale = new Vector3(size / 10f, 1f, size * aspect / 10f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------------- GROUND CLUTTER (fallen logs + rocks) ----------------
        // FtF forest floors are messy: fallen logs, rocks, roots. Scatter simple
        // greybox logs/rocks across the forest floor (off paths) for that natural
        // "not a clean lawn" look. Real GameObjects (logs lie down, so they can't be
        // terrain trees which only rotate on Y), frustum + far-clip culled at runtime.
        static void ScatterClutter(Transform parent, Terrain terrain)
        {
            var root = new GameObject("GroundClutter").transform;
            root.SetParent(parent);

            var barkMat = BuilderUtils.MatTextured("clutter_log", BarkTexture(), new Color(0.42f, 0.35f, 0.27f), 0.08f);
            var rockMat = BuilderUtils.Mat("clutter_rock", new Color(0.30f, 0.31f, 0.33f), 0f);
            var cylinderMesh = BuilderUtils.PrimitiveMesh(PrimitiveType.Cylinder);
            var sphereMesh = BuilderUtils.PrimitiveMesh(PrimitiveType.Sphere);
            // low-poly: rocas del pack en vez de esferas (misma técnica de combine)
            if (MapLayout.UseLowPolyTrees)
            {
                var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Rocks/PT_Generic_Rock_01.prefab");
                var mf = rockPrefab != null ? rockPrefab.GetComponentInChildren<MeshFilter>() : null;
                var mr = rockPrefab != null ? rockPrefab.GetComponentInChildren<MeshRenderer>() : null;
                if (mf != null && mf.sharedMesh != null) sphereMesh = mf.sharedMesh;
                if (mr != null && mr.sharedMaterial != null) rockMat = mr.sharedMaterial;
            }

            // Was one GameObject per log/rock (thousands, across ~6600 grid cells) with
            // no batching or shadow-casting disabled - the same "thousands of unbatched
            // primitives" pattern that caused the original 134M-tri/42FPS incident
            // (DEV_LOG.md), just here instead of trees. Collected into CombineInstance
            // lists and baked into 2 combined static meshes at the end instead.
            var logCombines = new List<CombineInstance>();
            var rockCombines = new List<CombineInstance>();

            Random.InitState(MapLayout.Seed + 777);
            float step = MapLayout.ClutterGridStep;
            int placed = 0;
            for (float x = 20f; x < MapLayout.MapSizeX - 20f; x += step)
            {
                for (float z = 20f; z < MapLayout.MapSize - 20f; z += step)
                {
                    var p = new Vector2(x + Random.Range(-step * 0.5f, step * 0.5f), z + Random.Range(-step * 0.5f, step * 0.5f));
                    // keep clutter off the roads, river and right at spawn
                    if (BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute) < 12f) continue;
                    if (MapLayout.PavedRouteZAt(p.x) - p.y > 5f) continue; // lakeside embankment/water
                    if (BuilderUtils.DistToPolyline(p, MapLayout.DirtRoad) < 4f) continue;
                    if (BuilderUtils.DistToPolyline(p, MapLayout.PathA) < 3f) continue;
                    if (BuilderUtils.DistToRivers(p) < 26f) continue;
                    if (BuilderUtils.DistToPolyline(p, MapLayout.BeachPath) < 4f) continue;
                    if (Vector2.Distance(p, MapLayout.RiverBeach) < 14f) continue;
                    if (Vector2.Distance(p, MapLayout.Campsite) < 10f) continue;
                    if (Random.value > MapLayout.ClutterDensity) continue;

                    Vector3 pos = BuilderUtils.Ground(terrain, p.x, p.y);

                    if (Random.value < 0.5f)
                    {
                        // fallen log (cylinder laid on its side)
                        float rad = Random.Range(0.18f, 0.34f);
                        float halfLen = Random.Range(1.3f, 2.6f);
                        var rot = Quaternion.Euler(90f, Random.Range(0f, 360f), Random.Range(-6f, 6f));
                        var m = Matrix4x4.TRS(pos + Vector3.up * (rad * 0.8f), rot, new Vector3(rad * 2f, halfLen, rad * 2f));
                        logCombines.Add(new CombineInstance { mesh = cylinderMesh, transform = m });
                        placed++;
                    }
                    else
                    {
                        // a little cluster of rocks
                        int n = Random.Range(1, 4);
                        for (int i = 0; i < n; i++)
                        {
                            float s = Random.Range(0.35f, 1.0f);
                            var rp = new Vector2(p.x + Random.Range(-1.2f, 1.2f), p.y + Random.Range(-1.2f, 1.2f));
                            Vector3 rpos = BuilderUtils.Ground(terrain, rp.x, rp.y);
                            rpos -= Vector3.up * (s * 0.25f); // sink slightly
                            var rscale = new Vector3(s * Random.Range(0.9f, 1.4f), s * Random.Range(0.5f, 0.8f), s * Random.Range(0.9f, 1.4f));
                            var m = Matrix4x4.TRS(rpos, Random.rotation, rscale);
                            rockCombines.Add(new CombineInstance { mesh = sphereMesh, transform = m });
                        }
                        placed++;
                    }
                }
            }
            BuilderUtils.BuildCombinedStatic(root, "ClutterLogs", logCombines, barkMat);
            BuilderUtils.BuildCombinedStatic(root, "ClutterRocks", rockCombines, rockMat);
            Debug.Log("GroundClutter: " + placed + " log/rock clusters placed.");
        }

        // ---------------- TREES (terrain instances) ----------------

        // pineCount: cuántos de los [0..realTreeCount) prototipos "reales" son pino
        // (índices [0,pineCount)) vs frondoso (índices [pineCount,realTreeCount)).
        // Si pineCount==realTreeCount no hay frondosos disponibles -> sin split.
        static List<TreeInstance> ScatterTrees(int realTreeCount, int pineCount, int greenIndex, int dryIndex)
        {
            bool hasForestSplit = pineCount > 0 && pineCount < realTreeCount;
            int broadleafCount = realTreeCount - pineCount;

            // OESTE (campo argentino, x < ForestSplitX) = frondoso; ESTE (bosque/peligro)
            // = pino. Owner: "pongamos mitad y mitad" — el río corre aprox por el medio
            // del mapa (MapLayout.ForestSplitX), separando los dos lados ya documentados
            // en MapLayout ("OESTE = humano, ESTE = peligro").
            int PickRealTreeIndex(float x)
            {
                if (!hasForestSplit) return Random.Range(0, realTreeCount);
                bool west = x < MapLayout.ForestSplitX;
                return west ? pineCount + Random.Range(0, broadleafCount) : Random.Range(0, pineCount);
            }

            var trees = new List<TreeInstance>();
            float step = MapLayout.TreeGridStep;
            float jitter = step * 0.4f;
            for (float x = 10f; x < MapLayout.MapSizeX - 10f; x += step)
            {
                for (float z = 10f; z < MapLayout.MapSize - 10f; z += step)
                {
                    var p = new Vector2(x + Random.Range(-jitter, jitter), z + Random.Range(-jitter, jitter));

                    // LÍNEA DE ÁRBOLES: en las montañas altas (picos del lago central,
                    // laderas) no crecen árboles arriba de cierta altura — deja ver la
                    // roca/nieve del terreno (montaña real, no loma toda verde).
                    if (TerrainBuilder.HeightAt(p.x, p.y) > MapLayout.TreeLine) continue;

                    // LAKESIDE SHORE (between the guardrail and the water): a few small
                    // young pines scattered on the grassy embankment. Handled before the
                    // road exclusion below so they can grow right behind the guardrail.
                    float southD = MapLayout.PavedRouteZAt(p.x) - p.y;
                    if (southD > MapLayout.ShoreVegFar) continue; // out in the water
                    if (southD > MapLayout.ShoreVegNear)
                    {
                        if (realTreeCount > 0 && Random.value < MapLayout.ShorePineDensity)
                        {
                            float ss = Random.Range(0.26f, 0.46f); // small shoreline pines
                            float st = Random.Range(0.72f, 1.08f);
                            trees.Add(new TreeInstance
                            {
                                position = new Vector3(p.x / MapLayout.MapSizeX, 0f, p.y / MapLayout.MapSize),
                                prototypeIndex = PickRealTreeIndex(p.x),
                                heightScale = ss,
                                widthScale = ss * Random.Range(0.8f, 1.2f),
                                rotation = Random.Range(0f, Mathf.PI * 2f),
                                color = new Color(st, st, st),
                                lightmapColor = Color.white
                            });
                        }
                        continue;
                    }

                    // keep paths, river and clearings free. Se habia ensanchado a 9f/6f
                    // por "estan muy cerca de los caminos", pero el owner pidio volver a
                    // como estaba ("vuelve a poner la cantidad de arboles en los
                    // caminos") -> de nuevo 3.5f/3f, arboles otra vez hasta el borde.
                    if (BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute) < 13f) continue;
                    float dRoad = BuilderUtils.DistToPolyline(p, MapLayout.DirtRoad);
                    float dA = BuilderUtils.DistToPolyline(p, MapLayout.PathA);
                    float dScary = BuilderUtils.DistToScaryPaths(p);
                    float dExtra = BuilderUtils.DistToExtraTrails(p); // caminos nuevos del owner
                    if (dRoad < 3.5f || dA < 3.5f || dScary < 3f || dExtra < 3.5f) continue;
                    if (BuilderUtils.DistToRivers(p) < 28f) continue;
                    // despejar el caminito a la playa y la playa misma
                    if (BuilderUtils.DistToPolyline(p, MapLayout.BeachPath) < 5f) continue;
                    if (Vector2.Distance(p, MapLayout.RiverBeach) < 15f) continue;
                    // lago + orilla: sin árboles en el agua ni en toda la playa plana
                    // (antes solo despejaba 28m, mucho menos que la playa real de
                    // CentralLakeBeachWidth -> quedaba una hilera de árboles metida en la
                    // orilla/agua, owner: "quita arboles alrededor del lago")
                    if (Vector2.Distance(p, MapLayout.CentralLakeCenter) < MapLayout.CentralLakeRadius + MapLayout.CentralLakeBeachWidth + 10f) continue;
                    // montañas del lago (CentralPeaks): despeje por proximidad DESACTIVADO
                    // junto con el asset de montaña (MapGenerator.cs — owner: "quitalas").
                    // Reactivar junto con MountainRingBuilder.BuildCentralLakeMountains.
                    if (Vector2.Distance(p, MapLayout.WrongTurnDeath) < 8f) continue;
                    if (Vector2.Distance(p, MapLayout.LakeLookout) < 9f) continue;
                    if (Vector2.Distance(p, MapLayout.AbandonedCabin) < 11f) continue;
                    // these clearing radii used to be 26/15/22/13 - with the torch/tree
                    // render distance now only ~16m, a 26m clearing meant the treeline
                    // was always beyond render range, so it looked like trees "never
                    // spawn near the player" right at spawn (which sits at Campsite).
                    // Shrunk so the treeline is actually visible from these locations.
                    if (Vector2.Distance(p, MapLayout.Campsite) < 12f) continue;
                    // despejar TODO el lote de la casa de la vieja (dentro del cerco),
                    // no solo el centro, así el patio/perímetro queda libre
                    if (p.x > MapLayout.OldLadyLotMin.x - 1f && p.x < MapLayout.OldLadyLotMax.x + 1f &&
                        p.y > MapLayout.OldLadyLotMin.y - 1f && p.y < MapLayout.OldLadyLotMax.y + 1f) continue;
                    // sin árboles sobre la casa ni el galpón (huella + margen)
                    if (MapLayout.InRect(p, MapLayout.OldLadyHouseFootMin, MapLayout.OldLadyHouseFootMax, 3f)) continue;
                    if (MapLayout.InRect(p, MapLayout.OldLadyBarnFootMin, MapLayout.OldLadyBarnFootMax, 3f)) continue;
                    if (Vector2.Distance(p, MapLayout.MainCriminalCamp) < 12f) continue;
                    if (Vector2.Distance(p, MapLayout.HostageArea) < 6f) continue;
                    // ÁREAS NUEVAS abiertas (MapPlan): sin árboles vivos para que se lean distintas
                    if (Vector2.Distance(p, MapLayout.EstepaCenter) < 38f) continue; // estepa = campo abierto
                    if (Vector2.Distance(p, MapLayout.Mallin) < 22f) continue;       // pantano (mallín)
                    if (Vector2.Distance(p, MapLayout.Roquedal) < 20f) continue;     // roquedal (piedra)
                    if (Vector2.Distance(p, MapLayout.BurntForest) < 24f) continue;  // quemado: solo troncos negros (props)
                    if (Vector2.Distance(p, MapLayout.Estancia) < 16f) continue;     // casco de estancia
                    if (Vector2.Distance(p, MapLayout.Corrales) < 14f) continue;     // corrales
                    if (MapLayout.InYpfPad(p)) continue;                             // lote de la estación YPF

                    bool inField = Vector2.Distance(p, MapLayout.HuntingField) < 45f;
                    float prob;
                    bool dryTree;
                    if (dScary < 20f)      { prob = MapLayout.ScaryPathTreeDensity; dryTree = Random.value < 0.85f; }
                    else if (dA < 18f || dRoad < 18f || dExtra < 16f) { prob = MapLayout.PathATreeDensity; dryTree = Random.value < 0.35f; }
                    else if (inField)      { prob = MapLayout.FieldTreeDensity; dryTree = true; }
                    else                   { prob = MapLayout.ForestTreeDensity; dryTree = Random.value < 0.45f; }
                    if (Random.value > prob) continue;

                    bool pickedReal = realTreeCount > 0 && Random.value < MapLayout.RealTreeMixFraction;
                    int protoIndex = pickedReal ? PickRealTreeIndex(p.x) : (dryTree ? dryIndex : greenIndex);
                    bool isCampoTree = hasForestSplit && pickedReal && protoIndex >= pineCount;

                    // BOTD conifers are used at their native sizes (4 sizes already),
                    // so this scale just adds spread: lots of small young pines (0.4x)
                    // up to full-grown (1.6x). Lower min = more small pines; capped
                    // lower so we don't get absurd 25m+ giants (native Tall x 2.4).
                    // low-poly: un poco más altos (owner: "más altos pero no tanto");
                    // BOTD queda con su tuning nativo.
                    float s = isCampoTree ? Random.Range(1.3f, 2.1f)                     // owner: "hazlo mas grande" (arbol de campo)
                            : MapLayout.UsePsxTrees ? Random.Range(0.7f, 1.35f)          // PSX: escala más contenida (no gigantes)
                            : MapLayout.UseLowPolyTrees ? Random.Range(0.55f, 2.0f)
                            : Random.Range(0.4f, 1.6f);
                    float tint = Random.Range(0.72f, 1.08f); // breaks the "identical clones" look
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(p.x / MapLayout.MapSizeX, 0f, p.y / MapLayout.MapSize),
                        prototypeIndex = protoIndex,
                        heightScale = s,
                        widthScale = s * Random.Range(0.8f, 1.25f),
                        rotation = Random.Range(0f, Mathf.PI * 2f),
                        color = new Color(tint, tint, tint),
                        lightmapColor = Color.white
                    });
                }
            }
            return trees;
        }

        // ---------------- Real trees from AlanTree.fbx ----------------
        // Use the downloaded AlanTree asset AS-IS: its own trunk AND leaf geometry,
        // its own bark/leaf textures. The only processing is (a) baking its child
        // meshes into one root-level mesh (Unity terrain trees require the renderer
        // on the prefab root) and (b) fixing its materials to render on URP. No
        // procedural substitutes, no mesh combining tricks, no foliage filtering.

        static GameObject[] BuildALanTreePrototypes()
        {
            string fbxPath = MapLayout.ALanTreeFolder + "/AlanTree.fbx";
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            var meshFilters = instance.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters.Length == 0) { Object.DestroyImmediate(instance); return null; }

            // one submesh group per material; keep EVERY part (trunk + leaves)
            var matList = new List<Material>();
            var groups = new List<List<CombineInstance>>();
            foreach (var mf in meshFilters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var mats = mr.sharedMaterials;
                for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                {
                    var mat = sub < mats.Length ? mats[sub] : (mats.Length > 0 ? mats[mats.Length - 1] : null);
                    if (mat == null) continue;
                    mat.enableInstancing = true;
                    WireALanTreeMaterial(mat);
                    EditorUtility.SetDirty(mat);

                    int idx = matList.IndexOf(mat);
                    if (idx < 0) { idx = matList.Count; matList.Add(mat); groups.Add(new List<CombineInstance>()); }
                    groups[idx].Add(new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = sub,
                        transform = mf.transform.localToWorldMatrix
                    });
                }
            }
            if (matList.Count == 0) { Object.DestroyImmediate(instance); return null; }

            var subCombines = new CombineInstance[matList.Count];
            for (int g = 0; g < matList.Count; g++)
            {
                var sub = new Mesh();
                sub.CombineMeshes(groups[g].ToArray(), true, true);
                subCombines[g] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
            }
            var combinedMesh = new Mesh { name = "ALanTree" };
            combinedMesh.CombineMeshes(subCombines, false, true); // merge sub-meshes = false -> keep one submesh per material
            combinedMesh.RecalculateBounds();
            Object.DestroyImmediate(instance);

            string meshPath = MapLayout.GeneratedFolder + "/mesh_ALanTree.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(combinedMesh, meshPath);

            string prefabPath = MapLayout.GeneratedFolder + "/ALanTree.prefab";
            AssetDatabase.DeleteAsset(prefabPath);

            Bounds b = combinedMesh.bounds;
            float nativeHeight = Mathf.Max(0.01f, b.size.y);
            var root = new GameObject("ALanTree");
            // normalize to a sane real-world height regardless of the fbx's native scale
            root.transform.localScale = Vector3.one * (MapLayout.RealTreeTargetHeight / nativeHeight);
            root.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = matList.ToArray();
            // trunk collider: thin capsule centered on the mesh footprint so the player
            // stops at the trunk, not at the (much wider) canopy
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(b.center.x, b.center.y, b.center.z);
            col.height = Mathf.Max(1f, b.size.y);
            col.radius = 0.35f * nativeHeight / MapLayout.RealTreeTargetHeight; // ~0.35m after normalization
            col.radius = Mathf.Max(0.2f, col.radius);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            int tris = combinedMesh.triangles.Length / 3;
            var matNames = new List<string>();
            foreach (var m in matList) matNames.Add(m.name);
            Debug.Log("ALanTree: loaded, " + tris + " tris, native height=" + nativeHeight.ToString("0.0")
                + "m, normalized to " + MapLayout.RealTreeTargetHeight + "m. Materials: " + string.Join(", ", matNames));

            return new[] { prefab };
        }

        // AlanTree's materials point at textures on the original author's disk, which
        // Unity can't find, so they import blank/white. Assign the pack's own bark or
        // leaf texture (by material name), and set leaf materials up as alpha-clipped
        // cutout so the leaf cards render as leaf-shaped silhouettes instead of solid
        // rectangles (opaque) or ghostly see-through blobs (transparent).
        static void WireALanTreeMaterial(Material mat)
        {
            // FBX-imported materials can come in on the built-in Standard shader,
            // which renders solid magenta under URP. Force URP/Lit so it renders.
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && mat.shader != urpLit) mat.shader = urpLit;

            string lname = mat.name.ToLowerInvariant();
            // bark/trunk/wood/branch explicitly WIN over leaf keywords, so a trunk
            // material never gets mistaken for foliage (which would alpha-clip it
            // against the leaf texture's alpha and clip the whole trunk away -> the
            // "leaves show but trunks are invisible" bug).
            bool isBark = lname.Contains("bark") || lname.Contains("trunk") || lname.Contains("wood")
                || lname.Contains("branch") || lname.Contains("stem") || lname.Contains("broadleaf");
            bool isFoliage = !isBark && (lname.Contains("leaf") || lname.Contains("leaves")
                || lname.Contains("foliage") || lname.Contains("canopy") || lname.Contains("sample"));

            string leafPath = MapLayout.ALanTreeFolder + "/SampleLeaves_2.tga";
            string barkPath = MapLayout.ALanTreeFolder + "/BroadleafBark.tga";
            string texPath = isFoliage ? leafPath : barkPath;

            // make sure the leaf texture actually exposes its alpha for cutout
            if (isFoliage) EnsureAlphaTexture(leafPath);

            // always (re)assign the correct texture - after a shader swap the old
            // _MainTex ref may not carry over to URP's _BaseMap
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f); // kill the "frosty/plastic" moonlight sheen

            ApplyLeafOrBarkSurface(mat, isFoliage);
        }

        // Shared by every external tree source (AlanTree, klen Maple, Dream Tree 2):
        // URP Lit surface setup so leaves read as cutout leaf-shaped cards and bark
        // reads as solid opaque geometry that never gets accidentally alpha-clipped
        // or backface-culled away (see comments below - both were real bugs).
        static void ApplyLeafOrBarkSurface(Material mat, bool isFoliage)
        {
            if (isFoliage)
            {
                // URP Lit: Opaque surface + Alpha Clipping = hard cutout leaf shapes
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                mat.EnableKeyword("_ALPHATEST_ON");
                // 0.1 (too low) let near-transparent edge pixels through - those pixels
                // still carry a sliver of the leaf texture's original white background
                // color, which read as a white halo/fringe around every leaf card.
                // 0.5 clips those partial-alpha edge pixels away entirely.
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                mat.doubleSidedGI = true;
                if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // leaves visible from both sides
            }
            else
            {
                // trunk/bark: force fully opaque, no alpha clipping (so it can never
                // get clipped away), and a slight brightness boost so the dark bark
                // still reads against the near-black scene
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
                mat.SetOverrideTag("RenderType", "Opaque");
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 1);
                mat.renderQueue = -1; // default geometry queue
                var boost = new Color(1.4f, 1.3f, 1.15f); // slight lift so bark reads, but not glaring white in the torch beam
                mat.color = boost;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", boost);
                // Cull OFF: CombineMeshes can flip the trunk's winding/normals, and
                // with back-face culling that hides the visible faces entirely - which
                // is exactly "leaves show but trunks are invisible" (leaves are already
                // Cull Off). Double-sided renders it regardless of winding.
                if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                mat.doubleSidedGI = true;
            }
        }

        // ---------------- Real trees from klen "HQ Autumn Dry Maple Trees" + Dream Tree 2 ----------------
        // Both are ready-made prefabs (unlike AlanTree, no need to hunt for the right
        // sub-mesh), but their materials use non-URP shaders (klen: Built-in
        // Standard/a custom Built-in vegetation shader; Dream Tree 2: HDRP/Lit) that
        // render solid magenta as-is. Same bake pattern as AlanTree: combine to one
        // root mesh, normalize height, rewire materials to URP, add a trunk collider.

        static GameObject BakeExternalTree(GameObject src, string outName, System.Action<Material> wireMaterial, float targetHeight)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);

            // If the source ships an LODGroup (Dream Tree 2 has lod0/lod1/lod2), every
            // LOD level's renderer exists simultaneously as a child - grabbing them ALL
            // would combine 3 overlapping copies of the tree into one stacked mesh. Use
            // only the highest-detail LOD's renderers in that case.
            MeshFilter[] meshFilters;
            var lodGroup = instance.GetComponent<LODGroup>();
            var lods = lodGroup != null ? lodGroup.GetLODs() : null;
            if (lods != null && lods.Length > 0)
            {
                var lod0 = new List<MeshFilter>();
                foreach (var r in lods[0].renderers)
                {
                    if (r == null) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) lod0.Add(mf);
                }
                meshFilters = lod0.ToArray();
            }
            else
            {
                meshFilters = instance.GetComponentsInChildren<MeshFilter>(true);
            }
            if (meshFilters.Length == 0) { Object.DestroyImmediate(instance); return null; }

            var matList = new List<Material>();
            var groups = new List<List<CombineInstance>>();
            foreach (var mf in meshFilters)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;
                var mats = mr.sharedMaterials;
                for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                {
                    var mat = sub < mats.Length ? mats[sub] : (mats.Length > 0 ? mats[mats.Length - 1] : null);
                    if (mat == null) continue;
                    mat.enableInstancing = true;
                    wireMaterial(mat);
                    EditorUtility.SetDirty(mat);

                    int idx = matList.IndexOf(mat);
                    if (idx < 0) { idx = matList.Count; matList.Add(mat); groups.Add(new List<CombineInstance>()); }
                    groups[idx].Add(new CombineInstance
                    {
                        mesh = mf.sharedMesh,
                        subMeshIndex = sub,
                        transform = mf.transform.localToWorldMatrix
                    });
                }
            }
            if (matList.Count == 0) { Object.DestroyImmediate(instance); return null; }

            var subCombines = new CombineInstance[matList.Count];
            for (int g = 0; g < matList.Count; g++)
            {
                var sub = new Mesh();
                sub.CombineMeshes(groups[g].ToArray(), true, true);
                subCombines[g] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
            }
            var combinedMesh = new Mesh { name = outName };
            combinedMesh.CombineMeshes(subCombines, false, true);
            combinedMesh.RecalculateBounds();
            Object.DestroyImmediate(instance);

            string meshPath = MapLayout.GeneratedFolder + "/mesh_" + outName + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(combinedMesh, meshPath);

            string prefabPath = MapLayout.GeneratedFolder + "/" + outName + ".prefab";
            AssetDatabase.DeleteAsset(prefabPath);

            Bounds b = combinedMesh.bounds;
            float nativeHeight = Mathf.Max(0.01f, b.size.y);
            var root = new GameObject(outName);
            root.transform.localScale = Vector3.one * (targetHeight / nativeHeight);
            root.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = matList.ToArray();
            var col = root.AddComponent<CapsuleCollider>();
            col.center = b.center;
            col.height = Mathf.Max(1f, b.size.y);
            col.radius = Mathf.Max(0.2f, 0.35f * nativeHeight / targetHeight);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log(outName + ": baked, native height=" + nativeHeight.ToString("0.0") + "m, normalized to " + targetHeight + "m.");
            return prefab;
        }

        // A handful of the 10 available Maple models (spread across the poly range) -
        // using all 10 would just be near-duplicate silhouettes at different LODs for
        // a single tree slot, not 10x the visual variety.
        static readonly string[] KlenMapleNames =
        {
            "Maple_468Poly", "Maple_1952Poly", "Maple_5423poly", "Maple_8631Poly", "Maple_12338Poly"
        };

        // Conifers [BOTD] URP prefabs, used directly as terrain-tree prototypes
        // (they're purpose-built for terrain trees: CTI shader with wind + LODs +
        // billboards). Four native sizes give the big/small variety on their own,
        // and ScatterTrees adds per-instance random scaling on top.
        static readonly string[] ConiferPrefabNames =
        {
            "PF Conifer Tall BOTD URP",
            "PF Conifer Medium BOTD URP",
            "PF Conifer Small BOTD URP",
            "PF Conifer Bare BOTD URP",
        };

        // Convierte TODOS los materiales del pack Polytope de Built-in a URP/Lit
        // (por eso salían rosas). Idempotente: los que ya son URP los saltea.
        static void ConvertLowPolyMaterialsToURP()
        {
            // El pack Polytope trae SUS PROPIOS shaders (Foliage/Opaque/Plants/Rock/
            // Water) que dan el look real: textura + degradado vertical (Top/Ground
            // Color) + translucencia. Los materiales apuntaban a un shader viejo que ya
            // no existe (guid 4e605b1c) → magenta. La solución fiel es re-vincular cada
            // material a su shader del pack por tipo; los valores (_BaseTexture, _Color,
            // _TopColor, _GroundColor) siguen guardados en el material, así que se ve
            // TAL CUAL viene en el pack.
            //
            // REQUISITO: antes hay que importar PT_Nature_Free_URP_17.unitypackage
            // (Tools > Folklore Archives > Importar Shaders Polytope URP) para que estos
            // shaders sean URP. Sin eso son Built-in y saldrían magenta igual.
            var fol = Shader.Find("Polytope Studio/PT_Vegetation_Foliage_Shader");
            var opa = Shader.Find("Polytope Studio/PT_Vegetation_Opaque_Shader");
            var pla = Shader.Find("Polytope Studio/PT_Vegetation_Plants_Shader");
            var flo = Shader.Find("Polytope Studio/PT_Vegetation_Flowers_Shader");
            var roc = Shader.Find("Polytope Studio/PT_Rock_Shader");
            var wat = Shader.Find("Polytope Studio/PT_Water_Shader");
            if (fol == null || opa == null)
            {
                Debug.LogWarning("Shaders del pack Polytope no encontrados. Importá primero " +
                    "'Tools > Folklore Archives > Importar Shaders Polytope URP' (PT_Nature_Free_URP_17).");
                return;
            }

            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Polytope Studio" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                if (m.isVariant) continue; // no se puede setear shader en un Material Variant (hereda del padre)
                string ln = m.name.ToLowerInvariant();
                if (ln.Contains("skybox") || ln.Contains("plane mat")) continue;

                // TRUNK/BARK antes que "pine" (el tronco del pino tiene "pine" en el nombre).
                Shader target;
                if (ln.Contains("water")) target = wat;
                else if (ln.Contains("rock")) target = roc;
                else if (ln.Contains("trunk") || ln.Contains("bark") || ln.Contains("opaque")
                      || ln.Contains("mushroom") || ln.Contains("terrain")) target = opa;
                else if (ln.Contains("poppy") || ln.Contains("flower")) target = flo;
                else if (ln.Contains("grass")) target = pla;
                else target = fol; // hojas, pino, shrub, fern, plant, generic, fruit…

                if (target == null) target = fol;
                if (m.shader != target) { m.shader = target; EditorUtility.SetDirty(m); }
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Low-poly: " + n + " materiales re-vinculados a los shaders del pack (textura + degradado original).");
        }

        // Importa el paquete URP que trae el pack Polytope (shaders URP nativos). Sin
        // esto los shaders del pack son Built-in y todo sale magenta en URP. Unity 6 /
        // URP 17 → PT_Nature_Free_URP_17. Correr UNA vez.
        [MenuItem("Tools/Folklore Archives/Importar Shaders Polytope URP")]
        public static void ImportPolytopeURP()
        {
            const string pkg = "Assets/Polytope Studio/Lowpoly_Environments/URP/PT_Nature_Free_URP_17.unitypackage";
            if (!System.IO.File.Exists(pkg))
            {
                Debug.LogError("No se encontró el paquete: " + pkg);
                return;
            }
            Debug.Log("Importando " + pkg + " … (esperá a que compilen los shaders y luego regenerá el mapa)");
            AssetDatabase.ImportPackage(pkg, false); // false = sin diálogo
        }

        // EXPERIMENTO: pinos low-poly de Polytope Studio (usados crudos como terrain
        // trees). Sin billboard/LOD/viento — ForestBuilder desactiva el billboard cuando
        // este pack está activo (MapLayout.UseLowPolyTrees).
        // Solo el pino VERDE (con hojas). El _dead es pelado y los _cut/_logs/_stump son
        // troncos cortados → owner: "todos con hojas". La variedad la da la escala/tinte
        // aleatorio por instancia.
        // ── PSX (StarkCrafts): árboles del FBX como prototipos de terrain-tree ──
        // PSX_Tree1/PSX_Tree4 = pinos (bosque, lado ESTE/peligro). PSX_Tree2/PSX_Tree3
        // = frondosos (antes descartados — "el dueño NO los quiere" — reactivados para
        // el lado OESTE/campo argentino). El owner sacó Tree3 en una ronda ("usa solo
        // ese") y lo volvió a pedir en la siguiente ("se repite mucho el mismo") —
        // quedan los dos juntos de nuevo.
        static readonly string[] PsxPineNames       = { "PSX_Tree1", "PSX_Tree4" };
        static readonly string[] PsxBroadleafNames  = { "PSX_Tree2", "PSX_Tree3" };  // owner: "esta muchas veces el mismo, vuelve a poner el otro" — Tree3 de vuelta (se repetia mucho con uno solo)
        static readonly HashSet<string> PsxPineSet  = new HashSet<string>(PsxPineNames);
        const string PsxTexDir = "Assets/StarkCrafts/PSX_Forest_Level_byStarkCrafts/PSX_ExtractedTex/";

        // Devuelve TODOS los prototipos con los PINOS primero y los FRONDOSOS después
        // (índices [0, pineCount) = pino, [pineCount, largo) = frondoso), para que
        // ScatterTrees pueda elegir el rango correcto según el lado del mapa.
        static GameObject[] BuildPsxTreePrototypes(out int pineCount)
        {
            pineCount = 0;
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PsxForestHelper.FbxPath);
            if (fbx == null) { Debug.LogWarning("PSX: FBX no importado (" + PsxForestHelper.FbxPath + ") — caigo a low-poly/BOTD."); return null; }

            const float target = 8f; // altura objetivo en metros (árbol alto)
            // TRONCO: corteza real del pack (PSX_Bark2). Compartida por todos.
            Material trunk = PsxMat("PSX_PineTrunk", Color.white, PsxTexDir + "PSX_Bark2_128px.png");

            var pineResults = new List<GameObject>();
            var broadleafResults = new List<GameObject>();
            var report = new System.Text.StringBuilder("PSX árboles:\n");
            var allNames = new List<string>(PsxPineNames);
            allNames.AddRange(PsxBroadleafNames);
            foreach (var name in allNames)
            {
                Transform child = FindChildByName(fbx.transform, name);
                var mf = child != null ? child.GetComponent<MeshFilter>() : null;
                var mr = child != null ? child.GetComponent<MeshRenderer>() : null;
                if (mf == null || mf.sharedMesh == null || mr == null) continue;
                Mesh mesh = mf.sharedMesh;

                // COPA: la textura de aguja REAL de este pino (con alpha → recorte).
                //   PSX_Tree1 → PSX_TreeCrown1_128px ; PSX_Tree4 → PSX_TreeCrown4_Tex_128px
                Material crown = PsxMat("PSX_PineCrown_" + name, Color.white,
                                        PineCrownTexFor(name), cutout: true, wind: true);

                // El FBX es Z-up (Blender): la ALTURA es Z. Roto -90° en X (Z→Y) FIJO.
                Vector3 sz = mesh.bounds.size;
                Quaternion orient = Quaternion.Euler(-90f, 0f, 0f);
                float hExt = Mathf.Max(0.001f, sz.z);
                report.AppendLine($"  {name}: bounds=({sz.x:0.00},{sz.y:0.00},{sz.z:0.00}) alturaZ={sz.z:0.00}");

                // material por submesh: "...crown/corwn..." = copa (aguja); si no = tronco (corteza)
                var src = mr.sharedMaterials;
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = (i < src.Length && src[i] != null) ? src[i].name.ToLowerInvariant() : "";
                    mats[i] = (mn.Contains("crown") || mn.Contains("corwn")) ? crown : trunk;
                }

                // HORNEAR la rotación+escala+centrado DENTRO de la malla (los terrain-trees
                // ignoran las transforms anidadas → hay que dejar la malla ya parada).
                Mesh baked = BakeMesh(mesh, orient, target / hExt,
                                      MapLayout.GeneratedFolder + "/PSX_" + name + "_mesh.asset");

                var root = new GameObject(name);
                root.AddComponent<MeshFilter>().sharedMesh = baked;
                root.AddComponent<MeshRenderer>().sharedMaterials = mats;
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, target * 0.5f, 0f);
                col.height = target;
                col.radius = target * 0.1f;

                string path = MapLayout.GeneratedFolder + "/PSX_" + name + ".prefab";
                AssetDatabase.DeleteAsset(path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                if (prefab != null)
                {
                    if (PsxPineSet.Contains(name)) pineResults.Add(prefab);
                    else broadleafResults.Add(prefab);
                }
            }
            pineCount = pineResults.Count;
            var results = new List<GameObject>(pineResults);
            results.AddRange(broadleafResults);
            if (results.Count == 0) { Debug.LogWarning("PSX: no encontré PSX_Tree1..4 en el FBX."); return null; }
            report.AppendLine($"  pinos={pineCount}, frondosos={broadleafResults.Count}");
            Debug.Log(report.ToString());
            return results.ToArray();
        }

        // ── PINOS BILLBOARD (planos) ─────────────────────────────────────────
        // Los árboles del pack PSX vienen SIN textura (cartones lisos). El dueño
        // quiere pinos que se vean como la foto: un pino recortado sobre fondo
        // transparente = un BILLBOARD. Uso la textura MyPineTree04.png (pino
        // fotográfico con alpha, de Kajaman's Roads) sobre planos cruzados. Look
        // Fears-to-Fathom: plano, barato, pero se lee como pino de verdad.
        const string PineBillboardTex = "Assets/KajamansRoads/Textures/MyPineTree04.png";
        static GameObject[] BuildBillboardPineTrees()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PineBillboardTex);
            if (tex == null) { Debug.LogWarning("Pino billboard: no encontré " + PineBillboardTex + " — caigo a BOTD."); return null; }

            // material recorte-alpha, doble cara, color base BLANCO (que se vea la textura tal cual)
            Material pineMat = PsxMat("PSX_BillboardPine", Color.white, PineBillboardTex, cutout: true);

            const float H = 9f;              // altura del pino en metros
            const float W = H * 0.5f;        // ancho (la textura es 1:2)
            Mesh mesh = BuildCrossedBillboardMesh(W, H, 3,   // 3 planos a 0/60/120°
                                                  MapLayout.GeneratedFolder + "/PSX_BillboardPine_mesh.asset");

            var root = new GameObject("BillboardPine");
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = pineMat;
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, H * 0.45f, 0f);
            col.height = H * 0.9f;
            col.radius = 0.5f;

            string path = MapLayout.GeneratedFolder + "/PSX_BillboardPine.prefab";
            AssetDatabase.DeleteAsset(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (prefab == null) return null;
            Debug.Log("Pino billboard: prototipo creado (" + PineBillboardTex + ").");
            return new[] { prefab };
        }

        // Malla de N planos verticales cruzados (billboard 3D). Base en y=0, centrada
        // en XZ. Normales apuntando hacia AFUERA+ARRIBA para que reciban luz pareja
        // (evita cards oscuros). UV completa 0..1 en cada plano.
        static Mesh BuildCrossedBillboardMesh(float width, float height, int planes, string path)
        {
            float hw = width * 0.5f;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();
            for (int p = 0; p < planes; p++)
            {
                float ang = Mathf.PI * p / planes;         // 0..180° repartido
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)); // eje del ancho
                Vector3 nrm = (Vector3.up * 0.6f + Vector3.Cross(Vector3.up, dir) * 0.4f).normalized;
                int b = verts.Count;
                verts.Add(-dir * hw + Vector3.up * 0f);      // left-bottom
                verts.Add( dir * hw + Vector3.up * 0f);      // right-bottom
                verts.Add( dir * hw + Vector3.up * height);  // right-top
                verts.Add(-dir * hw + Vector3.up * height);  // left-top
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                for (int k = 0; k < 4; k++) norms.Add(nrm);
                tris.Add(b + 0); tris.Add(b + 3); tris.Add(b + 2); // front
                tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 1);
            }
            var m = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // Textura de copa que corresponde a cada árbol del pack, según el material
        // original del mesh. Extraídas del FBX a PSX_ExtractedTex. Tree2/Tree3 son los
        // frondosos (antes descartados con "el dueño NO los quiere" — reactivados para
        // el lado CAMPO del mapa, owner: "pongamos mitad y mitad, campo argentino").
        static string PineCrownTexFor(string treeName)
        {
            switch (treeName)
            {
                case "PSX_Tree1": return PsxTexDir + "PSX_TreeCrown1_128px.png";       // pino chico
                case "PSX_Tree4": return PsxTexDir + "PSX_TreeCrown4_Tex_128px.png";   // pino alto/esbelto
                case "PSX_Tree2": return PsxTexDir + "PSX_TreeCrown2_128px.png";       // frondoso redondo (campo)
                case "PSX_Tree3": return PsxTexDir + "PSX_TreeCrown3_Tex_128px.png";   // frondoso (campo)
                default:          return PsxTexDir + "PSX_TreeCrown1_128px.png";
            }
        }

        static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // Hornea una malla: rota + escala los vértices y baja la base a y=0 (centrada en
        // XZ). Devuelve una malla NUEVA guardada como asset (los terrain-trees usan la
        // malla cruda, ignoran las transforms del prefab → hay que dejarla ya parada).
        static Mesh BakeMesh(Mesh src, Quaternion rot, float scale, string path)
        {
            var verts = src.vertices;
            var norms = src.normals;
            for (int i = 0; i < verts.Length; i++) verts[i] = (rot * verts[i]) * scale;
            for (int i = 0; i < norms.Length; i++) norms[i] = rot * norms[i];
            // bounds tras rotar/escalar → offset para base en y=0 y centrado XZ
            if (verts.Length > 0)
            {
                var b = new Bounds(verts[0], Vector3.zero);
                for (int i = 1; i < verts.Length; i++) b.Encapsulate(verts[i]);
                var off = new Vector3(b.center.x, b.min.y, b.center.z);
                for (int i = 0; i < verts.Length; i++) verts[i] -= off;
            }
            var m = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            m.indexFormat = src.indexFormat;
            m.vertices = verts;
            if (norms.Length == verts.Length) m.normals = norms;
            var uv = src.uv; if (uv != null && uv.Length == verts.Length) m.uv = uv;
            m.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) m.SetTriangles(src.GetTriangles(s), s);
            if (norms.Length != verts.Length) m.RecalculateNormals();
            m.RecalculateBounds();
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // material URP/Lit para los árboles PSX (que vienen blancos). Opcional: textura
        // (_BaseMap) y recorte alpha (para el follaje = hojas con transparencia).
        static Material PsxMat(string name, Color col, string texPath = null, bool cutout = false, bool wind = false)
        {
            string path = "Assets/Settings/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            // copas con viento → shader Folklore/TreeWind (balancea la copa). Resto → URP/Lit.
            var shader = wind ? Shader.Find("Folklore/TreeWind") : Shader.Find("Universal Render Pipeline/Lit");
            if (m == null)
            {
                m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (shader != null && m.shader != shader) m.shader = shader;   // re-aplicar si cambió
            if (m.HasProperty("_BaseColor"))  m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", 0f);
            if (wind)
            {
                if (m.HasProperty("_WindStrength")) m.SetFloat("_WindStrength", 0.68f);
                if (m.HasProperty("_WindSpeed"))    m.SetFloat("_WindSpeed", 1.0f);
            }
            if (texPath != null)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                if (t != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            }
            if (cutout)
            {
                // hojas: alpha cutout + doble cara (para que el card se vea de ambos lados)
                m.SetFloat("_AlphaClip", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.4f);
                if (m.HasProperty("_Cull"))   m.SetFloat("_Cull", 0f);
                m.renderQueue = 2450; // AlphaTest
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static readonly string[] LowPolyTreeNames = { "PT_Pine_Tree_03_green" };
        static GameObject[] BuildLowPolyTreePrototypes()
        {
            const string dir = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/";
            var results = new List<GameObject>();
            foreach (var n in LowPolyTreeNames)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(dir + n + ".prefab");
                if (src != null) results.Add(src);
            }
            if (results.Count == 0) Debug.LogWarning("Low-poly pines de Polytope no encontrados - vuelvo a BOTD.");
            else Debug.Log("Low-poly: cargados " + results.Count + " pinos de Polytope.");
            return results.Count > 0 ? results.ToArray() : null;
        }

        static GameObject[] BuildConiferPrototypes()
        {
            const string dir = "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/";
            var results = new List<GameObject>();
            foreach (var n in ConiferPrefabNames)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(dir + n + ".prefab");
                if (src != null) results.Add(src);
            }
            if (results.Count == 0) Debug.LogWarning("Conifers [BOTD] URP prefabs not found at " + dir + " - did the URP sub-package import?");
            else Debug.Log("Conifers: loaded " + results.Count + " BOTD conifer prototype(s).");
            return results.Count > 0 ? results.ToArray() : null;
        }

        static GameObject[] BuildKlenMapleTreePrototypes()
        {
            var results = new List<GameObject>();
            foreach (var name in KlenMapleNames)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/klen/Prefab/" + name + ".prefab");
                if (src == null) continue;
                var prefab = BakeExternalTree(src, "KlenMaple_" + name, WireKlenMapleMaterial, MapLayout.RealTreeTargetHeight);
                if (prefab != null) results.Add(prefab);
            }
            return results.Count > 0 ? results.ToArray() : null;
        }

        // klen's package ships bark2.mat (Built-in Standard shader) for the trunk and
        // Klen_AT.mat (a custom Built-in vegetation shader) for the leaves - neither
        // renders under URP, so materials are rewired the same way as AlanTree's.
        static void WireKlenMapleMaterial(Material mat)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && mat.shader != urpLit) mat.shader = urpLit;

            string lname = mat.name.ToLowerInvariant();
            bool isBark = lname.Contains("bark");
            bool isFoliage = !isBark; // Klen_AT / Klen_AT2 / Klen_AT3 are all leaf materials

            string texPath = isFoliage ? "Assets/klen/Model/Materials/Klen_AT.tga" : "Assets/klen/Model/Materials/bark2.png";
            string normalPath = isFoliage ? "Assets/klen/Model/Materials/Klen_AT_NRM.png" : "Assets/klen/Model/Materials/bark2_NRM.png";
            if (isFoliage) EnsureAlphaTexture(texPath);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
            var normal = BuilderUtils.LoadAsNormalMap(normalPath);
            if (normal != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            ApplyLeafOrBarkSurface(mat, isFoliage);
        }

        static GameObject[] BuildDreamTreePrototypes()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DreamTree2/Prefab/DreamTree.prefab");
            if (src == null) return null;
            var prefab = BakeExternalTree(src, "DreamTree2", WireDreamTreeMaterial, MapLayout.RealTreeTargetHeight);
            return prefab != null ? new[] { prefab } : null;
        }

        // ---------------- Bushes from Yughues Free Bushes ----------------
        // Undergrowth, not canopy - scattered as their own pass in Build()/
        // ScatterBushes(), independent of the tree density/mix above.

        static readonly string[] YughuesBushNames = { "P_Bush01", "P_Bush02", "P_Bush03", "P_Bush04", "P_Bush05" };

        static GameObject[] BuildYughuesBushPrototypes()
        {
            var results = new List<GameObject>();
            foreach (var name in YughuesBushNames)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YughuesFreeBushes2018/Prefabs/" + name + ".prefab");
                if (src == null) continue;
                // each bush prefab has its own LODGroup (BakeExternalTree already
                // handles that) but ships with no collider despite the pack's listing
                var prefab = BakeExternalTree(src, "YughuesBush_" + name, WireYughuesBushMaterial, MapLayout.BushTargetHeight);
                if (prefab != null) results.Add(prefab);
            }
            return results.Count > 0 ? results.ToArray() : null;
        }

        // Bush materials (M_BushNN) are on the Built-in Standard shader - same URP
        // rewiring as the other packs. Bushes are all-foliage (no separate bark
        // material to tell apart), so every material here gets the cutout treatment.
        static void WireYughuesBushMaterial(Material mat)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && mat.shader != urpLit) mat.shader = urpLit;

            // material names are M_Bush01..05 - map back to the matching T_BushNN_* set
            string suffix = "01";
            foreach (var n in new[] { "01", "02", "03", "04", "05" })
                if (mat.name.Contains(n)) { suffix = n; break; }

            string basePath = "Assets/YughuesFreeBushes2018/Textures/T_Bush" + suffix;
            string texPath = basePath + "_d.tga";
            string normalPath = basePath + "_n.tga";
            EnsureAlphaTexture(texPath);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
            var normal = BuilderUtils.LoadAsNormalMap(normalPath);
            if (normal != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            ApplyLeafOrBarkSurface(mat, true); // all-foliage
        }

        // Independent scatter pass for the Yughues bushes - own grid/density (see
        // MapLayout.BushGridStep/BushDensity), not mixed into the tree pool above.
        static void ScatterBushes(int bushProtoStart, int bushProtoCount, List<TreeInstance> trees)
        {
            if (bushProtoCount <= 0) return;
            float step = MapLayout.BushGridStep;
            float jitter = step * 0.4f;
            for (float x = 10f; x < MapLayout.MapSizeX - 10f; x += step)
            {
                for (float z = 10f; z < MapLayout.MapSize - 10f; z += step)
                {
                    var p = new Vector2(x + Random.Range(-jitter, jitter), z + Random.Range(-jitter, jitter));

                    // LAKESIDE SHORE (between guardrail and water): some bushes on the
                    // grassy embankment, handled before the road exclusion below.
                    float southD = MapLayout.PavedRouteZAt(p.x) - p.y;
                    if (southD > MapLayout.ShoreVegFar) continue; // out in the water
                    if (southD > MapLayout.ShoreVegNear)
                    {
                        if (Random.value < MapLayout.ShoreBushDensity)
                        {
                            float bs = Random.Range(0.8f, 1.5f);
                            float bt = Random.Range(0.75f, 1.05f);
                            trees.Add(new TreeInstance
                            {
                                position = new Vector3(p.x / MapLayout.MapSizeX, 0f, p.y / MapLayout.MapSize),
                                prototypeIndex = bushProtoStart + Random.Range(0, bushProtoCount),
                                heightScale = bs,
                                widthScale = bs * Random.Range(0.85f, 1.2f),
                                rotation = Random.Range(0f, Mathf.PI * 2f),
                                color = new Color(bt, bt, bt),
                                lightmapColor = Color.white
                            });
                        }
                        continue;
                    }

                    if (BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute) < 13f) continue;
                    float dRoad = BuilderUtils.DistToPolyline(p, MapLayout.DirtRoad);
                    float dA = BuilderUtils.DistToPolyline(p, MapLayout.PathA);
                    float dScary = BuilderUtils.DistToScaryPaths(p);
                    if (dRoad < 3f || dA < 3f || dScary < 2.5f) continue;
                    if (BuilderUtils.DistToRivers(p) < 22f) continue;
                    if (BuilderUtils.DistToPolyline(p, MapLayout.BeachPath) < 4f) continue;
                    if (Vector2.Distance(p, MapLayout.RiverBeach) < 13f) continue;
                    if (Vector2.Distance(p, MapLayout.Campsite) < 12f) continue;
                    // despejar TODO el lote de la casa de la vieja (dentro del cerco),
                    // no solo el centro, así el patio/perímetro queda libre
                    if (p.x > MapLayout.OldLadyLotMin.x - 1f && p.x < MapLayout.OldLadyLotMax.x + 1f &&
                        p.y > MapLayout.OldLadyLotMin.y - 1f && p.y < MapLayout.OldLadyLotMax.y + 1f) continue;
                    // sin árboles sobre la casa ni el galpón (huella + margen)
                    if (MapLayout.InRect(p, MapLayout.OldLadyHouseFootMin, MapLayout.OldLadyHouseFootMax, 3f)) continue;
                    if (MapLayout.InRect(p, MapLayout.OldLadyBarnFootMin, MapLayout.OldLadyBarnFootMax, 3f)) continue;
                    if (Vector2.Distance(p, MapLayout.MainCriminalCamp) < 12f) continue;
                    if (Vector2.Distance(p, MapLayout.HostageArea) < 6f) continue;

                    if (Random.value > MapLayout.BushDensity) continue;

                    float s = Random.Range(0.9f, 1.8f);
                    float tint = Random.Range(0.75f, 1.05f);
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(p.x / MapLayout.MapSizeX, 0f, p.y / MapLayout.MapSize),
                        prototypeIndex = bushProtoStart + Random.Range(0, bushProtoCount),
                        heightScale = s,
                        widthScale = s * Random.Range(0.85f, 1.2f),
                        rotation = Random.Range(0f, Mathf.PI * 2f),
                        color = new Color(tint, tint, tint),
                        lightmapColor = Color.white
                    });
                }
            }
        }

        // Dream Tree 2 ships "bark drtr.mat" / "leaf drtr.mat" on HDRP/Lit - not
        // available in this URP project, rewired the same way as the other two.
        static void WireDreamTreeMaterial(Material mat)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null && mat.shader != urpLit) mat.shader = urpLit;

            string lname = mat.name.ToLowerInvariant();
            bool isFoliage = lname.Contains("leaf") || lname.Contains("grass");

            string texPath = isFoliage ? "Assets/DreamTree2/Textures/leaf drtr2.png" : "Assets/DreamTree2/Textures/barkdrtr.png";
            string normalPath = isFoliage ? "Assets/DreamTree2/Textures/leaf_Normal.png" : "Assets/DreamTree2/Textures/barkdrtr_Normal.png";
            if (isFoliage) EnsureAlphaTexture(texPath);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                mat.mainTexture = tex;
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
            var normal = BuilderUtils.LoadAsNormalMap(normalPath);
            if (normal != null && mat.HasProperty("_BumpMap")) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            ApplyLeafOrBarkSurface(mat, isFoliage);
        }

        // Force a texture to import with its alpha channel usable for alpha-cutout.
        static void EnsureAlphaTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            bool changed = false;
            if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        // Terrain trees require the mesh renderer on the prefab ROOT,
        // so we bake all parts into a single mesh (one submesh per material).

        struct TreePart
        {
            public string builtinMesh;
            public Vector3 pos, scale, euler;
            public int mat;
            public TreePart(string mesh, Vector3 p, Vector3 s, Vector3 e, int m)
            { builtinMesh = mesh; pos = p; scale = s; euler = e; mat = m; }
        }

        static GameObject GreenTreePrefab()
        {
            var trunkMat = BuilderUtils.MatTextured("trunk_bark", BarkTexture(), Color.white, 0.08f);
            var canopy1 = BuilderUtils.MatTextured("canopy1_leaf",
                FoliageTexture("leaf_dark", new Color(0.09f, 0.20f, 0.07f), new Color(0.18f, 0.32f, 0.12f)), Color.white, 0.05f);
            var canopy2 = BuilderUtils.MatTextured("canopy2_leaf",
                FoliageTexture("leaf_light", new Color(0.16f, 0.30f, 0.11f), new Color(0.27f, 0.42f, 0.18f)), Color.white, 0.05f);
            return TreePrefab("GreenTree", new[]
            {
                new TreePart("Cylinder.fbx", new Vector3(0f, 2.2f, 0f), new Vector3(0.3f, 2.2f, 0.3f), Vector3.zero, 0),
                new TreePart("Sphere.fbx", new Vector3(0f, 4.6f, 0f), new Vector3(2.6f, 2.2f, 2.6f), Vector3.zero, 1),
                new TreePart("Sphere.fbx", new Vector3(0.8f, 3.9f, 0.3f), new Vector3(1.8f, 1.5f, 1.8f), Vector3.zero, 2)
            }, new[] { trunkMat, canopy1, canopy2 }, 0.35f, 4.4f);
        }

        static GameObject DryTreePrefab()
        {
            var trunkMat = BuilderUtils.MatTextured("trunk_bark", BarkTexture(), Color.white, 0.08f);
            var dryMat = BuilderUtils.MatTextured("drybranch_bark", BarkTexture(), new Color(0.85f, 0.78f, 0.62f), 0.1f);
            // owner wants every tree to unmistakably read as leafy, even the "dry"
            // variant - full canopy now, just tinted sere/yellow-brown instead of
            // green, rather than sparse/mostly-bare like before.
            var dryLeaf = BuilderUtils.MatTextured("dryleaf",
                FoliageTexture("leaf_sere", new Color(0.28f, 0.22f, 0.10f), new Color(0.45f, 0.36f, 0.16f)), Color.white, 0.05f);
            return TreePrefab("DryTree", new[]
            {
                new TreePart("Cylinder.fbx", new Vector3(0f, 1.9f, 0f), new Vector3(0.18f, 1.9f, 0.18f), new Vector3(0f, 0f, 6f), 0),
                new TreePart("Cylinder.fbx", new Vector3(0.35f, 3.3f, 0f), new Vector3(0.06f, 1.1f, 0.06f), new Vector3(40f, 0f, 0f), 1),
                new TreePart("Cylinder.fbx", new Vector3(-0.3f, 3.2f, 0.2f), new Vector3(0.06f, 1.0f, 0.06f), new Vector3(35f, 120f, 0f), 1),
                new TreePart("Cylinder.fbx", new Vector3(0f, 3.5f, -0.3f), new Vector3(0.05f, 0.9f, 0.05f), new Vector3(55f, 240f, 0f), 1),
                new TreePart("Sphere.fbx", new Vector3(0f, 4.4f, 0f), new Vector3(2.2f, 1.8f, 2.2f), Vector3.zero, 2),
                new TreePart("Sphere.fbx", new Vector3(0.7f, 3.8f, 0.25f), new Vector3(1.5f, 1.2f, 1.5f), Vector3.zero, 2),
                new TreePart("Sphere.fbx", new Vector3(-0.6f, 3.7f, 0.3f), new Vector3(1.3f, 1.0f, 1.3f), Vector3.zero, 2)
            }, new[] { trunkMat, dryMat, dryLeaf }, 0.2f, 4.2f);
        }

        // ---------------- Procedural textures (no external art needed) ----------------

        static Texture2D BarkTexture()
        {
            string path = MapLayout.GeneratedFolder + "/tex_bark.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ridge = Mathf.PerlinNoise(x * 0.18f, y * 0.035f);
                    float fine = Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.25f;
                    float v = Mathf.Clamp01(ridge * 0.7f + fine + 0.1f);
                    var c = Color.Lerp(new Color(0.16f, 0.11f, 0.07f), new Color(0.40f, 0.30f, 0.19f), v);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static Texture2D FoliageTexture(string name, Color dark, Color light)
        {
            string path = MapLayout.GeneratedFolder + "/tex_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float clump = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
                    float speck = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                    float v = Mathf.Clamp01(clump * 0.75f + speck * 0.35f);
                    var c = Color.Lerp(dark, light, v);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static GameObject TreePrefab(string name, TreePart[] parts, Material[] mats, float colRadius, float colHeight)
        {
            string prefabPath = MapLayout.GeneratedFolder + "/" + name + ".prefab";
            string meshPath = MapLayout.GeneratedFolder + "/mesh_" + name + ".asset";
            AssetDatabase.DeleteAsset(prefabPath);
            AssetDatabase.DeleteAsset(meshPath);

            // combine: one submesh per material
            var subCombines = new CombineInstance[mats.Length];
            for (int m = 0; m < mats.Length; m++)
            {
                var list = new List<CombineInstance>();
                foreach (var part in parts)
                {
                    if (part.mat != m) continue;
                    list.Add(new CombineInstance
                    {
                        mesh = Resources.GetBuiltinResource<Mesh>(part.builtinMesh),
                        transform = Matrix4x4.TRS(part.pos, Quaternion.Euler(part.euler), part.scale)
                    });
                }
                var sub = new Mesh();
                sub.CombineMeshes(list.ToArray(), true, true);
                subCombines[m] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
            }
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(subCombines, false, true);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, meshPath);

            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = mats;
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, colHeight * 0.5f, 0f);
            col.radius = colRadius;
            col.height = colHeight;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------- GRASS (terrain details) ----------------

        static int _mudGrassCleared = 0;
        static void SetupGrass(TerrainData td)
        {
            _mudGrassCleared = 0;
            // 1024 (up from 512) = ~1m per detail cell instead of ~2m, so the ~1.6m
            // bare wheel ruts on the dirt road actually resolve instead of being lost
            // between cells. Cells are now 4x smaller, so per-cell counts below are
            // lowered to keep total density sane while still "bien poblado".
            td.SetDetailResolution(1024, 32);
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode); // our density values are instance counts (0-16)

            // Real vegetation from the Terrain Sample Asset Pack (instanced meshes)
            // Tall + dense on purpose: "monte entrerriano" look (wild, overgrown scrubland).
            bool lp = MapLayout.UseLowPolyTrees;
            // LOW-POLY "como viene en el pack": la malla de pasto a su ESCALA NATIVA
            // (multiplicadores ~1, sin estirar). Antes estiraba LP_HighGrass a 2.8 de
            // alto con poco ancho → se leía como cartas/bloques cuadrados. Ahora uso el
            // pasto estándar del pack (PT_Grass_02) en proporción nativa, con una leve
            // variación aleatoria, tal como en la demo del pack.
            // ALTO y frondoso (owner: "altísimos"): la malla es ~0.66m, así que subo
            // el multiplicador de altura a 2.5–4.0 → pasto de ~1.6–2.6m. Ancho moderado
            // para que no engorde. Con la textura alpha real del pack se ve como briznas
            // altas, no bloques.
            // PSX (billboards de textura, lo más liviano) tiene prioridad sobre low-poly.
            // healthyColor/dryColor tintan la textura: verde apagado y pajizo/seco.
            bool psx = MapLayout.UsePsxGrass;
            var psxHealthy = new Color(0.68f, 0.74f, 0.48f);
            var psxDry     = new Color(0.62f, 0.56f, 0.32f);

            var grassGreen = psx ? PsxGrassDetail("PSX_GrassBlade_128px", 1.2f, 2.0f, 2.2f, 3.2f, psxHealthy, psxDry)
                           : lp  ? LowPolyDetail(LP_HighGrass, 1.0f, 1.5f, 3.0f, 4.5f)    : PackDetail("Grass_B", 0.9f, 1.7f, 4.8f, 7.8f);
            var grassDry   = psx ? PsxGrassDetail("PSX_GrassBlade_128px", 1.2f, 2.0f, 2.1f, 3.1f, psxDry, psxDry)
                           : lp  ? LowPolyDetail(LP_HighGrass, 1.0f, 1.5f, 3.2f, 4.7f)    : PackDetail("GrassDry_B", 1.0f, 1.9f, 5.1f, 8.4f);
            // Pasto de caminos/senderos: BAJO (owner) — misma textura/malla pero corta.
            var grassShort = psx ? PsxGrassDetail("PSX_GrassBlade_128px", 0.9f, 1.4f, 1.0f, 1.6f, psxHealthy, psxDry)
                           : lp  ? LowPolyDetail(LP_HighGrass, 0.8f, 1.1f, 1.0f, 1.6f)    : PackDetail("Grass_B", 0.8f, 1.3f, 1.4f, 2.3f);
            var fern       = lp ? LowPolyDetail(LP_ShrubGreen, 0.9f, 1.15f, 0.9f, 1.15f): PackDetail("Fern_A", 0.9f, 1.6f, 1.1f, 1.8f);
            var dryBush    = lp ? LowPolyDetail(LP_ShrubDead, 0.9f, 1.15f, 0.9f, 1.15f) : PackDetail("BushDry_A", 0.9f, 1.6f, 0.9f, 1.6f);

            if (grassGreen == null || grassDry == null)
            {
                Debug.LogWarning("Terrain Sample Asset Pack not found - using procedural grass.");
                SetupProceduralGrass(td);
                return;
            }

            var protos = new List<DetailPrototype> { grassGreen, grassDry };
            int shortIdx = -1, fernIdx = -1, bushIdx = -1;
            if (grassShort != null) { shortIdx = protos.Count; protos.Add(grassShort); }
            if (fern != null) { fernIdx = protos.Count; protos.Add(fern); }
            if (dryBush != null) { bushIdx = protos.Count; protos.Add(dryBush); }
            td.detailPrototypes = protos.ToArray();

            int res = td.detailResolution;
            var maps = new List<int[,]>();
            for (int i = 0; i < protos.Count; i++) maps.Add(new int[res, res]);

            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    float wx = xi / (float)(res - 1) * MapLayout.MapSizeX;
                    float wz = zi / (float)(res - 1) * MapLayout.MapSize;
                    var p = new Vector2(wx, wz);

                    // sin pasto SOLO bajo la huella de la casa (que no atraviese el piso);
                    // sin pasto bajo la HUELLA de la casa y el galpón (que no atraviese el
                    // piso); afuera el pasto llega hasta las paredes (pedido del owner)
                    if (MapLayout.InRect(p, MapLayout.OldLadyHouseFootMin, MapLayout.OldLadyHouseFootMax, 0.2f)) continue;
                    if (MapLayout.InRect(p, MapLayout.OldLadyBarnFootMin, MapLayout.OldLadyBarnFootMax, 0.2f)) continue;

                    // CLARO PELADO (campamento/rancho/galpón/cabaña): barro sin nada de pasto.
                    if (TerrainBuilder.IsClearing(p)) { _mudGrassCleared++; continue; }

                    // SENDEROS: NO se toca el pasto (el owner quiere el pasto que se mueve
                    // igual que en el bosque). La TEXTURA del piso ya es barro por el splat
                    // (PaintTextures pinta la capa 4 en los senderos) y asoma entre las
                    // briznas. El pasto sigue el flujo normal de abajo.

                    // claro del campamento de los ladrones (suelo pisado bajo ranchos/fuego)
                    if (Vector2.Distance(p, MapLayout.MainCriminalCamp) < 26f) continue;

                    // ===== ÁREAS NUEVAS (MapPlan): vegetación propia de cada zona =====
                    // ESTEPA: nada de pasto verde — coirón SECO y RALO (mata baja pajiza).
                    if (Vector2.Distance(p, MapLayout.EstepaCenter) < 40f)
                    {
                        if (Random.value < 0.35f) maps[1][zi, xi] = 1 + Random.Range(0, 2); // pasto dry ralo (índice 1 = grassDry)
                        continue;
                    }
                    // MALLÍN / ROQUEDAL / QUEMADO / ESTANCIA / CORRALES: suelo pelado (barro/piedra/ceniza asoma)
                    if (Vector2.Distance(p, MapLayout.Mallin) < 22f) continue;
                    if (Vector2.Distance(p, MapLayout.Roquedal) < 20f) continue;
                    if (Vector2.Distance(p, MapLayout.BurntForest) < 24f) continue;
                    if (Vector2.Distance(p, MapLayout.Estancia) < 16f) continue;
                    if (Vector2.Distance(p, MapLayout.Corrales) < 14f) continue;
                    if (MapLayout.InYpfPad(p)) continue;   // lote de la estación YPF: sin pasto

                    float southDg = MapLayout.PavedRouteZAt(p.x) - p.y;
                    if (southDg > MapLayout.ShoreVegFar) continue; // out in the water
                    // keep grass off the tarmac, but on the lakeside let it grow right
                    // up to the guardrail instead of stopping 10m short of it
                    if (southDg <= MapLayout.ShoreVegNear && BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute) < 10f) continue;
                    float dRiv = BuilderUtils.DistToPolyline(p, MapLayout.River);
                    if (dRiv < 18f) continue;
                    // lago + orilla: sin pasto alto en el agua ni en toda la playa plana
                    if (Vector2.Distance(p, MapLayout.CentralLakeCenter) < MapLayout.CentralLakeRadius + MapLayout.CentralLakeBeachWidth + 10f) continue;
                    // montañas del lago: despeje por proximidad DESACTIVADO junto con el
                    // asset (ver mismo comentario en el bloque de árboles de arriba)
                    // franja de arena de la ribera (TerrainBuilder pinta arena por
                    // altura hasta ~10m): sin pasto encima de la arena
                    if (dRiv < 34f &&
                        td.GetInterpolatedHeight(wx / MapLayout.MapSizeX, wz / MapLayout.MapSize) < 9.6f) continue;

                    // claro del campamento PRIMERO: el BeachPath arranca EN el campamento,
                    // así que si el "pasto corto" del sendero (abajo) va antes, mete briznas
                    // dentro de la fogata (y esas no pasan por este radio). Por eso va acá.
                    if (Vector2.Distance(p, MapLayout.Campsite) < MapLayout.CampsiteClearRadius) continue;

                    // sendero pisado del campamento a la playa: pasto corto y ralo
                    if (BuilderUtils.DistToPolyline(p, MapLayout.BeachPath) < 2.5f)
                    {
                        if (shortIdx >= 0) maps[shortIdx][zi, xi] = 1 + Random.Range(0, 2);
                        continue;
                    }
                    // mini playa arenosa: sin pasto
                    if (Vector2.Distance(p, MapLayout.RiverBeach) < 12f) continue;

                    float dRoad = BuilderUtils.DistToPolyline(p, MapLayout.DirtRoad);
                    float dA = BuilderUtils.DistToPolyline(p, MapLayout.PathA);
                    float dScary = BuilderUtils.DistToScaryPaths(p);

                    // CAMINO DE TIERRA (ruta→campamento): DOBLE huella de ruedas de auto,
                    // con pasto medio en el medio y los bordes, y las dos franjas peladas.
                    if (dRoad < 2.8f)
                    {
                        if (shortIdx >= 0)
                        {
                            float rutNoise = Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f) * 0.15f;
                            bool onRut = Mathf.Abs(dRoad - 1.1f) < (0.55f + rutNoise);
                            if (!onRut) maps[shortIdx][zi, xi] = 9 + Random.Range(0, 4);
                        }
                        continue;
                    }
                    // SENDEROS A PIE (Path A/B, túneles, caminos nuevos): UNA SOLA huella
                    // angosta pelada (~1m), con vegetación densa pegada al borde.
                    float dExtraTr = BuilderUtils.DistToExtraTrails(p);
                    float dFoot = Mathf.Min(dA, Mathf.Min(dScary, dExtraTr));
                    if (dFoot < 1.1f) continue; // centro pelado angosto (los costados = pasto denso)

                    float df = Vector2.Distance(p, MapLayout.HuntingField);
                    float dm = Vector2.Distance(p, MapLayout.MainCriminalCamp);
                    bool dryZone = df < 55f || dm < 110f;

                    // Confirmed via profiling that grass density (not the trees) was driving
                    // 200M+ tris at the original 7..16/cell everywhere. Now that the scene
                    // is near-total darkness and render distances hug the flashlight's reach
                    // (see MapLayout.FlashlightRange), the actually-rendered area at any
                    // moment is ~4-5x smaller than before, so density can go back up a lot
                    // without the total drawn instance count blowing back up.
                    // detail resolution is now 1024 (cells 4x smaller), so per-cell
                    // counts are ~halved vs the old 6..13 to keep it dense ("bien
                    // poblado") without quadrupling the total instance count.
                    float n = Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f);
                    int v = 4 + Mathf.RoundToInt(n * 4f) + Random.Range(0, 3); // 4..11 (owner: pasto poblado)

                    // DEGRADADO POR DISTANCIA (owner: menos pasto lejos de caminos/POIs
                    // para FPS; el pasto cercano lo tapa igual). Distancia a la zona
                    // jugable más próxima (caminos + POIs), reusando lo ya calculado.
                    float dPaved = BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute);
                    float dGameplay = Mathf.Min(Mathf.Min(dRoad, dA), Mathf.Min(dScary, dPaved));
                    // Campamento: medir al BORDE del claro de barro (no al centro), así el
                    // pasto está PLENO pegado al barro y no ralo alrededor del campamento.
                    dGameplay = Mathf.Min(dGameplay, Mathf.Max(0f, Vector2.Distance(p, MapLayout.Campsite) - 12f));
                    dGameplay = Mathf.Min(dGameplay, df); // HuntingField
                    dGameplay = Mathf.Min(dGameplay, dm); // MainCriminalCamp
                    dGameplay = Mathf.Min(dGameplay, Vector2.Distance(p, MapLayout.OldLadyRanch));
                    dGameplay = Mathf.Min(dGameplay, Vector2.Distance(p, MapLayout.Grave));
                    // zonas y caminos nuevos del owner
                    dGameplay = Mathf.Min(dGameplay, Vector2.Distance(p, MapLayout.LakeMountain));
                    dGameplay = Mathf.Min(dGameplay, BuilderUtils.DistToExtraTrails(p));
                    float tFar = Mathf.InverseLerp(MapLayout.GrassFullRadius, MapLayout.GrassFarRadius, dGameplay);
                    float densityFactor = Mathf.Lerp(1f, MapLayout.GrassFarDensity, tFar);
                    // RALEO del pasto de campo abierto. Owner ahora quiere pasto POBLADO
                    // (alto y denso), así que casi sin raleo (1.0 = densidad plena; bajá
                    // si querés ver más tierra entre medio).
                    const float grassThin = 1.0f;
                    v = Mathf.RoundToInt(v * densityFactor * grassThin);
                    if (v <= 0) continue; // bosque profundo lejos de todo: sin pasto

                    if (dryZone) maps[1][zi, xi] = v;
                    else maps[0][zi, xi] = v;

                    // dry bushes close the scary tunnels at eye level
                    if (bushIdx >= 0 && dScary < 11f) maps[bushIdx][zi, xi] = Random.Range(1, 3);
                    // ferns deep inside the green forest
                    if (fernIdx >= 0 && !dryZone && dScary > 14f && dA > 10f && dRoad > 10f && n > 0.45f)
                        maps[fernIdx][zi, xi] = Random.Range(1, 3);
                }
            }
            for (int i = 0; i < protos.Count; i++) td.SetDetailLayer(0, 0, i, maps[i]);
            Debug.Log($"<color=cyan>[GRASS] pasto despejado sobre el barro (IsMudSpot): {_mudGrassCleared} celdas detail. Campsite={MapLayout.Campsite}</color>");
        }

        // Prefabs de pasto/arbusto low-poly (Polytope)
        const string LP_HighGrass  = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/PT_High_Grass_02_v1.prefab";
        // el de Plants es un prefab con LODGroup (3 hijos, sin mesh en la raíz) → no
        // sirve como terrain detail (Unity lo renderiza raro/cuadrado). El de Helpers
        // es la MISMA malla pero como GameObject único (LOD0) — el pasto de la demo
        // "tal como viene". Ese es el que va como detail.
        const string LP_Grass      = "Assets/Polytope Studio/Lowpoly_Demos/Environment_Free/Helpers/PT_Grass_02_v1.prefab";
        const string LP_ShrubGreen = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Shrubs/PT_Generic_Shrub_01_green.prefab";
        const string LP_ShrubDead  = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Shrubs/PT_Generic_Shrub_01_dead.prefab";

        // Detail prototype a partir de un prefab low-poly (sin darken/fade shader: usa
        // su material URP ya convertido). Los tamaños son estimados — se tunean a ojo.
        // PASTO PSX: detalle basado en TEXTURA (grass billboard), no en malla. Es el
        // modo más barato del sistema de detalles del terrain — Unity los dibuja como
        // quads que miran a la cámara, sin instanciar mallas. La textura sale del FBX
        // de StarkCrafts (PSX_ExtractedTex), así que pega con los pinos PSX.
        // OJO: PSX_Grass_128px es un ATLAS (juncos + lavanda + pasto). Para el billboard
        // uso PSX_GrassBlade_128px, que es solo la franja de pasto recortada de ese atlas.
        static DetailPrototype PsxGrassDetail(string texName, float minW, float maxW,
                                              float minH, float maxH, Color healthy, Color dry)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PsxTexDir + texName + ".png");
            if (tex == null) { Debug.LogWarning("PSX grass: textura no encontrada " + PsxTexDir + texName + ".png"); return null; }
            return new DetailPrototype
            {
                usePrototypeMesh = false,          // ← textura, no malla
                prototypeTexture = tex,
                renderMode = DetailRenderMode.GrassBillboard,
                useInstancing = false,             // instancing solo aplica a detalles de malla
                minWidth = minW, maxWidth = maxW,
                minHeight = minH, maxHeight = maxH,
                noiseSpread = 0.3f,
                healthyColor = healthy,
                dryColor = dry
            };
        }

        static DetailPrototype LowPolyDetail(string prefabPath, float minW, float maxW, float minH, float maxH)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning("Low-poly detail no encontrado: " + prefabPath); return null; }
            return new DetailPrototype
            {
                usePrototypeMesh = true,
                prototype = prefab,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = minW, maxWidth = maxW,
                minHeight = minH, maxHeight = maxH,
                noiseSpread = 0.3f,
                healthyColor = Color.white,
                dryColor = Color.white
            };
        }

        static DetailPrototype PackDetail(string prefabName, float minWidth, float maxWidth, float minHeight, float maxHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TerrainSampleAssets/Prefabs/" + prefabName + ".prefab");
            if (prefab == null) return null;

            // darken/tint the grass by swapping ONLY its base-color texture(s) for a
            // recoloured copy - the shadergraph (and its wind waving) is untouched.
            // PASTO SECO/QUEMADO (owner: "muy verde plástico, hacelo seco"): tinte
            // khaki cálido - sube el rojo, baja el verde y hunde el azul para matar
            // el verde y darle el tono pajizo/otoñal.
            DarkenGrassBaseColor(prefab, new Color(0.40f, 0.50f, 0.27f));

            // optional: swap to the custom Folklore/GrassFade shader so the grass
            // dither-fades in near the cull distance instead of popping. When off,
            // actively restore the pack shader (the swap is persistent on the material
            // asset, so turning the flag off must undo it - Unity keeps the original
            // Base_Map/Texture2D_ texture props, so restoring the shader brings back
            // the darkened pack grass automatically).
            if (MapLayout.GrassDistanceFade) ApplyGrassFadeShader(prefab);
            else RestorePackGrassShader(prefab);

            return new DetailPrototype
            {
                usePrototypeMesh = true,
                prototype = prefab,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = minWidth, maxWidth = maxWidth,
                minHeight = minHeight, maxHeight = maxHeight,
                noiseSpread = 0.3f,
                healthyColor = Color.white,
                dryColor = Color.white
            };
        }

        // Sets the GLOBAL fade range read by Folklore/GrassFade. Called at generation
        // (night default) and re-called by the day/night toggle so the fade always
        // ends exactly at the current detailObjectDistance. Passing a cull distance of
        // 0 (or GrassDistanceFade off) disables the fade.
        // Apaga/enciende la translucidez de las hojas de los Conifers BOTD según
        // MapLayout.TreeLeafTranslucency. Apagada = más barato por pixel (menos glow
        // a contraluz de día). NO toca el viento de las hojas. Reversible: cambiá el
        // flag y regenerá. Guarda el valor original una vez para poder restaurarlo.
        const string LeafMatPath = "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Sources/Materials/M Conifer Leaves URP.mat";

        // Instancia el windzone de CTI que maneja el viento de los Conifers BOTD. Sin
        // esto, las hojas de los árboles del terreno no se mueven (el shader tiene el
        // viento pero necesita este driver global). Es una sola componente, casi 0 FPS.
        const string WindzonePath = "Assets/Forst/CTI Runtime Components/CTI Runtime Components URP 14plus/Prefabs/CTI Windzone URP.prefab";
        static void AddTreeWind(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindzonePath);
            if (prefab == null) { Debug.LogWarning("CTI Windzone no encontrado - los árboles no tendrán viento."); return; }
            var wz = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            wz.name = "CTI_Windzone";
            wz.transform.SetParent(parent);
            wz.transform.position = Vector3.zero;
        }
        static void ApplyTreeLeafSettings()
        {
            var leaf = AssetDatabase.LoadAssetAtPath<Material>(LeafMatPath);
            if (leaf == null) return; // BOTD no presente
            // _AmbientTranslucency: enum 0=off .. 2=high. On restaura a 2, off = 0.
            leaf.SetFloat("_AmbientTranslucency", MapLayout.TreeLeafTranslucency ? 2f : 0f);
            // por si el shadergraph usa keyword para la variante (best-effort, inocuo
            // si el keyword no existe):
            if (MapLayout.TreeLeafTranslucency) leaf.EnableKeyword("_AMBIENTTRANSLUCENCY");
            else leaf.DisableKeyword("_AMBIENTTRANSLUCENCY");
            // Restaurar SIEMPRE el viento de las hojas (venía activo en el material
            // original; un guardado previo pudo dejarlo off). Nunca lo apagamos.
            leaf.EnableKeyword("_LEAFTUMBLING");
            leaf.EnableKeyword("_LEAFTURBULENCE");
            EditorUtility.SetDirty(leaf);
            AssetDatabase.SaveAssets();
        }

        public static void SetGrassFadeGlobals(float cullDistance)
        {
            if (!MapLayout.GrassDistanceFade) { Shader.SetGlobalFloat("_GrassFadeEnd", 0f); return; }
            Shader.SetGlobalFloat("_GrassFadeEnd", cullDistance);
            Shader.SetGlobalFloat("_GrassFadeStart", Mathf.Max(0f, cullDistance - MapLayout.GrassFadeMargin));
        }

        // Restores a grass detail prefab's material(s) to the pack's TerrainGrass
        // shadergraph (undoing ApplyGrassFadeShader). Unity keeps the material's saved
        // texture props (Base_Map etc. with the darkened copy) even after a shader
        // swap, so just re-assigning the shadergraph brings back the original look.
        static void RestorePackGrassShader(GameObject prefab)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/TerrainSampleAssets/ShaderGraphs/TerrainGrass.shadergraph");
            if (shader == null) return; // pack not present; nothing to restore
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == shader) continue;
                    m.shader = shader;
                    EditorUtility.SetDirty(m);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // Swaps a grass detail prefab's material(s) to Folklore/GrassFade, carrying
        // over the (already-darkened) albedo texture so it looks the same but fades.
        static void ApplyGrassFadeShader(GameObject prefab)
        {
            var shader = Shader.Find("Folklore/GrassFade");
            if (shader == null)
            {
                Debug.LogWarning("Folklore/GrassFade shader not found - grass will keep the pack material (no distance fade).");
                return;
            }
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    // grab the current albedo (the darkened copy) BEFORE swapping shader
                    Texture albedo = null;
                    var sh = m.shader;
                    int count = sh.GetPropertyCount();
                    for (int i = 0; i < count && albedo == null; i++)
                    {
                        if (sh.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                        var t = m.GetTexture(sh.GetPropertyName(i));
                        if (t == null) continue;
                        string tn = t.name.ToLowerInvariant();
                        if (tn.Contains("basecolor") || tn.Contains("albedo") || tn.Contains("diffuse") || tn.Contains("grass"))
                            albedo = t;
                    }
                    m.shader = shader;
                    if (albedo != null) m.SetTexture("_BaseMap", albedo);
                    m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_Cutoff", 0.4f);
                    EditorUtility.SetDirty(m);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // Replace EVERY base-color texture the grass material samples with a darkened
        // copy. The pack grass shadergraph references its albedo texture under more
        // than one property name (e.g. both "Base_Map" and an internal
        // "Texture2D_..." slot) and samples via the internal one - which is why only
        // reassigning "Base_Map" did nothing before. We swap them all. Only albedo
        // maps are touched (never normal/mask/thickness), and the SHADER is left
        // alone so the wind animation still works.
        static void DarkenGrassBaseColor(GameObject prefab, Color mul)
        {
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var shader = m.shader;
                    int count = shader.GetPropertyCount();
                    for (int i = 0; i < count; i++)
                    {
                        if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                        string prop = shader.GetPropertyName(i);
                        var tex = m.GetTexture(prop) as Texture2D;
                        if (tex == null) continue;
                        string tn = tex.name.ToLowerInvariant();
                        if (!(tn.Contains("basecolor") || tn.Contains("albedo") || tn.Contains("diffuse"))) continue;
                        var dark = DarkenTextureCached(tex, mul);
                        if (dark != null) m.SetTexture(prop, dark);
                    }
                    EditorUtility.SetDirty(m);
                }
            }
            AssetDatabase.SaveAssets();
        }

        const string DarkPrefix = "tex_darkgrass_";

        static Texture2D DarkenTextureCached(Texture2D assigned, Color mul)
        {
            if (assigned == null) return null;

            // strip any accumulated prefix to get the real base-texture name. (Bug
            // guard: previously the output re-prefixed the already-darkened texture
            // every regenerate -> tex_darkgrass_tex_darkgrass_...  until the path
            // blew past Windows' length limit and asset creation failed.)
            string cleanName = assigned.name;
            while (cleanName.StartsWith(DarkPrefix)) cleanName = cleanName.Substring(DarkPrefix.Length);
            // strip a previous tint tag too - the cache key now includes the tint
            // colour, so changing the tint regenerates instead of reusing the old copy
            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, "^c[0-9a-f]{6}_", "");

            // CACHE: if we've already made this darkened texture (same source AND same
            // tint), reuse it. Avoids deleting/recreating the asset and toggling the
            // source .tif's readable flag (a slow SaveAndReimport) on every regenerate.
            string tintTag = "c" + ColorUtility.ToHtmlStringRGB(mul).ToLowerInvariant() + "_";
            string outPath = MapLayout.GeneratedFolder + "/" + DarkPrefix + tintTag + cleanName + ".asset";
            var cached = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (cached != null) return cached;

            // always darken from the ORIGINAL texture (outside our Generated folder),
            // not a previously-darkened copy - otherwise the grass compounds darker.
            Texture2D src = assigned;
            if (assigned.name != cleanName || AssetDatabase.GetAssetPath(assigned).Contains("/Generated/"))
            {
                src = null;
                foreach (var guid in AssetDatabase.FindAssets(cleanName + " t:Texture2D"))
                {
                    var gp = AssetDatabase.GUIDToAssetPath(guid);
                    if (gp.Contains("/Generated/")) continue;
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(gp);
                    if (t != null && t.name == cleanName) { src = t; break; }
                }
                if (src == null) return null; // original not found, leave it be
            }

            // make the source readable so GetPixels works (blit-free = no gamma ambiguity)
            string srcPath = AssetDatabase.GetAssetPath(src);
            var importer = AssetImporter.GetAtPath(srcPath) as TextureImporter;
            bool wasReadable = importer != null && importer.isReadable;
            if (importer != null && !wasReadable) { importer.isReadable = true; importer.SaveAndReimport(); }

            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true);
            Color[] px;
            try { px = src.GetPixels(); }
            catch { if (importer != null && !wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); } return null; }

            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color(c.r * mul.r, c.g * mul.g, c.b * mul.b, c.a); // keep alpha (blade cutout)
            }
            tex.SetPixels(px);
            tex.Apply(true);
            tex.wrapMode = src.wrapMode;
            AssetDatabase.CreateAsset(tex, outPath);

            if (importer != null && !wasReadable) { importer.isReadable = false; importer.SaveAndReimport(); }
            return tex;
        }

        static void SetupProceduralGrass(TerrainData td)
        {
            td.SetDetailResolution(512, 32);
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode); // our density values are instance counts (0-16)

            var green = new DetailPrototype
            {
                prototypeTexture = GrassBladeTexture("grassblade", new Color(0.30f, 0.48f, 0.16f)),
                renderMode = DetailRenderMode.GrassBillboard,
                healthyColor = new Color(0.35f, 0.50f, 0.20f),
                dryColor = new Color(0.48f, 0.46f, 0.22f),
                minWidth = 0.9f, maxWidth = 1.6f,
                minHeight = 4.2f, maxHeight = 7.2f,
                noiseSpread = 0.35f
            };
            var dry = new DetailPrototype
            {
                prototypeTexture = GrassBladeTexture("drygrassblade", new Color(0.55f, 0.50f, 0.24f)),
                renderMode = DetailRenderMode.GrassBillboard,
                healthyColor = new Color(0.55f, 0.50f, 0.24f),
                dryColor = new Color(0.60f, 0.50f, 0.25f),
                minWidth = 1.0f, maxWidth = 1.8f,
                minHeight = 4.8f, maxHeight = 7.8f,
                noiseSpread = 0.30f
            };
            td.detailPrototypes = new[] { green, dry };

            int res = td.detailResolution;
            int[,] greenMap = new int[res, res];
            int[,] dryMap = new int[res, res];
            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    float wx = xi / (float)(res - 1) * MapLayout.MapSizeX;
                    float wz = zi / (float)(res - 1) * MapLayout.MapSize;
                    var p = new Vector2(wx, wz);

                    // sin pasto SOLO bajo la huella de la casa (que no atraviese el piso);
                    // sin pasto bajo la HUELLA de la casa y el galpón (que no atraviese el
                    // piso); afuera el pasto llega hasta las paredes (pedido del owner)
                    if (MapLayout.InRect(p, MapLayout.OldLadyHouseFootMin, MapLayout.OldLadyHouseFootMax, 0.2f)) continue;
                    if (MapLayout.InRect(p, MapLayout.OldLadyBarnFootMin, MapLayout.OldLadyBarnFootMax, 0.2f)) continue;

                    // claro del campamento (fogata + troncos + carpas + mesa)
                    if (Vector2.Distance(p, MapLayout.Campsite) < MapLayout.CampsiteClearRadius) continue;
                    // claro del campamento de los ladrones (suelo pisado)
                    if (Vector2.Distance(p, MapLayout.MainCriminalCamp) < 26f) continue;

                    float southDg = MapLayout.PavedRouteZAt(p.x) - p.y;
                    if (southDg > MapLayout.ShoreVegFar) continue; // out in the water
                    // lakeside grass grows up to the guardrail; only the tarmac side is cleared
                    if (southDg <= MapLayout.ShoreVegNear && BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute) < 10f) continue;
                    if (BuilderUtils.DistToPolyline(p, MapLayout.DirtRoad) < 5f) continue;
                    if (BuilderUtils.DistToPolyline(p, MapLayout.PathA) < 5f) continue;
                    if (BuilderUtils.DistToScaryPaths(p) < 4f) continue;
                    if (BuilderUtils.DistToRivers(p) < 18f) continue;

                    float df = Vector2.Distance(p, MapLayout.HuntingField);
                    float dm = Vector2.Distance(p, MapLayout.MainCriminalCamp);
                    bool dryZone = df < 55f || dm < 110f;

                    float n = Mathf.PerlinNoise(wx * 0.05f, wz * 0.05f);
                    int v = n > 0.25f ? Random.Range(8, 14) : Random.Range(4, 8);
                    if (dryZone) dryMap[zi, xi] = v + 2;
                    else greenMap[zi, xi] = v;
                }
            }
            td.SetDetailLayer(0, 0, 0, greenMap);
            td.SetDetailLayer(0, 0, 1, dryMap);
        }

        static Texture2D GrassBladeTexture(string name, Color c)
        {
            string path = MapLayout.GeneratedFolder + "/tex_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, clear);

            for (int b = 0; b < 26; b++)
            {
                int x0 = Random.Range(2, size - 2);
                int height = Random.Range(size / 3, size - 4);
                float lean = Random.Range(-0.3f, 0.3f);
                float bright = Random.Range(0.7f, 1.15f);
                var col = new Color(c.r * bright, c.g * bright, c.b * bright, 1f);
                for (int y = 0; y < height; y++)
                {
                    int xx = x0 + Mathf.RoundToInt(lean * y);
                    if (xx < 0 || xx >= size) break;
                    tex.SetPixel(xx, y, col);
                    if (y < height / 2 && xx + 1 < size) tex.SetPixel(xx + 1, y, col);
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        // ---------------- Standalone dry tree (used by LandmarkBuilder) ----------------

        public static void DryTree(Transform parent, Vector3 pos, Material trunkMat, Material dryMat)
        {
            float s = Random.Range(0.7f, 1.3f);
            var trunk = BuilderUtils.Prim(PrimitiveType.Cylinder, "DryTree", parent,
                pos + Vector3.up * 1.9f * s, new Vector3(0.18f * s, 1.9f * s, 0.18f * s), trunkMat,
                new Vector3(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f)));

            int branches = Random.Range(2, 5);
            for (int i = 0; i < branches; i++)
            {
                var branch = BuilderUtils.Prim(PrimitiveType.Cylinder, "Branch", trunk.transform,
                    pos + Vector3.up * 3.4f * s + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f)),
                    new Vector3(0.06f, 1.1f * s, 0.06f), dryMat,
                    new Vector3(Random.Range(20f, 75f), Random.Range(0f, 360f), 0f));
                Object.DestroyImmediate(branch.GetComponent<Collider>());
            }
        }
    }
}
