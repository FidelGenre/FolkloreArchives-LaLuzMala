// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  RoadTerrainBuilder.cs — owner: "quiero un terreno de Unity, lo más
//  chico posible, para que tenga bosque de los dos lados de la ruta".
//
//  La ruta EXTENDIDA a mano (PavedRoad_Surface (N)) flota sobre el vacío.
//  Los terrenos de Unity son rectángulos alineados a los ejes (no se
//  curvan), así que "lo más chico" = el AABB de la línea central de la
//  extensión + un margen. Como la extensión va en DIAGONAL (~-6°), ese
//  rectángulo queda largo y finito con la ruta cruzándolo → hay bosque
//  a AMBOS lados. Se rellena de pinos (los mismos Conifers [BOTD] del
//  bosque principal) dejando LIBRE el corredor de la ruta. Plano a la
//  altura de la ruta, con TerrainCollider.
//
//  Se rehace cada Generate leyendo la posición VIVA de la extensión
//  (si movés la ruta, el terreno la sigue). Corre DESPUÉS de
//  ApplySavedLayout (cuando la extensión ya está recreada).
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class RoadTerrainBuilder
    {
        const string TerrainName = "RoadExtensionTerrain";
        const string AssetPath   = "Assets/_FolkloreArchives/ExtraTerrains/RoadExtensionTerrain.asset";
        const string GrassLayerPath = "Assets/TerrainSampleAssets/TerrainLayers/Grass_A_TerrainLayer.terrainlayer";
        const string ConiferDir  = "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/";

        // ── Ajustes (tweak y regenerar) ─────────────────────────────────────
        const float Margin        = 50f;   // metros de bosque más allá del recorrido de la ruta (a cada lado)
        const float RoadClearHalf = 9f;    // corredor SIN árboles a cada lado del centro de la ruta
        const float TreeStep      = 4.5f;  // separación entre árboles (más chico = más denso). owner: "más densidad"
        // Los prototipos son "BigPine" (el pino escalado ×PineScale), así que la escala por
        // instancia del código es más chica para que el resultado final sea el mismo tamaño
        // grande de siempre (PineScale × TreeScale ≈ 2.7–4.5). Pintados a mano a 1–2× del
        // pincel salen grandes también (3–6×), que es lo que el owner quería.
        const float PineScale     = 3f;    // cuánto se agranda el prefab del pino
        const float TreeScaleMin  = 0.9f;
        const float TreeScaleMax  = 1.5f;
        const float RoadY         = 17.05f;// altura de la superficie de la ruta (MapLayout.RoadSurfaceHeight + lift)

        public static void Build(Transform mapRoot)
        {
            try { BuildInner(mapRoot); }
            catch (System.Exception e)
            {
                Debug.LogError("[RoadTerrain] excepción armando el terreno de la ruta: " + e);
            }
        }

        static void BuildInner(Transform mapRoot)
        {
            // ── PERMANENTE vs. REHACER ──
            // Si el asset del terreno ya está "committeado" (tiene los pinos agrandados
            // "BigPine..."), lo REUSAMOS tal cual → preserva lo que el owner editó a mano
            // (árboles pintados, altura, etc.). Solo si todavía tiene los pinos viejos (o no
            // existe) lo rehacemos UNA vez con los pinos agrandados; de ahí en más, permanente.
            // Para forzar un rehacer: borrar el asset RoadExtensionTerrain.asset.
            Terrain oldTerr = null;
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == TerrainName) { oldTerr = t; break; }

            var existingData = AssetDatabase.LoadAssetAtPath<TerrainData>(AssetPath);
            bool committed = existingData != null && existingData.treePrototypes != null &&
                System.Array.Exists(existingData.treePrototypes, p => p.prefab != null && p.prefab.name.StartsWith("BigPine"));
            // Reusar SOLO si además existe el GameObject del terreno (si no, es un estado roto:
            // asset sí pero terreno no → hay que rehacerlo, si no queda invisible).
            if (committed && oldTerr != null && oldTerr.terrainData != null)
            {
                oldTerr.transform.SetParent(mapRoot, true);
                oldTerr.allowAutoConnect = false;
                EnsureGrass(oldTerr, FindMainTerrain()); // agrega pasto si todavía no tiene (no toca alturas/árboles)
                Debug.Log("<color=lime>[RoadTerrain] terreno permanente reusado — ediciones a mano preservadas. " +
                          "(Para rehacerlo desde cero, borrá Assets/_FolkloreArchives/ExtraTerrains/RoadExtensionTerrain.asset).</color>");
                return;
            }
            // Rehacer: borrar el GameObject viejo (si hay) y el asset viejo, y armar de cero.
            if (oldTerr != null) Object.DestroyImmediate(oldTerr.gameObject);
            if (existingData != null) AssetDatabase.DeleteAsset(AssetPath);

            // Buscar las extensiones del asfalto ("PavedRoad_Surface (N)") en TODA la escena
            // (no solo como hijo directo del mapa: según cómo se recreó el duplicado puede
            // colgar de la raíz de la escena). Línea central en mundo = su transform vivo
            // aplicado a los puntos de la ruta (mismo truco que usa el auto).
            var route = MapLayout.PavedRoute;
            var rx = new System.Text.RegularExpressions.Regex(@"^PavedRoad_Surface \(\d+\)$");
            var pieces = new List<Transform>();
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rx.IsMatch(tr.name)) pieces.Add(tr);

            var center = new List<Vector3>();
            foreach (var piece in pieces)
                for (int i = 0; i < route.Length; i++)
                    center.Add(piece.TransformPoint(new Vector3(route[i].x, RoadY, route[i].y)));

            // Corredor libre de árboles = la línea central de TODO el asfalto (base + extensiones),
            // no solo la extensión: al estirar el terreno hacia el mapa (abajo) puede tapar parte
            // del asfalto BASE, y no queremos árboles encima de esa ruta. Solo para despejar
            // árboles; el rectángulo (AABB) se calcula solo con las extensiones (center).
            var clearLine = new List<Vector3>(center);
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tr.name == "PavedRoad_Surface")
                    for (int i = 0; i < route.Length; i++)
                        clearLine.Add(tr.TransformPoint(new Vector3(route[i].x, RoadY, route[i].y)));

            Debug.Log($"<color=cyan>[RoadTerrain] extensiones de ruta encontradas: {pieces.Count} " +
                      $"(nombres: {(pieces.Count > 0 ? string.Join(", ", pieces.ConvertAll(p => p.name)) : "ninguna")}).</color>");
            if (center.Count < 2)
            {
                Debug.LogWarning("[RoadTerrain] no encontré ninguna 'PavedRoad_Surface (N)' → no armo terreno. " +
                                 "¿La extensión existe y se llama así? (necesita el sufijo ' (1)').");
                return;
            }

            // AABB (en XZ) de la línea central + margen. Altura mínima de la ruta para apoyar
            // el terreno un poco por debajo (la ruta queda arriba, sobre una leve banquina).
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue;
            foreach (var c in center)
            {
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
                minY = Mathf.Min(minY, c.y);
            }
            minX -= Margin; maxX += Margin; minZ -= Margin; maxZ += Margin;

            // Cerrar el HUECO con el terreno principal: si la ruta arranca más allá del borde
            // ESTE del terreno principal, estiramos este terreno hacia el mapa hasta solaparlo
            // un poco (si no, el tramo de ruta entre ambos terrenos queda flotando). Buscamos
            // el terreno principal (el de mayor área que no sea éste) y llevamos minX hasta su
            // borde. Su rango de Z ya cubre la ruta ahí, así que con estirar en X alcanza.
            Terrain main = null; float bestArea = 0f;
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name == TerrainName || t.terrainData == null) continue;
                var sz = t.terrainData.size; float area = sz.x * sz.z;
                if (area > bestArea) { bestArea = area; main = t; }
            }
            if (main != null)
            {
                float mainMaxX = main.transform.position.x + main.terrainData.size.x;
                minX = Mathf.Min(minX, mainMaxX - 5f); // solaparse ~5m con el principal
            }

            float width = maxX - minX, length = maxZ - minZ;

            // ── TerrainData plano a la altura de la ruta ──
            AssetDatabase.DeleteAsset(AssetPath);
            var td = new TerrainData { heightmapResolution = 513 };
            td.size = new Vector3(width, 30f, length); // alturas quedan en 0 = plano

            // Capas de textura: COPIADAS del terreno principal (las mismas que ya se ven bien),
            // en vez de cargar de una ruta fija que puede estar movida (por eso salía BLANCO).
            // La capa 0 del terreno generado es el pasto (ver TerrainBuilder). Pintamos el
            // alphamap 100% a esa capa: si una capa está asignada pero con peso 0, no renderiza.
            TerrainLayer[] mainLayers = (main != null && main.terrainData != null) ? main.terrainData.terrainLayers : null;
            if (mainLayers == null || mainLayers.Length == 0)
            {
                var g = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GrassLayerPath); // fallback
                if (g != null) mainLayers = new[] { g };
            }
            if (mainLayers != null && mainLayers.Length > 0)
            {
                td.terrainLayers = mainLayers;
                td.alphamapResolution = 256;
                int nl = mainLayers.Length;
                var alpha = new float[td.alphamapHeight, td.alphamapWidth, nl];
                for (int y = 0; y < td.alphamapHeight; y++)
                    for (int x = 0; x < td.alphamapWidth; x++)
                        alpha[y, x, 0] = 1f; // todo capa 0 (pasto)
                td.SetAlphamaps(0, 0, alpha);
            }

            // Árboles: usamos los MISMOS prototipos que el terreno principal (los que de verdad
            // renderizan bien en este proyecto — pueden ser PSX/StarkCrafts o conífera según el
            // pool activo). Cargar pinos por ruta fija fallaba porque quizás no es el árbol en
            // uso (por eso "no se ven"). Fallback: los Conifers [BOTD]. Reparto en grilla
            // dejando LIBRE el corredor de la ruta.
            // owner: "solamente pinos quiero". Los Conifers [BOTD] NO renderizan en este
            // proyecto (billboards blancos). Los pinos que SÍ funcionan son los del terreno
            // principal: PSX_Tree1 / PSX_Tree4 (los Tree2/Tree3 son frondosos). Filtramos por
            // nombre los prototipos del principal y nos quedamos SOLO con los pinos.
            var pool = new List<TreePrototype>();
            if (main != null && main.terrainData != null && main.terrainData.treePrototypes != null)
            {
                foreach (var pt in main.terrainData.treePrototypes)
                {
                    if (pt.prefab == null) continue;
                    string n = pt.prefab.name.ToLowerInvariant();
                    bool isPine = n.Contains("tree1") || n.Contains("tree4") || n.Contains("pine") ||
                                  n.Contains("conifer") || n.Contains("pino") || n.Contains("fir") || n.Contains("abeto");
                    if (isPine) pool.Add(pt);
                }
                if (pool.Count == 0) pool.AddRange(main.terrainData.treePrototypes); // no reconocí pinos → uso todos
            }
            if (pool.Count == 0) // último recurso: Conifers [BOTD]
            {
                var conifers = LoadConifers();
                if (conifers != null) foreach (var c in conifers) pool.Add(new TreePrototype { prefab = c, bendFactor = 0f });
            }
            // Agrandar cada pino → prototipo "BigPine_..." (prefab escalado ×PineScale). Así,
            // al pintarlos a mano con el pincel (tope 2×) igual salen grandes, y sirve de marca
            // de que el terreno ya está "committeado" (ver arriba, PERMANENTE vs REHACER).
            var bigPool = new List<TreePrototype>();
            foreach (var p in pool) bigPool.Add(MakeBigPine(p));
            pool = bigPool;

            if (pool.Count > 0)
            {
                td.treePrototypes = pool.ToArray();
                td.SetTreeInstances(ScatterTrees(clearLine, minX, minZ, width, length, pool.Count).ToArray(), true);
                string usados = string.Join(", ", pool.ConvertAll(p => p.prefab != null ? p.prefab.name : "null"));
                Debug.Log($"<color=cyan>[RoadTerrain] pinos usados: {pool.Count} ({usados}).</color>");
            }
            else Debug.LogWarning("[RoadTerrain] no hay prototipos de árbol → terreno sin bosque.");

            AssetDatabase.CreateAsset(td, AssetPath);

            // ── GameObject del terreno ──
            var go = Terrain.CreateTerrainGameObject(td); // ya trae Terrain + TerrainCollider
            go.name = TerrainName;
            go.transform.SetParent(mapRoot);
            go.transform.position = new Vector3(minX, minY - 0.4f, minZ); // superficie ~0.4m bajo la ruta

            var terr = go.GetComponent<Terrain>();
            terr.allowAutoConnect = false; // distinto tamaño/resolución que el principal → no intentar coser (evita el warning)
            // Material + distancias de árboles copiadas del terreno PRINCIPAL, para que se vea
            // igual (mismo shader URP + mismo alcance de render de árboles).
            if (main != null)
            {
                if (main.materialTemplate != null) terr.materialTemplate = main.materialTemplate;
                terr.treeDistance = main.treeDistance;
                terr.treeBillboardDistance = main.treeBillboardDistance;
                terr.detailObjectDistance = main.detailObjectDistance;
                terr.drawTreesAndFoliage = true;
            }
            else
            {
                terr.treeDistance = MapLayout.TreeRenderDistance;
                terr.treeBillboardDistance = MapLayout.TreeBillboardDistance;
            }

            EnsureGrass(terr, main); // pasto 3D como el terreno principal

            Debug.Log($"<color=lime>[RoadTerrain] Terreno de extensión {width:0}x{length:0}m, " +
                      $"{td.treeInstanceCount} árboles (prototipos usados: {(td.treePrototypes != null ? td.treePrototypes.Length : 0)}).</color>");
        }

        // Árboles en grilla sobre TODO el rectángulo, salteando el corredor de la ruta
        // (así quedan a los dos lados). Posición normalizada (0..1) que pide el TerrainData.
        static List<TreeInstance> ScatterTrees(List<Vector3> center, float minX, float minZ, float width, float length, int protoCount)
        {
            var poly = new Vector2[center.Count];
            for (int i = 0; i < center.Count; i++) poly[i] = new Vector2(center[i].x, center[i].z);

            var trees = new List<TreeInstance>();
            float jitter = TreeStep * 0.35f;
            for (float x = minX + 3f; x < minX + width - 3f; x += TreeStep)
                for (float z = minZ + 3f; z < minZ + length - 3f; z += TreeStep)
                {
                    var p = new Vector2(x + Random.Range(-jitter, jitter), z + Random.Range(-jitter, jitter));
                    if (BuilderUtils.DistToPolyline(p, poly) < RoadClearHalf) continue; // libre la ruta
                    float nx = (p.x - minX) / width, nz = (p.y - minZ) / length;
                    if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                    float s = Random.Range(TreeScaleMin, TreeScaleMax), tint = Random.Range(0.75f, 1f);
                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(nx, 0f, nz),
                        prototypeIndex = Random.Range(0, protoCount),
                        heightScale = s,
                        widthScale = s * Random.Range(0.85f, 1.15f),
                        rotation = Random.Range(0f, Mathf.PI * 2f),
                        color = new Color(tint, tint, tint),
                        lightmapColor = Color.white
                    });
                }
            return trees;
        }

        // Crea (o rehace) un prefab "BigPine_<nombre>" = el pino original escalado ×PineScale,
        // en una carpeta versionada. Lo desempaqueta (standalone, no variante) para que no
        // dependa del prefab base (que puede vivir en Generated/, ignorada). Devuelve el
        // prototipo apuntando a ese prefab grande.
        static TreePrototype MakeBigPine(TreePrototype src)
        {
            if (src.prefab == null) return src;
            string safe = src.prefab.name;
            string path = "Assets/_FolkloreArchives/ExtraTerrains/BigPine_" + safe + ".prefab";
            GameObject inst = null;
            try
            {
                // Si el prefab grande YA existe (de una generación anterior), reusarlo — así NO
                // creamos prefabs en medio del Generate cada vez (eso es lo que puede fallar).
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) return new TreePrototype { prefab = existing, bendFactor = src.bendFactor };

                inst = (GameObject)PrefabUtility.InstantiatePrefab(src.prefab);
                if (inst == null) return src;
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = "BigPine_" + safe;                 // el nombre marca "committeado"
                inst.transform.localScale = Vector3.one * PineScale;
                var prefab = PrefabUtility.SaveAsPrefabAsset(inst, path);
                Object.DestroyImmediate(inst); inst = null;
                return prefab != null ? new TreePrototype { prefab = prefab, bendFactor = src.bendFactor } : src;
            }
            catch (System.Exception e)
            {
                // Si crear el prefab grande falla (crear assets a mitad del Generate es flaky),
                // NO rompemos el terreno: usamos el pino normal. El terreno igual se arma.
                Debug.LogWarning("[RoadTerrain] no pude crear el pino grande (" + safe + "), uso el normal: " + e.Message);
                if (inst != null) Object.DestroyImmediate(inst);
                return src;
            }
        }

        // Agrega pasto 3D al terreno COPIANDO los prototipos de detalle del terreno principal
        // (los que ya renderizan bien) y pintando la capa 0 (pasto verde). Idempotente: si el
        // terreno YA tiene detalle, no hace nada (así no pisa lo que el owner haya pintado a
        // mano). NO toca alturas ni árboles → seguro de llamar sobre el terreno permanente.
        const int   GrassDetailRes    = 512;  // resolución de la grilla de pasto
        const int   GrassDensity      = 14;   // densidad por celda (más = más tupido)
        const float GrassObjectDensity = 1f;  // multiplicador del terreno (1 = máximo, pasto lleno)
        static void EnsureGrass(Terrain terr, Terrain main)
        {
            if (terr == null || terr.terrainData == null) return;
            var td = terr.terrainData;
            // SIEMPRE igualar las distancias de render (árboles + pasto) a las del terreno
            // principal — aunque el pasto ya esté y saltemos el repintado (rama de reuso). Así
            // los árboles del terreno nuevo se ven de tan lejos como los del principal.
            // owner: "aumentá la distancia de render de los árboles, está muy baja".
            if (main != null)
            {
                terr.treeDistance = main.treeDistance;
                terr.treeBillboardDistance = main.treeBillboardDistance;
                terr.detailObjectDistance = main.detailObjectDistance;
                terr.drawTreesAndFoliage = true;
            }
            // IDEMPOTENTE: si el terreno YA tiene pasto (prototipos de detalle), NO lo re-pinto →
            // respeta lo que el owner borró/pintó a mano (ej: sacar el pasto de la ruta). Solo lo
            // arma la PRIMERA vez (terreno sin detalle). Para re-armarlo desde cero: borrar el
            // asset RoadExtensionTerrain.asset y regenerar.
            if (td.detailPrototypes != null && td.detailPrototypes.Length > 0)
            {
                Debug.Log("<color=cyan>[RoadTerrain] el terreno ya tiene pasto — respeto lo pintado/borrado a mano.</color>");
                return;
            }
            if (main == null || main.terrainData == null)
            { Debug.LogWarning("[RoadTerrain] no encontré el terreno principal para copiar el pasto."); return; }
            var mainProtos = main.terrainData.detailPrototypes;
            if (mainProtos == null || mainProtos.Length == 0)
            { Debug.LogWarning("[RoadTerrain] el terreno principal '" + main.name + "' NO tiene pasto (detailPrototypes vacío) → no hay de dónde copiarlo. Aviso para hacer el pasto propio."); return; }

            // El pasto NO es cuestión de densidad total (el principal tiene ~5 inst/m² y se ve
            // denso; el mío con lo mismo se veía ralo). La diferencia es la MEZCLA: el principal
            // reparte el pasto en VARIAS capas (corto + ALTO + arbustos) y el look lush viene de
            // las capas de pasto alto; yo pintaba TODO en la capa 0 (el chato). Fix: pintar CADA
            // capa con la misma densidad que el principal en sus zonas con pasto. Además, res
            // pareja (celdas ~0.7m) para que no salga en hileras.
            var mtd = main.terrainData;
            double myArea = td.size.x * (double)td.size.z;
            double mainCellsPerM2 = (double)mtd.detailResolution * mtd.detailResolution / (mtd.size.x * (double)mtd.size.z);

            td.detailPrototypes = mainProtos;
            float longDim = Mathf.Max(td.size.x, td.size.z);
            int myRes = Mathf.Clamp(Mathf.RoundToInt(longDim / 0.7f), 512, 1024);
            myRes -= myRes % 16;
            td.SetDetailResolution(myRes, 16);
            // CLAVE que faltaba: copiar el MODO DE SCATTER del principal. En CoverageMode el valor
            // por celda es COBERTURA (independiente de la resolución); en InstanceCountMode es un
            // CONTEO. Con modos distintos, los mismos valores se dibujan totalmente distinto.
            var scatter = mtd.detailScatterMode;
            try { td.SetDetailScatterMode(scatter); } catch (System.Exception e) { Debug.LogWarning("[RoadTerrain] no pude copiar detailScatterMode: " + e.Message); }
            bool coverage = scatter == DetailScatterMode.CoverageMode;
            int res = td.detailResolution;
            double myCellsPerM2 = (double)res * res / myArea;

            var sb = new System.Text.StringBuilder("[RoadTerrain] PASTO por capa (mío vs principal), res " + res +
                                                   " (celdas ~" + (longDim / res).ToString("0.00") + "m), scatter " + scatter + ":\n");
            for (int L = 0; L < mainProtos.Length; L++)
            {
                double avgL = 0;
                try
                {
                    var md = mtd.GetDetailLayer(0, 0, mtd.detailWidth, mtd.detailHeight, L);
                    long s = 0, n = 0; foreach (int v in md) if (v > 0) { s += v; n++; }
                    avgL = n > 0 ? (double)s / n : 0;
                }
                catch { }
                // Coverage: mismo valor por celda (es cobertura, no depende de la res). Count:
                // escalar por celdas/m² para igualar el pasto por m².
                int myPer = coverage
                    ? Mathf.Clamp(Mathf.RoundToInt((float)avgL), 0, 255)
                    : Mathf.Clamp(Mathf.RoundToInt((float)(avgL * mainCellsPerM2 / myCellsPerM2)), 0, 250);
                var map = new int[res, res];
                if (myPer > 0) for (int y = 0; y < res; y++) for (int x = 0; x < res; x++) map[y, x] = myPer;
                td.SetDetailLayer(0, 0, L, map);
                var pp = mainProtos[L];
                string pn = pp.usePrototypeMesh ? (pp.prototype != null ? pp.prototype.name : "mesh?")
                                                : (pp.prototypeTexture != null ? pp.prototypeTexture.name : "tex?");
                sb.AppendLine($"  capa {L} '{pn}' (mode {pp.renderMode}, h {pp.minHeight:0.0}-{pp.maxHeight:0.0}): " +
                              $"principal avg {avgL:0.0} → mío {myPer}/celda");
            }

            terr.detailObjectDistance = Mathf.Max(main.detailObjectDistance, 150f);
            terr.detailObjectDensity = main.detailObjectDensity;
            terr.drawTreesAndFoliage = true;
            terr.Flush();
            var reassign = terr.terrainData; terr.terrainData = null; terr.terrainData = reassign; // refresh
            terr.Flush();
            EditorUtility.SetDirty(td);
            sb.AppendLine($"  detailObjDensity {terr.detailObjectDensity:0.00}, distancia {terr.detailObjectDistance:0}, principal detailObjDensity {main.detailObjectDensity:0.00}, detailRes {mtd.detailResolution}");
            Debug.Log("<color=cyan>" + sb + "</color>");
        }

        // El terreno principal = el de mayor área que no sea éste.
        static Terrain FindMainTerrain()
        {
            Terrain main = null; float bestArea = 0f;
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name == TerrainName || t.terrainData == null) continue;
                var sz = t.terrainData.size; float area = sz.x * sz.z;
                if (area > bestArea) { bestArea = area; main = t; }
            }
            return main;
        }

        static GameObject[] LoadConifers()
        {
            string[] names = { "PF Conifer Tall BOTD URP", "PF Conifer Medium BOTD URP", "PF Conifer Small BOTD URP", "PF Conifer Bare BOTD URP" };
            var list = new List<GameObject>();
            foreach (var n in names)
            {
                var g = AssetDatabase.LoadAssetAtPath<GameObject>(ConiferDir + n + ".prefab");
                if (g != null) list.Add(g);
            }
            if (list.Count == 0) Debug.LogWarning("[RoadTerrain] no encontré los pinos Conifers [BOTD] en " + ConiferDir);
            return list.Count > 0 ? list.ToArray() : null;
        }
    }
}
