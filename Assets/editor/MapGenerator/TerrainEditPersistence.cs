// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TerrainEditPersistence.cs — lets the owner's manual terrain
//  edits (Smooth Height brush, raise/lower, etc.) survive a full
//  map regenerate.
//
//  The terrain heightmap is fully recomputed from HeightAt() every
//  time the map is generated, which wipes any hand-painting. This
//  script snapshots the DIFFERENCE between the current (hand-edited)
//  heightmap and the pure procedural one, saves it to a file, and
//  re-applies that difference after each regenerate.
//
//  Workflow:
//    1. Edit the terrain in-editor (Smooth Height, etc.).
//    2. Tools > Folklore Archives > Save Terrain Edits   ← click this.
//    3. Regenerate the map whenever — the edits come back automatically.
//    (Re-run step 2 after any new terrain painting to capture it.)
//
//  Stored as a diff (not the whole heightmap) so:
//    • cells you never touched stay 0 → procedural terrain still wins
//      there (road-position changes etc. keep working),
//    • only the cells you actually smoothed carry a correction.
// ============================================================
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class TerrainEditPersistence
    {
        // Kept OUTSIDE the Generated folder (which holds regenerated throwaway
        // assets) so it is clearly persistent, owner-authored data.
        const string EditsPath = "Assets/_FolkloreArchives/terrain_edits.bytes";

        // Diffs smaller than this (in normalised 0..1 height) are treated as zero,
        // to avoid re-introducing 16-bit heightmap quantisation noise everywhere.
        // 1e-5 * MaxHeight(60m) ≈ 0.6 mm — well below anything visible.
        const float DiffEpsilon = 1e-5f;

        // ── Save (menu) ───────────────────────────────────────────────────────
        [MenuItem("Tools/Folklore Archives/Save Terrain Edits")]
        public static void SaveTerrainEdits()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                var go = GameObject.Find("Terrain");
                if (go != null) terrain = go.GetComponent<Terrain>();
            }
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("[TerrainEdits] No active Terrain found. Generate the map first.");
                return;
            }

            var td  = terrain.terrainData;
            int res = td.heightmapResolution;

            float[,] actual = td.GetHeights(0, 0, res, res);       // hand-edited heightmap
            float[,] proc   = TerrainBuilder.ComputeProceduralHeights(res); // pure procedural base

            int edited = 0;
            using (var fs = new FileStream(EditsPath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(res);
                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float d = actual[z, x] - proc[z, x];
                        if (Mathf.Abs(d) < DiffEpsilon) d = 0f;
                        else edited++;
                        bw.Write(d);
                    }
                }
            }

            AssetDatabase.Refresh();
            float pct = 100f * edited / (res * (float)res);
            Debug.Log($"<color=lime>[TerrainEdits] Saved {edited} edited cells " +
                      $"({pct:F2}% of terrain) to {EditsPath}. " +
                      $"These now survive Generate Greybox Map.</color>");
        }

        // ── Apply (called from TerrainBuilder.Build) ──────────────────────────
        // Adds the saved diff onto the freshly-computed procedural heightmap,
        // in place. Silent no-op if there is no edits file or the resolution
        // changed (e.g. heightmapResolution was altered).
        public static void ApplyTerrainEdits(float[,] h, int res)
        {
            if (!File.Exists(EditsPath)) return;

            try
            {
                using (var fs = new FileStream(EditsPath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    int savedRes = br.ReadInt32();
                    if (savedRes != res)
                    {
                        Debug.LogWarning($"[TerrainEdits] Saved edits are {savedRes}px but terrain " +
                                         $"is now {res}px — skipping. Re-save terrain edits to fix.");
                        return;
                    }

                    // Radio alrededor del lago ACTUAL a ignorar: el lago se movió/cambió de
                    // forma varias veces desde que se guardó este archivo (owner: "ahora
                    // yendo al lago esta asi todo levantado destruido" -- el diff viejo
                    // quedaba desalineado contra la base nueva ahí). Fuera de este radio
                    // (corral, galpón, etc.) el diff sigue aplicando normal.
                    float lakeExcludeR = MapLayout.CentralLakeRadius + MapLayout.CentralLakeShore + 20f;
                    int applied = 0, skippedNearLake = 0;
                    for (int z = 0; z < res; z++)
                    {
                        float wz = z / (float)(res - 1) * MapLayout.MapSize;
                        for (int x = 0; x < res; x++)
                        {
                            float d = br.ReadSingle();
                            if (d == 0f) continue;
                            float wx = x / (float)(res - 1) * MapLayout.MapSizeX;
                            if (MapLayout.LakeDist(new Vector2(wx, wz)) < lakeExcludeR) { skippedNearLake++; continue; }
                            h[z, x] = Mathf.Clamp01(h[z, x] + d);
                            applied++;
                        }
                    }
                    if (applied > 0 || skippedNearLake > 0)
                        Debug.Log($"[TerrainEdits] Re-applied {applied} saved terrain edits ({skippedNearLake} cerca del lago actual ignoradas -- base cambió).");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TerrainEdits] Failed to read " + EditsPath + ": " + e.Message);
            }
        }
    }
}
