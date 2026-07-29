// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TerrainPaintPersistence.cs — owner: "agrega un guardado real"
//  para texturas/pasto pintados a mano (TerrainEditPersistence ya
//  cubre el heightmap; esto hace lo mismo para el splat y las capas
//  de detalle). Mismo criterio: guarda solo la DIFERENCIA contra lo
//  puramente procedural, así el código (nuevos caminos/POIs) sigue
//  ganando donde nunca pintaste nada a mano.
//
//  Alphamap (splat, capas 0-8): se guarda el VECTOR COMPLETO de pesos
//  por celda tocada, no un diff por capa — las capas de un terreno
//  tienen que sumar 1 entre sí, y sumar un diff por separado podría
//  romper esa normalización. Guardar el vector final (ya válido, tal
//  cual estaba pintado) evita el problema.
//  Detail layers (pasto, una capa por prototipo de pasto): SÍ es un
//  diff aditivo simple, como el heightmap — cada capa es independiente
//  (cantidad de instancias 0-16 por celda), no hay que normalizar
//  entre capas.
//
//  Ambos formatos son DISPERSOS (solo las celdas que en verdad
//  difieren de lo procedural) — en denso, el mapa entero pesaría
//  decenas de MB.
//
//  Save recomputa el baseline puramente procedural desde cero (crea un
//  TerrainData temporal en memoria, nunca se guarda como asset) para
//  poder comparar — esto puede tardar varios minutos (mismo costo que
//  Rebuild Terrain), así que es una acción deliberada, no algo que
//  corra en cada Generate.
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class TerrainPaintPersistence
    {
        const string AlphaPath  = "Assets/_FolkloreArchives/terrain_paint_alpha.bytes";
        const string DetailPath = "Assets/_FolkloreArchives/terrain_paint_detail.bytes";
        const float AlphaEpsilon = 1e-4f; // distancia (al cuadrado) mínima para contar una celda como "tocada"

        [MenuItem("Tools/Folklore Archives/Save Terrain Paint")]
        public static void SaveTerrainPaint()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                var go = GameObject.Find("Terrain");
                if (go != null) terrain = go.GetComponent<Terrain>();
            }
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("[TerrainPaint] No hay Terrain activo. Generá el mapa primero.");
                return;
            }
            var live = terrain.terrainData;

            Debug.Log("<color=yellow>[TerrainPaint] Recalculando el terreno puramente procedural para comparar (puede tardar varios minutos, mismo costo que Rebuild Terrain)…</color>");

            var baseline = new TerrainData();
            baseline.heightmapResolution = live.heightmapResolution;
            baseline.alphamapResolution  = live.alphamapResolution;
            baseline.size = live.size;
            baseline.SetHeights(0, 0, TerrainBuilder.ComputeProceduralHeights(baseline.heightmapResolution));
            TerrainBuilder.PaintTextures(baseline);
            ForestBuilder.SetupGrass(baseline);

            SaveAlphaDiff(live, baseline);
            SaveDetailDiff(live, baseline);

            Object.DestroyImmediate(baseline);

            // Árboles borrados a mano (pincel Paint Trees + Shift). Se guarda como diff
            // contra el baseline procedural que ForestBuilder cachea en cada Generate.
            int treeRem = TreePersistence.SaveTreeRemovals(live);
            string treeMsg = treeRem < 0
                ? " (árboles: falta el baseline — regenerá el mapa una vez y volvé a guardar)"
                : $" + {treeRem} árboles borrados";

            AssetDatabase.Refresh();
            Debug.Log("<color=lime>[TerrainPaint] Pintado a mano (texturas + pasto" + treeMsg +
                      ") guardado. Sobrevive a Rebuild Terrain/Rebuild Forest de ahora en más.</color>");
        }

        static void SaveAlphaDiff(TerrainData live, TerrainData baseline)
        {
            int res = live.alphamapResolution;
            int layers = live.alphamapLayers;
            var liveMap = live.GetAlphamaps(0, 0, res, res);
            var baseMap = baseline.GetAlphamaps(0, 0, res, res);

            var changed = new List<(int z, int x, float[] w)>();
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float distSq = 0f;
                    for (int l = 0; l < layers; l++)
                    {
                        float d = liveMap[z, x, l] - baseMap[z, x, l];
                        distSq += d * d;
                    }
                    if (distSq < AlphaEpsilon * AlphaEpsilon) continue;
                    var w = new float[layers];
                    for (int l = 0; l < layers; l++) w[l] = liveMap[z, x, l];
                    changed.Add((z, x, w));
                }
            }

            using (var fs = new FileStream(AlphaPath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(res);
                bw.Write(layers);
                bw.Write(changed.Count);
                foreach (var c in changed)
                {
                    bw.Write(c.z); bw.Write(c.x);
                    for (int l = 0; l < layers; l++) bw.Write(c.w[l]);
                }
            }
            float pct = 100f * changed.Count / (res * (float)res);
            Debug.Log($"[TerrainPaint] {changed.Count} celdas de textura ({pct:F2}%) guardadas en {AlphaPath}.");
        }

        static void SaveDetailDiff(TerrainData live, TerrainData baseline)
        {
            int res = live.detailResolution;
            int layerCount = live.detailPrototypes.Length;
            if (layerCount != baseline.detailPrototypes.Length)
            {
                Debug.LogWarning("[TerrainPaint] La cantidad de capas de pasto del terreno actual no coincide con la recién calculada (¿cambiaste qué prototipos usa el código sin correr Rebuild Forest primero?) — no puedo comparar el pasto esta vez. Se guardaron igual las texturas.");
                if (File.Exists(DetailPath)) File.Delete(DetailPath);
                return;
            }

            using (var fs = new FileStream(DetailPath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(res);
                bw.Write(layerCount);
                int totalChanged = 0;
                for (int l = 0; l < layerCount; l++)
                {
                    var liveLayer = live.GetDetailLayer(0, 0, res, res, l);
                    var baseLayer = baseline.GetDetailLayer(0, 0, res, res, l);
                    var changed = new List<(int z, int x, int v)>();
                    for (int z = 0; z < res; z++)
                        for (int x = 0; x < res; x++)
                            if (liveLayer[z, x] != baseLayer[z, x])
                                changed.Add((z, x, liveLayer[z, x]));

                    bw.Write(changed.Count);
                    foreach (var c in changed) { bw.Write(c.z); bw.Write(c.x); bw.Write(c.v); }
                    totalChanged += changed.Count;
                }
                Debug.Log($"[TerrainPaint] {totalChanged} celdas de pasto (entre {layerCount} capas) guardadas en {DetailPath}.");
            }
        }

        // ── Apply (llamado desde TerrainBuilder.Build / ForestBuilder.Build) ──────

        public static void ApplyAlphaPaint(TerrainData td)
        {
            if (!File.Exists(AlphaPath)) return;
            try
            {
                using (var fs = new FileStream(AlphaPath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    int res = br.ReadInt32();
                    int layers = br.ReadInt32();
                    int count = br.ReadInt32();
                    if (res != td.alphamapResolution || layers != td.alphamapLayers)
                    {
                        Debug.LogWarning("[TerrainPaint] El guardado de texturas no coincide en resolución/capas con el terreno actual — lo salteo. Volvé a correr Save Terrain Paint.");
                        return;
                    }
                    var map = td.GetAlphamaps(0, 0, res, res);
                    // ídem TerrainEditPersistence: ignorar celdas cerca del lago ACTUAL --
                    // el guardado quedó grabado contra una posición/forma vieja del lago
                    // (owner: "ahora yendo al lago esta asi todo levantado destruido").
                    float lakeExcludeR = MapLayout.CentralLakeRadius + MapLayout.CentralLakeShore + 20f;
                    int applied = 0, skippedNearLake = 0;
                    for (int i = 0; i < count; i++)
                    {
                        int z = br.ReadInt32(), x = br.ReadInt32();
                        var vals = new float[layers];
                        for (int l = 0; l < layers; l++) vals[l] = br.ReadSingle();
                        float wx = x / (float)res * MapLayout.MapSizeX;
                        float wz = z / (float)res * MapLayout.MapSize;
                        if (MapLayout.LakeDist(new Vector2(wx, wz)) < lakeExcludeR) { skippedNearLake++; continue; }
                        for (int l = 0; l < layers; l++) map[z, x, l] = vals[l];
                        applied++;
                    }
                    td.SetAlphamaps(0, 0, map);
                    if (applied > 0 || skippedNearLake > 0)
                        Debug.Log($"[TerrainPaint] Reaplicadas {applied} celdas de textura pintadas a mano ({skippedNearLake} cerca del lago actual ignoradas -- base cambió).");
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[TerrainPaint] No pude leer " + AlphaPath + ": " + e.Message); }
        }

        public static void ApplyDetailPaint(TerrainData td)
        {
            if (!File.Exists(DetailPath)) return;
            try
            {
                using (var fs = new FileStream(DetailPath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    int res = br.ReadInt32();
                    int layers = br.ReadInt32();
                    if (res != td.detailResolution || layers != td.detailPrototypes.Length)
                    {
                        Debug.LogWarning("[TerrainPaint] El guardado de pasto no coincide en resolución/capas con el terreno actual — lo salteo. Volvé a correr Save Terrain Paint.");
                        return;
                    }
                    // ídem ApplyAlphaPaint: ignorar celdas cerca del lago ACTUAL, el
                    // guardado quedó grabado contra una posición/forma vieja del lago.
                    float lakeExcludeR = MapLayout.CentralLakeRadius + MapLayout.CentralLakeShore + 20f;
                    int totalApplied = 0, totalSkipped = 0;
                    for (int l = 0; l < layers; l++)
                    {
                        int count = br.ReadInt32();
                        if (count == 0) continue;
                        var layer = td.GetDetailLayer(0, 0, res, res, l);
                        int appliedHere = 0;
                        for (int i = 0; i < count; i++)
                        {
                            int z = br.ReadInt32(), x = br.ReadInt32(), v = br.ReadInt32();
                            float wx = x / (float)res * MapLayout.MapSizeX;
                            float wz = z / (float)res * MapLayout.MapSize;
                            if (MapLayout.LakeDist(new Vector2(wx, wz)) < lakeExcludeR) { totalSkipped++; continue; }
                            layer[z, x] = v;
                            appliedHere++;
                        }
                        if (appliedHere > 0) td.SetDetailLayer(0, 0, l, layer);
                        totalApplied += appliedHere;
                    }
                    if (totalApplied > 0 || totalSkipped > 0)
                        Debug.Log($"[TerrainPaint] Reaplicadas {totalApplied} celdas de pasto pintado a mano ({totalSkipped} cerca del lago actual ignoradas -- base cambió).");
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[TerrainPaint] No pude leer " + DetailPath + ": " + e.Message); }
        }

        [MenuItem("Tools/Folklore Archives/Clear Terrain Paint")]
        public static void ClearTerrainPaint()
        {
            bool any = false;
            if (File.Exists(AlphaPath))  { File.Delete(AlphaPath);  any = true; }
            if (File.Exists(DetailPath)) { File.Delete(DetailPath); any = true; }
            TreePersistence.ClearRemovals(); // también el borrado de árboles
            if (any) { AssetDatabase.Refresh(); Debug.Log("[TerrainPaint] Guardado de texturas/pasto/árboles borrado."); }
            else { AssetDatabase.Refresh(); Debug.Log("[TerrainPaint] Guardado limpiado (texturas/pasto/árboles)."); }
        }
    }
}
