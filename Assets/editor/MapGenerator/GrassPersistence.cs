// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  GrassPersistence.cs — hace que el BORRADO manual de pasto (pincel
//  Paint Details + Shift) sobreviva al Generate, integrado a "Save
//  Terrain Paint".
//
//  POR QUÉ NO ALCANZABA el diff contra un baseline recalculado
//  (TerrainPaintPersistence.SaveDetailDiff): `SetupGrass` reparte el
//  pasto con Random NO determinístico → recalcular el baseline da el
//  pasto en OTRAS celdas que el real, así que el diff capturaba RUIDO
//  (celdas con pasto), y al re-aplicarlo REINTRODUCÍA pasto sobre lo
//  borrado. Por eso el pasto "volvía".
//
//  SOLUCIÓN (igual que TreePersistence): el baseline es el pasto REAL
//  recién generado (capturado en ForestBuilder). Se guardan solo las
//  celdas que el owner REDUJO (live < baseline). Al aplicar, esas
//  celdas se bajan a su valor guardado → nunca AGREGA pasto, solo
//  reduce → no puede reintroducir lo borrado.
//
//  Flujo:
//   - ForestBuilder (tras SetupGrass): CaptureBaseline(td) + ApplyRemovals(td).
//   - Save Terrain Paint: SaveRemovals(td) = celdas donde live < baseline.
//   - Clear Terrain Paint: Clear().
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class GrassPersistence
    {
        // datos del owner (persistente): qué celdas de pasto redujo/borró a mano.
        const string RemovalsPath = "Assets/_FolkloreArchives/grass_removals.bytes";
        // cache descartable: el pasto procedural completo del último rebuild.
        static string BaselinePath => MapLayout.GeneratedFolder + "/grass_baseline.bytes";

        // Guarda el pasto COMPLETO recién generado (todas las capas) como baseline.
        // Llamar en ForestBuilder justo después de SetupGrass (antes de reducir).
        public static void CaptureBaseline(TerrainData td)
        {
            int res = td.detailResolution;
            int layers = td.detailPrototypes.Length;
            using (var bw = new BinaryWriter(File.Create(BaselinePath)))
            {
                bw.Write(res);
                bw.Write(layers);
                for (int l = 0; l < layers; l++)
                {
                    var m = td.GetDetailLayer(0, 0, res, res, l);
                    for (int z = 0; z < res; z++)
                        for (int x = 0; x < res; x++)
                            bw.Write((byte)Mathf.Clamp(m[z, x], 0, 255));
                }
            }
        }

        // Baja las celdas guardadas a su valor reducido (nunca sube pasto).
        public static void ApplyRemovals(TerrainData td)
        {
            if (!File.Exists(RemovalsPath)) return;
            int res = td.detailResolution, curLayers = td.detailPrototypes.Length;
            var byLayer = new Dictionary<int, List<(int z, int x, int v)>>();
            using (var br = new BinaryReader(File.OpenRead(RemovalsPath)))
            {
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    int l = br.ReadInt32(), z = br.ReadInt32(), x = br.ReadInt32(), v = br.ReadInt32();
                    if (!byLayer.TryGetValue(l, out var lst)) { lst = new List<(int, int, int)>(); byLayer[l] = lst; }
                    lst.Add((z, x, v));
                }
            }
            int applied = 0;
            foreach (var kv in byLayer)
            {
                int l = kv.Key;
                if (l >= curLayers) continue;
                var layer = td.GetDetailLayer(0, 0, res, res, l);
                foreach (var c in kv.Value)
                    if (c.z < res && c.x < res) { layer[c.z, c.x] = c.v; applied++; }
                td.SetDetailLayer(0, 0, l, layer);
            }
            if (applied > 0) Debug.Log($"[GrassPersist] {applied} celdas de pasto borradas re-aplicadas.");
        }

        // Guarda las celdas donde el pasto ACTUAL es MENOR que el baseline (= borrado a
        // mano). Devuelve cuántas, -1 si falta baseline, -2 si no coincide la resolución.
        public static int SaveRemovals(TerrainData live)
        {
            if (!File.Exists(BaselinePath)) return -1;
            int res, layers;
            byte[][] baseLayers;
            using (var br = new BinaryReader(File.OpenRead(BaselinePath)))
            {
                res = br.ReadInt32();
                layers = br.ReadInt32();
                if (res != live.detailResolution) return -2;
                baseLayers = new byte[layers][];
                for (int l = 0; l < layers; l++) baseLayers[l] = br.ReadBytes(res * res);
            }
            int curLayers = live.detailPrototypes.Length;
            var removals = new List<(int l, int z, int x, int v)>();
            for (int l = 0; l < layers && l < curLayers; l++)
            {
                var liveL = live.GetDetailLayer(0, 0, res, res, l);
                var bl = baseLayers[l];
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                    {
                        int bv = bl[z * res + x];
                        int lv = liveL[z, x];
                        if (lv < bv) removals.Add((l, z, x, lv)); // el owner redujo/borró acá
                    }
            }
            using (var bw = new BinaryWriter(File.Create(RemovalsPath)))
            {
                bw.Write(removals.Count);
                foreach (var r in removals) { bw.Write(r.l); bw.Write(r.z); bw.Write(r.x); bw.Write(r.v); }
            }
            return removals.Count;
        }

        public static void Clear()
        {
            if (File.Exists(RemovalsPath)) File.Delete(RemovalsPath);
        }
    }
}
