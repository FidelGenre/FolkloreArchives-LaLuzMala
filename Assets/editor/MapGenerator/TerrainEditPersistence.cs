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

        // ── (eliminado) menú "Save Terrain Edits" ─────────────────────────────
        //  Guardaba un DIFF de altura contra la base procedural. Frágil: si se guardaba
        //  con el terreno en mal estado, aplastaba todo al regenerar. Reemplazado por
        //  Tools ▸ Folklore Archives ▸ "Save Terrain" (guarda el asset directo, sin diff).
        //  ApplyTerrainEdits queda porque el build FRESCO (si alguna vez se regenera de
        //  cero) todavía lo usa para restaurar ediciones viejas.

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
