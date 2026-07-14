// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TerrainBuilder.cs — heightmap (rolling hills, criminal-camp
//  hill, enclosing ridges, riverbed, flat campsite) and noisy
//  ground textures (grass / dirt / concrete / dry grass).
// ============================================================
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FolkloreArchives.MapGen
{
    public static class TerrainBuilder
    {
        public const string TerrainAssetPath = "Assets/_FolkloreArchives/Generated/FolkloreTerrain.asset";

        // Subí este número cada vez que cambie la lógica del splat (barro/caminos) para
        // que el próximo Generate re-pinte el terreno cacheado una sola vez.
        const int SplatVersion = 13;
        const string SplatVersionKey = "Folklore_SplatVersion";

        public static Terrain Build(Transform parent)
        {
            // CACHE: el terreno es lo más caro de generar (pintar 2048² celdas con
            // distancias a caminos = ~3.6 min). Como es determinístico (Seed), si ya
            // existe el asset lo REUSAMOS y saltamos toda la generación. Para forzar el
            // rebuild (cambiaste altura/caminos/POIs): Tools > … > Rebuild Terrain.
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            if (td == null)
            {
                td = new TerrainData();
                td.heightmapResolution = 513;
                td.alphamapResolution = 2048; // ~0.5m/texel - fine enough to resolve the two worn wheel ruts on the dirt road
                td.size = new Vector3(MapLayout.MapSizeX, MapLayout.MaxHeight, MapLayout.MapSize);

                int res = td.heightmapResolution;
                float[,] h = ComputeProceduralHeights(res);
                TerrainEditPersistence.ApplyTerrainEdits(h, res);
                td.SetHeights(0, 0, h);
                PaintTextures(td);
                AssetDatabase.CreateAsset(td, TerrainAssetPath);
                EditorPrefs.SetInt(SplatVersionKey, SplatVersion);
            }
            else if (EditorPrefs.GetInt(SplatVersionKey, 0) != SplatVersion)
            {
                // Terreno cacheado pero el CÓDIGO del splat cambió (subí SplatVersion):
                // re-pinto el barro UNA vez sobre el cache (no en cada Generate, es caro).
                // Así el owner solo hace Generate y el barro aparece sin acordarse del botón.
                Debug.Log("<color=yellow>[SPLAT] version nueva → re-pintando el barro + despejando pasto sobre el barro (una vez)…</color>");
                PaintTextures(td);
                ClearGrassOnMud(td); // el pasto cacheado también tapa el barro: despejarlo acá
                EditorUtility.SetDirty(td);
                AssetDatabase.SaveAssets();
                EditorPrefs.SetInt(SplatVersionKey, SplatVersion);
            }

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(parent);
            go.transform.position = Vector3.zero;
            var terrain = go.GetComponent<Terrain>();
            // LOD del terreno: más alto = menos triángulos dibujados (se simplifica con
            // la distancia). 30 rebaja bastante los polígonos; con la niebla no se nota.
            // (Se aplica cada Generate, no necesita Rebuild Terrain.)
            terrain.heightmapPixelError = 30f;
            // dense-forest self-shadowing at night is invisible under fog anyway -
            // skip it entirely, it's a big chunk of the per-frame shadow pass cost
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            return terrain;
        }

        // Lleva la cámara del Scene view justo encima del CAMPAMENTO, mirando de arriba,
        // para poder VER el barro sin tener que buscarlo navegando (saca la ambigüedad
        // de "¿estoy parado en el barro o en el bosque?").
        [MenuItem("Tools/Folklore Archives/Ver el Campamento (camara)")]
        public static void FrameCampsite()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) { Debug.LogWarning("Abrí una ventana Scene primero."); return; }
            var t = Terrain.activeTerrain;
            float y = t != null ? t.SampleHeight(new Vector3(MapLayout.Campsite.x, 0f, MapLayout.Campsite.y)) : 12f;
            var center = new Vector3(MapLayout.Campsite.x, y, MapLayout.Campsite.y);
            sv.LookAt(center, Quaternion.Euler(72f, 0f, 0f), 22f); // picado cerrado, bien cerca
            sv.Repaint();
            Debug.Log($"<color=lime>Cámara sobre el CAMPAMENTO {MapLayout.Campsite}. Si acá NO ves barro marrón, el barro no se está pintando/despejando en el campamento.</color>");
        }

        // Borra el terreno cacheado → el próximo Generate lo rehace desde cero.
        // Usar cuando cambiaste altura/caminos/POIs/edits del terreno.
        [MenuItem("Tools/Folklore Archives/Rebuild Terrain (forzar)")]
        public static void ForceRebuildTerrain()
        {
            AssetDatabase.DeleteAsset(TerrainAssetPath);
            Debug.Log("<color=lime>Terreno cacheado borrado — el próximo Generate lo regenera (~3.6 min esa vez).</color>");
        }

        // RE-PINTA solo el splatmap (texturas del suelo: bosque verde + barro en
        // caminos/campamento) sobre el terreno cacheado, SIN borrar árboles/pasto ni
        // recomputar altura. Es la forma rápida de aplicar cambios de PaintTextures
        // (el barro de los caminos) sin el rebuild completo de 3.6 min.
        // La CAUSA de que el barro "nunca aparecía": PaintTextures solo corría al crear
        // el terreno; un Generate normal reusa el cache y jamás re-pinta.
        [MenuItem("Tools/Folklore Archives/Repaint Terrain (barro caminos)")]
        public static void RepaintTerrain()
        {
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainAssetPath);
            if (td == null) { Debug.LogWarning("No hay terreno cacheado — hacé Generate primero, después Repaint."); return; }
            PaintTextures(td);
            ClearGrassOnMud(td);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            // REFRESCAR el terreno VIVO de la escena: SetAlphamaps/SetDetailLayer tocan
            // el asset, pero el Terrain ya instanciado cachea el render del splat y del
            // pasto y NO se actualiza solo. Flush() lo obliga a re-leer del TerrainData.
            int flushed = 0;
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                if (t.terrainData != td) continue;
                t.terrainData = td;   // re-asignar por si quedó una copia
                t.Flush();
                flushed++;
            }
            Debug.Log($"<color=lime>Terreno RE-PINTADO: bosque VERDE + BARRO en caminos/campamento, pasto despejado sobre el barro. Terrenos vivos refrescados: {flushed}. Si sigue igual, hacé Generate Greybox Map.</color>");
        }

        // ¿este punto es zona de BARRO (camino a pie / campamento / rancho / galpón /
        // cabaña)? Mismo criterio que el splat en PaintTextures, para que el pasto se
        // despeje EXACTAMENTE donde está el barro (si no, el pasto denso tapa el barro).
        // El bosque general (fuera de estas zonas) NO se toca.
        // Ancho a cada lado de un sendero a pie (barro). El pasto verde llega a este borde.
        public const float FootTrailHalfWidth = 4.0f;

        // CLARO PELADO (barro SIN pasto): campamento, rancho, galpón, cabaña. Suelo pisado
        // de verdad — ahí no crece nada.
        public static bool IsClearing(Vector2 p)
        {
            if (Vector2.Distance(p, MapLayout.Campsite) < 12f) return true;
            if (Vector2.Distance(p, MapLayout.OldLadyHouseCenter) < 12f) return true;
            if (Vector2.Distance(p, MapLayout.OldLadyBarnCenter) < 8f) return true;
            if (Vector2.Distance(p, MapLayout.AbandonedCabin) < 13f) return true;
            return false;
        }

        // SENDERO a pie (barro CON pasto corto ralo encima): distancia al sendero más cercano.
        public static float DistToFootTrail(Vector2 p)
        {
            return Mathf.Min(BuilderUtils.DistToPolyline(p, MapLayout.PathA),
                   Mathf.Min(BuilderUtils.DistToScaryPaths(p),
                   Mathf.Min(BuilderUtils.DistToExtraTrails(p),
                             BuilderUtils.DistToPolyline(p, MapLayout.BeachPath))));
        }

        // ¿este punto tiene BARRO en el suelo (splat)? = claro pelado O sendero a pie.
        public static bool IsMudSpot(Vector2 p)
        {
            if (IsClearing(p)) return true;
            float footNoise = Mathf.PerlinNoise(p.x * 0.25f, p.y * 0.25f) * 0.3f;
            if (DistToFootTrail(p) < FootTrailHalfWidth + footNoise) return true;
            return false;
        }

        // Despeja el pasto DETAIL (todas las prototipos) SOLO sobre las zonas de barro,
        // sobre el terreno ya cacheado. Así el barro re-pintado queda a la vista sin
        // rehacer todo el bosque. El pasto del bosque queda como está.
        static void ClearGrassOnMud(TerrainData td)
        {
            int nproto = td.detailPrototypes != null ? td.detailPrototypes.Length : 0;
            if (nproto == 0) return;
            int res = td.detailResolution;
            int cleared = 0;
            var layers = new int[nproto][,];
            for (int i = 0; i < nproto; i++) layers[i] = td.GetDetailLayer(0, 0, res, res, i);
            for (int zi = 0; zi < res; zi++)
            {
                float wz = zi / (float)(res - 1) * MapLayout.MapSize;
                for (int xi = 0; xi < res; xi++)
                {
                    float wx = xi / (float)(res - 1) * MapLayout.MapSizeX;
                    if (!IsMudSpot(new Vector2(wx, wz))) continue; // pelar el pasto ALTO sobre todo el barro (claros + senderos) para que el barro se vea; el pasto CORTO ralo lo repone SetupGrass en el rebuild
                    for (int i = 0; i < nproto; i++)
                        if (layers[i][zi, xi] != 0) { layers[i][zi, xi] = 0; cleared++; }
                }
            }
            for (int i = 0; i < nproto; i++) td.SetDetailLayer(0, 0, i, layers[i]);
            Debug.Log($"<color=cyan>[Repaint] pasto despejado sobre el barro: {cleared} celdas detail limpiadas.</color>");
        }

        // Pure procedural heightmap (normalised 0..1), no hand-edits applied.
        // Shared by Build() and by TerrainEditPersistence when diffing the owner's
        // manual terrain edits against the procedural base.
        public static float[,] ComputeProceduralHeights(int res)
        {
            float[,] h = new float[res, res];
            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    float wx = xi / (float)(res - 1) * MapLayout.MapSizeX;
                    float wz = zi / (float)(res - 1) * MapLayout.MapSize;
                    h[zi, xi] = Mathf.Clamp01(HeightAt(wx, wz) / MapLayout.MaxHeight);
                }
            }
            return h;
        }

        static float HeightAt(float wx, float wz)
        {
            var p = new Vector2(wx, wz);

            // rolling base: wide soft hills + medium detail + finer bumps. Boosted
            // for more mountain-slope relief (FtF Ironbark sits on a hillside), not a
            // flat plane. Paths/campsite/river get flattened again further down.
            // Relieve rodante MÁS marcado (owner: que no sea plano/lineal, pero sin
            // exagerar): colinas amplias más altas + una capa extra de lomas medianas.
            float a = 8f
                + Mathf.PerlinNoise(wx * 0.0025f + 1.3f, wz * 0.0025f + 5.2f) * 17f   // colinas amplias
                + Mathf.PerlinNoise(wx * 0.006f + 8.4f, wz * 0.006f + 4.7f) * 9f      // lomas medianas (nueva)
                + Mathf.PerlinNoise(wx * 0.012f + 3.7f, wz * 0.012f + 9.1f) * 5f      // detalle medio
                + Mathf.PerlinNoise(wx * 0.025f + 6.1f, wz * 0.025f + 2.3f) * 2.2f;   // bumps finos

            // hill under the main criminal camp (visible from afar, "el monte")
            float dm = Vector2.Distance(p, MapLayout.MainCriminalCamp);
            a += 22f * Mathf.Exp(-(dm * dm) / (2f * 60f * 60f));

            // MONTAÑAS CENTRALES (rodean el lago gigante) - varios picos gaussianos
            float peakSig2 = 2f * MapLayout.CentralPeakSigma * MapLayout.CentralPeakSigma;
            foreach (var peak in MapLayout.CentralPeaks)
            {
                float dp = Vector2.Distance(p, peak);
                a += MapLayout.CentralPeakHeight * Mathf.Exp(-(dp * dp) / peakSig2);
            }

            // enclosing ridges: west / north / far-east walls of hills
            // (skipped near the paved route so the road stays flat)
            float ridge = Mathf.Clamp01((wz - 900f) / 100f); // north, always
            if (wz > 150f)
            {
                ridge = Mathf.Max(ridge, Mathf.Clamp01((110f - wx) / 110f)); // west
                ridge = Mathf.Max(ridge, Mathf.Clamp01((wx - (MapLayout.MapSizeX - 60f)) / 60f));  // far east (follows the wider map)
            }
            a += ridge * ridge * 16f * (0.7f + Mathf.PerlinNoise(wx * 0.02f, wz * 0.02f) * 0.6f);

            // keep the paved route mostly level - but not laser-flat: a real old road
            // has slight dips/bumps, so blend in a little noise near the centreline,
            // fading out toward the shoulders so it doesn't fight the flattening.
            float dPav = BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute);
            if (dPav < 16f)
            {
                float potholes = (Mathf.PerlinNoise(wx * 0.15f, wz * 0.15f) - 0.5f) * 0.5f; // +/- 0.25m, "leves"
                // was 11f - the surrounding rolling-hill noise here averages noticeably
                // higher than that now, so the road sat in a visible trench below its
                // own shoulders ("como un pozo"). Raised closer to the local grade.
                float flat = MapLayout.RoadSurfaceHeight + potholes * Mathf.Clamp01(1f - dPav / 8f);
                a = Mathf.Lerp(flat, a, Mathf.SmoothStep(0f, 1f, (dPav - 8f) / 8f));
            }

            // LAKESIDE: south of the road (away from the forest) the ground drops
            // behind the guardrail into a lake. Carve the terrain down to a lakebed
            // floor below the waterline so the water plane shows. Min() so it only ever
            // lowers the ground, never fights the road flatten or raises anything.
            float roadZ = MapLayout.PavedRouteZAt(wx);
            float southDist = roadZ - wz; // >0 means we're on the lake side
            if (southDist > MapLayout.LakeShoulderWidth)
            {
                float t = Mathf.Clamp01((southDist - MapLayout.LakeShoulderWidth) / MapLayout.LakeSlopeWidth);
                // slight wobble on the shoreline so it isn't a dead-straight bathtub edge
                float shoreWobble = (Mathf.PerlinNoise(wx * 0.03f, wz * 0.03f) - 0.5f) * 3f;
                float bed = Mathf.Lerp(MapLayout.RoadSurfaceHeight - 1f, MapLayout.LakeBedHeight + shoreWobble, t);
                a = Mathf.Min(a, bed);
            }

            // flat clearing for the campsite (tents, campfire)
            float dc = Vector2.Distance(p, MapLayout.Campsite);
            if (dc < 40f) a = Mathf.Lerp(12f, a, Mathf.SmoothStep(0f, 1f, (dc - 15f) / 25f));

            // riverbed carve (last, so it always wins)
            float dr = BuilderUtils.DistToPolyline(p, MapLayout.River);
            if (dr < 34f) a = Mathf.Lerp(3.5f, a, Mathf.SmoothStep(0f, 1f, (dr - 14f) / 20f));

            // segundo río (tributario del lago) — canal algo más angosto
            float dr2 = BuilderUtils.DistToPolyline(p, MapLayout.River2);
            if (dr2 < 26f) a = Mathf.Lerp(4.5f, a, Mathf.SmoothStep(0f, 1f, (dr2 - 10f) / 16f));

            // LAGO GIGANTE CENTRAL: cuenca carvada bajo la línea de agua. Min() = solo
            // baja el terreno, nunca sube (las montañas de alrededor quedan; adentro
            // gana el lago). Orilla irregular con ruido para que no sea un círculo perfecto.
            float dCL = Vector2.Distance(p, MapLayout.CentralLakeCenter);
            if (dCL < MapLayout.CentralLakeRadius + MapLayout.CentralLakeShore)
            {
                float wob = (Mathf.PerlinNoise(wx * 0.02f + 11f, wz * 0.02f + 7f) - 0.5f) * 10f;
                float t = Mathf.SmoothStep(0f, 1f, (dCL - MapLayout.CentralLakeRadius) / MapLayout.CentralLakeShore);
                a = Mathf.Min(a, Mathf.Lerp(MapLayout.CentralLakeBed + wob, a, t));
            }

            // MINI PLAYA DE PESCA junto al campamento (orilla oeste del río): una
            // plataforma arenosa plana ~1m sobre la línea de agua (7m) para poder
            // pararse a pescar, con un repecho suave que la conecta con el campamento.
            // Min() = solo BAJA el terreno del lado de tierra, nunca rellena el cauce
            // (el lado del agua queda por debajo y sigue mostrando agua).
            float dbe = Vector2.Distance(p, MapLayout.RiverBeach);
            if (dbe < 15f)
            {
                float beachH = 8.2f;
                float tb = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dbe - 5f) / 10f));
                a = Mathf.Min(a, Mathf.Lerp(beachH, a, tb));
            }

            // APLANAR el lote de la casa de la vieja: el terreno dentro del cerco queda
            // a nivel (OldLadyLotHeight) para que la casa no flote, con transición suave
            // de 12m hacia afuera. Va al final para que gane sobre lo demás.
            float lotDX = Mathf.Max(0f, Mathf.Max(MapLayout.OldLadyLotMin.x - wx, wx - MapLayout.OldLadyLotMax.x));
            float lotDZ = Mathf.Max(0f, Mathf.Max(MapLayout.OldLadyLotMin.y - wz, wz - MapLayout.OldLadyLotMax.y));
            float lotDist = Mathf.Sqrt(lotDX * lotDX + lotDZ * lotDZ);   // 0 = dentro del lote
            if (lotDist < 12f)
            {
                // altura natural del terreno justo AFUERA del lote (fuera del radio de
                // aplanado, así no recursiona) → la casa se asienta sola donde esté la
                // vieja, sin depender de una constante hardcodeada.
                float lotGrade = HeightAt(MapLayout.OldLadyLotMax.x + 20f,
                                          (MapLayout.OldLadyLotMin.y + MapLayout.OldLadyLotMax.y) * 0.5f);
                a = Mathf.Lerp(lotGrade, a, Mathf.SmoothStep(0f, 1f, lotDist / 12f));
            }

            return a;
        }

        static void PaintTextures(TerrainData td)
        {
            // Real textures from the Terrain Sample Asset Pack, with procedural fallback.
            // Con UsePsxGround, las capas naturales (pasto/tierra/sendero/arena) usan las
            // texturas seamless 128px del pack PSX. La RUTA ASFALTADA y la NIEVE siguen
            // como estaban: el pack PSX no trae asfalto ni nieve.
            bool psx = MapLayout.UsePsxGround;
            var layers = new TerrainLayer[7];
            // capa 0 (base) = PASTO VERDE (el bosque general queda verde). El barro
            // (Ground071) va SOLO en caminos/claros vía las otras capas.
            layers[0] = (psx ? PsxLayer("PSX_Seamless_WildForestGrass_128px",   6f) : null)
                        ?? PackLayer("Grass_A_TerrainLayer",  "grass",    new Color(0.16f, 0.30f, 0.12f));
            // SUELO BASE: barro MARRÓN de verdad (Ground054). Antes usaba el PSX
            // ForestEarthGround, que es VERDOSO → con BaseMudBlend=1 todo el suelo salía
            // verde. Forzado a marrón (el owner quiere el suelo de barro).
            layers[1] = MuddyDirtLayer();
            layers[2] = PavedRoadLayer();   // asfalto: sin equivalente PSX
            layers[3] = (psx ? PsxLayer("PSX_Seamless_ForestDryGround_128px",   6f) : null)
                        ?? PackLayer("Grass_Dry_TerrainLayer","drygrass", new Color(0.55f, 0.50f, 0.25f));
            // SENDEROS: barro MARRÓN de verdad (Ground054), NO el "wild ground" PSX que
            // tira a verde. Forzado aunque el resto del piso sea PSX (el owner quería barro).
            layers[4] = TrailLayer();
            layers[5] = (psx ? PsxLayer("PSX_Seamless_ForestGravel_Ground_128px", 5f) : null)
                        ?? PackLayer("Sand_TerrainLayer",     "sand",     new Color(0.76f, 0.70f, 0.50f));
            layers[6] = CreateLayer("snow", new Color(0.92f, 0.94f, 0.98f)); // nieve de los picos
            td.terrainLayers = layers;

            int res = td.alphamapResolution;
            float[,,] map = new float[res, res, 7];
            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    float wx = xi / (float)(res - 1) * MapLayout.MapSizeX;
                    float wz = zi / (float)(res - 1) * MapLayout.MapSize;
                    var p = new Vector2(wx, wz);

                    float dirt = 0f, dry = 0f;

                    // dry grass: hunting field + criminal hill area
                    float df = Vector2.Distance(p, MapLayout.HuntingField);
                    float dm = Vector2.Distance(p, MapLayout.MainCriminalCamp);
                    if (df < 55f) dry = Mathf.Max(dry, 0.85f * (1f - Mathf.Clamp01((df - 35f) / 20f)));
                    if (dm < 130f) dry = Mathf.Max(dry, 0.6f * (1f - Mathf.Clamp01((dm - 80f) / 50f)));

                    // river banks
                    float dr = BuilderUtils.DistToPolyline(p, MapLayout.River);
                    if (dr < 40f) dirt = Mathf.Max(dirt, 1f - Mathf.Clamp01((dr - 16f) / 24f));

                    // BARRO bordeando el lago central (owner: orilla de barro, sin árboles)
                    float dCL = Vector2.Distance(p, MapLayout.CentralLakeCenter);
                    if (dCL < MapLayout.CentralLakeRadius + 30f)
                        dirt = Mathf.Max(dirt, 1f - Mathf.Clamp01((dCL - (MapLayout.CentralLakeRadius - 8f)) / 38f));

                    // lakeside: the upper embankment stays grassy (shore grass/bushes/
                    // pines grow there); only the last few metres down to the waterline
                    // and the lakebed itself go bare gravel/dirt, like the RN40 photos.
                    float southDist = MapLayout.PavedRouteZAt(wx) - wz;
                    if (southDist > MapLayout.ShoreVegFar - 4f)
                        dirt = Mathf.Max(dirt, Mathf.Clamp01((southDist - (MapLayout.ShoreVegFar - 4f)) / 6f));

                    // dirt road to the campsite - now an OVERGROWN two-track: bare dirt
                    // only in the two worn wheel ruts, grass everywhere else (median +
                    // verges), like a road a car occasionally drives. DistToPolyline is
                    // distance from the centre line, so a single band offset from centre
                    // = both symmetric ruts at once. ForestBuilder.SetupGrass keeps grass
                    // OFF this same rut band so the dirt shows through there.
                    // CAMINO DE TIERRA (ruta→campamento): DOBLE huella de ruedas de auto
                    // (como estaba) - dos franjas peladas centradas ±1.1m de la línea.
                    float dRoadCentre = BuilderUtils.DistToPolyline(p, MapLayout.DirtRoad);
                    float rutNoise = Mathf.PerlinNoise(wx * 0.2f, wz * 0.2f) * 0.15f;
                    float trail = (Mathf.Abs(dRoadCentre - 1.1f) < (0.55f + rutNoise)) ? 1f : 0f;

                    // SENDEROS A PIE (Path A/B, túneles, caminos nuevos, playa): UNA SOLA
                    // huella angosta y chica (~1m), caminito individual tipo Lago Queñi.
                    float footNoise = Mathf.PerlinNoise(wx * 0.25f, wz * 0.25f) * 0.3f;
                    float dFootTr = Mathf.Min(BuilderUtils.DistToPolyline(p, MapLayout.PathA),
                                    Mathf.Min(BuilderUtils.DistToScaryPaths(p),
                                    Mathf.Min(BuilderUtils.DistToExtraTrails(p),
                                              BuilderUtils.DistToPolyline(p, MapLayout.BeachPath))));
                    if (dFootTr < FootTrailHalfWidth + footNoise) trail = 1f;   // sendero (~2.5m): barro con pasto corto ralo encima (el pasto lo pone SetupGrass)
                    // CLAROS de barro: campamento, rancho de la vieja y su galpón (owner:
                    // esos lugares pisados tienen que ser tierra/barro, no pasto).
                    if (Vector2.Distance(p, MapLayout.Campsite) < MapLayout.CampsiteClearRadius + 2f) trail = 1f;
                    if (Vector2.Distance(p, MapLayout.OldLadyHouseCenter) < 12f) trail = 1f;
                    if (Vector2.Distance(p, MapLayout.OldLadyBarnCenter) < 8f) trail = 1f;
                    if (Vector2.Distance(p, MapLayout.AbandonedCabin) < 13f) trail = 1f;
                    // orillas arenosas del río: una franja de arena a lo largo de toda
                    // la ribera, enmascarada por ALTURA — desde justo bajo la línea de
                    // agua (7m) hasta ~2m por encima. Así la bajada del campamento al
                    // agua (y la plataforma de pesca, a 8.2m) es arena, pero el cauce
                    // hondo (3.5m) y el terreno alto quedan como estaban.
                    float sand = 0f;
                    if (dr < 34f)
                    {
                        float hBank = HeightAt(wx, wz);
                        float fadeIn  = Mathf.Clamp01((hBank - 6.2f) / 0.8f);  // aparece llegando al agua
                        float fadeOut = 1f - Mathf.Clamp01((hBank - 9.2f) / 1.3f); // muere ladera arriba
                        sand = fadeIn * fadeOut;
                    }

                    // paved route: asymmetric shoulder widths, not a symmetric strip.
                    // South (lake side) keeps the old narrow 4.5/6.2m fade so the asphalt
                    // ends right at the guardrail (GuardrailOffset=5.5m) instead of
                    // bleeding onto the embankment - can't widen this side further without
                    // also moving the guardrail/lake carve. North (forest side) is widened
                    // to ~12-14m (owner: wants each lane roomy enough for a car on either
                    // side of the centre line) - matches/slightly exceeds ForestBuilder's
                    // ~12-13m tree-clearance radius so it still reaches close to the
                    // treeline instead of leaving a bare gap.
                    float dPavCentre = BuilderUtils.DistToPolyline(p, MapLayout.PavedRoute);
                    bool northOfRoad = wz >= MapLayout.PavedRouteZAt(wx);
                    float concrete = northOfRoad
                        ? BuilderUtils.Strip(dPavCentre, 12f, 14f)
                        : BuilderUtils.Strip(dPavCentre, 4.5f, 6.2f);

                    float w2 = concrete;
                    float w4 = trail * (1f - w2);
                    float w5 = sand * (1f - w2 - w4);
                    float w1 = dirt * (1f - w2 - w4 - w5);
                    float w3 = dry * (1f - w2 - w4 - w5 - w1);
                    float w0 = Mathf.Max(0f, 1f - w1 - w2 - w3 - w4 - w5);

                    // CLAROS PISADOS = BARRO Ground071 SÍ O SÍ (pisa asfalto/arena/pasto/
                    // lo que sea). Campamento, rancho, galpón, cabaña abandonada.
                    if (Vector2.Distance(p, MapLayout.Campsite) < 12f
                     || Vector2.Distance(p, MapLayout.OldLadyHouseCenter) < 12f
                     || Vector2.Distance(p, MapLayout.OldLadyBarnCenter) < 8f
                     || Vector2.Distance(p, MapLayout.AbandonedCabin) < 13f)
                    { w0 = 0f; w1 = 0f; w2 = 0f; w3 = 0f; w5 = 0f; w4 = 1f; }

                    // SUELO BASE EMBARRADO: lo que quedaba como pasto verde puro (w0)
                    // se reparte entre pasto y barro (capa Muddy) con manchones Perlin
                    // grandes, para que el suelo del mapa lea marrón/barro con parches
                    // verdes en vez de verde uniforme. Palanca: MapLayout.BaseMudBlend.
                    // Con BaseMudBlend=0 el owner quiere el BOSQUE 100% VERDE (nada de
                    // barro moteado en el piso del bosque). Solo si BaseMudBlend>0 se
                    // reparte pasto/barro por ruido. El barro de caminos/claros (w4) es
                    // aparte y no depende de esto.
                    if (MapLayout.BaseMudBlend > 0f)
                    {
                        float mudNoise = Mathf.PerlinNoise(wx * 0.016f + 37.2f, wz * 0.016f + 91.4f);
                        float baseMud = Mathf.Clamp01(MapLayout.BaseMudBlend + (mudNoise - 0.5f) * 0.45f);
                        w1 += w0 * baseMud;
                        w0 *= 1f - baseMud;
                    }

                    // NIEVE en los picos altos: por altura del terreno. La nieve pisa
                    // las demás capas arriba de la línea de nieve (SnowLine).
                    float hSnow = HeightAt(wx, wz);
                    float snow = Mathf.SmoothStep(0f, 1f, (hSnow - MapLayout.SnowLine) / 14f);
                    float keep = 1f - snow;

                    map[zi, xi, 0] = w0 * keep;
                    map[zi, xi, 1] = w1 * keep;
                    map[zi, xi, 2] = w2 * keep;
                    map[zi, xi, 3] = w3 * keep;
                    map[zi, xi, 4] = w4 * keep;
                    map[zi, xi, 5] = w5 * keep;
                    map[zi, xi, 6] = snow;
                }
            }
            td.SetAlphamaps(0, 0, map);
            Debug.Log($"<color=cyan>[SPLAT v3] barro Ground071 en caminos + campamento + rancho. BaseMudBlend={MapLayout.BaseMudBlend} (0=base verde). Si NO ves este mensaje al regenerar, el codigo no compilo.</color>");
        }

        // Base ground "barro": the pack's Muddy layer texture is a greenish olive,
        // so even painting the whole map with it still read as green (owner: "el piso
        // tiene que ser tierra, no verde"). We tint its diffuse to an unmistakable
        // warm dirt-brown (MapLayout.MudTint) via a generated copy and keep its real
        // normal map for surface detail. Falls back to the plain pack layer / a flat
        // brown if the textures aren't present.
        static TerrainLayer MuddyDirtLayer()
        {
            // BARRO DE BOSQUE (ambientCG Ground071: tierra + ramitas, estilo Fears to
            // Fathom). Fallback a Ground054 y luego a la Muddy tintada.
            const string g071 = "Assets/ExternalAssets/TerrainTextures/Ground071/";
            const string g054 = "Assets/ExternalAssets/TerrainTextures/Ground054/";
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(g071 + "Ground071_2K-JPG_Color.jpg");
            Texture2D normal = null;
            if (diffuse != null) normal = BuilderUtils.LoadAsNormalMap(g071 + "Ground071_2K-JPG_NormalGL.jpg");
            else
            {
                diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(g054 + "Ground054_1K-JPG_Color.jpg");
                if (diffuse != null) normal = BuilderUtils.LoadAsNormalMap(g054 + "Ground054_1K-JPG_NormalGL.jpg");
            }
            if (diffuse == null)
            {
                diffuse = BuilderUtils.Tint(
                    "Assets/TerrainSampleAssets/Textures/Terrain/Muddy_BaseColor.tif",
                    MapLayout.MudTint, "muddy_dirt_tinted");
                if (diffuse == null)
                    return PackLayer("Muddy_TerrainLayer", "dirt", new Color(0.42f, 0.30f, 0.18f));
                normal = BuilderUtils.LoadAsNormalMap(
                    "Assets/TerrainSampleAssets/Textures/Terrain/Muddy_Normal.tif");
            }

            string layerPath = MapLayout.GeneratedFolder + "/layer_muddydirt.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = diffuse;
            if (normal != null) layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(7f, 7f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static TerrainLayer TrailLayer()
        {
            // BARRO DE BOSQUE (Ground071, el que eligió el owner) en los senderos.
            // Fallback: Ground054, luego ground02, luego color.
            const string g071 = "Assets/ExternalAssets/TerrainTextures/Ground071/";
            const string g054 = "Assets/ExternalAssets/TerrainTextures/Ground054/";
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(g071 + "Ground071_2K-JPG_Color.jpg");
            Texture2D normal = null;
            if (diffuse != null) normal = BuilderUtils.LoadAsNormalMap(g071 + "Ground071_2K-JPG_NormalGL.jpg");
            else
            {
                diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(g054 + "Ground054_1K-JPG_Color.jpg");
                if (diffuse != null) normal = BuilderUtils.LoadAsNormalMap(g054 + "Ground054_1K-JPG_NormalGL.jpg");
            }
            if (diffuse == null)
                diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(MapLayout.NatureKitFolder + "/ground02.tga");
            if (diffuse == null)
                return CreateLayer("dirt", new Color(0.42f, 0.30f, 0.18f));

            string layerPath = MapLayout.GeneratedFolder + "/layer_traildirt.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            layer.diffuseTexture = diffuse;
            if (normal != null) layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(4f, 4f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // Kajaman's Roads ships real dark asphalt textures with lane markings
        // (Road_2lane_dark02), which reads unambiguously as a paved road - much
        // better than Yughues' stone/paver patterns (kept as the CreateLayer
        // fallback color below only if this texture is ever missing). The source
        // texture is authored with the markings running along its V axis; since
        // Unity terrain layers map U->world X and our road runs mostly along X,
        // it's rotated 90 deg via BuilderUtils.Rotate90 below so the dashes line up
        // "along the road" instead of across it (see DEV_LOG.md). Terrain layers
        // still can't rotate PER-SEGMENT to follow the road's curve though, so the
        // lines will still be very slightly off-angle through the bends - only a
        // real fix for that is actual road meshes, which this free pack doesn't
        // provide as placeable modular pieces.
        static TerrainLayer PavedRoadLayer()
        {
            string diffusePath = "Assets/KajamansRoads/Textures/Road_2lane_dark02.png";
            string normalPath = "Assets/KajamansRoads/Textures/Road_2lane_dark02_n.png";
            // Force-import in case the asset exists on disk but the AssetDatabase
            // hasn't indexed it yet (e.g. it was just added this session) - cheap
            // no-op if it's already imported.
            AssetDatabase.ImportAsset(diffusePath, ImportAssetOptions.Default);
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuse == null)
            {
                Debug.LogWarning("Kajaman's Roads texture not found at " + diffusePath + " - paved route falls back to the Rock layer (this is why the road can look like bare tan/grey rock instead of dark asphalt).");
                return PackLayer("Rock_TerrainLayer", "concrete", new Color(0.45f, 0.45f, 0.47f));
            }

            string layerPath = MapLayout.GeneratedFolder + "/layer_pavedroad.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }
            // Kajaman's texture is authored with the lane markings running along its
            // V axis; Unity terrain layers map U->world X and V->world Z, and our
            // road runs mostly along X, so the raw texture would show markings
            // running ACROSS the road instead of along it. Rotate 90 deg so the
            // dash pattern lines up with tileSize.x (along-road) instead.
            layer.diffuseTexture = BuilderUtils.Rotate90(diffusePath, false, "pavedroad_diffuse_rot");
            layer.normalMapTexture = BuilderUtils.Rotate90(normalPath, true, "pavedroad_normal_rot");
            // Empirically, TerrainLayer.tileSize.x ended up controlling the ACROSS-
            // road repeat and .y the ALONG-road one (opposite of the doc-assumed
            // U->X/V->Z mapping) - owner saw the centre/edge lines duplicated ~2x
            // across the width with (9, 20). Swapped: x=across-road (past the
            // widest paved section so only ONE tile spans the width - kept
            // proportional to the ~4.5:26 fade-edge:tileSize ratio that already
            // looked right, as the north shoulder widened from 12m to 14m), y=9
            // (along-road dash spacing, ~matches the original pack design).
            layer.tileSize = new Vector2(29f, 9f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ── Capas de terreno PSX (StarkCrafts) ──────────────────────────────
        // Texturas seamless de 128px que vienen con el pack. El look PS1 no sale de la
        // geometría (el terreno es un mesh, no hay "low poly" que valga) sino de la
        // textura de baja resolución SIN suavizar: por eso les fuerzo filterMode = Point.
        // Los mipmaps quedan ENCENDIDOS: sin ellos el piso hace ruido/aliasing horrible
        // a la distancia, que es peor que el pixelado que buscamos.
        const string PsxGroundDir = "Assets/StarkCrafts/PSX_Forest_Level_byStarkCrafts/PSX_ForestGround_Tex/";

        static TerrainLayer PsxLayer(string texName, float tile)
        {
            string texPath = PsxGroundDir + texName + ".png";
            var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (imp != null && imp.filterMode != FilterMode.Point)
            {
                imp.filterMode = FilterMode.Point;   // ← el pixelado PS1
                imp.mipmapEnabled = true;            // sin mips el piso lejano titila
                imp.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { Debug.LogWarning("PSX ground: falta " + texPath); return null; }

            string path = MapLayout.GeneratedFolder + "/psxlayer_" + texName + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = tex;
            layer.normalMapTexture = null;
            layer.tileSize = new Vector2(tile, tile);
            layer.specular = Color.black;   // el piso PSX es mate, sin brillos
            layer.metallic = 0f;
            layer.smoothness = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static TerrainLayer PackLayer(string packAsset, string fallbackName, Color fallbackColor)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                "Assets/TerrainSampleAssets/TerrainLayers/" + packAsset + ".terrainlayer");
            if (layer != null) return layer;
            return CreateLayer(fallbackName, fallbackColor);
        }

        static TerrainLayer CreateLayer(string name, Color color)
        {
            var tex = NoisyTexture("layer_" + name, color);
            string path = MapLayout.GeneratedFolder + "/layer_" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(9f, 9f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// Generates (or refreshes) a texture with natural-looking noise
        /// instead of a flat solid color.
        static Texture2D NoisyTexture(string name, Color c)
        {
            string path = MapLayout.GeneratedFolder + "/tex_" + name + ".asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = tex == null;
            const int S = 128;
            if (isNew) tex = new Texture2D(S, S, TextureFormat.RGBA32, false);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                    float n2 = Mathf.PerlinNoise(x * 0.45f + 7.3f, y * 0.45f + 2.9f);
                    float b = 0.75f + 0.4f * n + 0.2f * (n2 - 0.5f);
                    tex.SetPixel(x, y, new Color(c.r * b, c.g * b, c.b * b, 1f));
                }
            }
            tex.Apply();
            if (isNew) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            return tex;
        }
    }
}
