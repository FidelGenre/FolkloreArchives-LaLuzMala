// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FurniturePersistence.cs — lets the owner's manual edits to the
//  house furniture (move / rotate / scale / delete) survive a full
//  map regenerate.
//
//  HouseBuilder places furniture procedurally from HouseBuilder.
//  FurnitureItems every time the map is generated, which wipes any
//  hand-tuning. This script snapshots the current transform of each
//  furniture object (named "Furn_##_model", where ## = its stable ID =
//  its index in FurnitureItems) plus which IDs were deleted, saves that
//  to a JSON file, and HouseBuilder re-applies it on the next generate.
//
//  Workflow:
//    1. Generate the map.
//    2. Move / rotate / delete furniture in the Scene by hand.
//    3. Tools > Folklore Archives > Save Furniture Layout   ← click this.
//    4. Regenerate whenever — the layout comes back automatically.
//    (Re-click step 3 after any further edits to re-capture.)
//
//  NOTE: once saved, the JSON is AUTHORITATIVE for every furniture ID it
//  contains — the procedural positions in FurnitureItems are only used
//  for IDs NOT in the file (e.g. brand-new rows). To go back to fully
//  code-driven placement, use "Clear Furniture Layout" (deletes the file)
//  or re-save after moving things. If you REORDER/INSERT rows in
//  FurnitureItems the IDs shift and the saved file misaligns → re-save.
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class FurniturePersistence
    {
        // Kept OUTSIDE Generated/ (regenerated throwaway assets) so it is clearly
        // persistent, owner-authored data — same convention as terrain_edits.bytes.
        const string LayoutPath = "Assets/_FolkloreArchives/furniture_layout.json";
        const string GroupName  = "OldLadyHouse";

        [System.Serializable]
        public class FurnEntry
        {
            public int id;
            public bool deleted;
            public Vector3 pos, euler, scale;
        }

        [System.Serializable]
        class FurnLayout { public List<FurnEntry> entries = new List<FurnEntry>(); }

        // In-memory map loaded once per generate (HouseBuilder calls Load()).
        static Dictionary<int, FurnEntry> _map;

        // ── Load (called from HouseBuilder.BuildFurnitureKenney) ──────────────
        public static void Load()
        {
            _map = new Dictionary<int, FurnEntry>();
            if (!File.Exists(LayoutPath)) return;
            try
            {
                var layout = JsonUtility.FromJson<FurnLayout>(File.ReadAllText(LayoutPath));
                if (layout?.entries != null)
                    foreach (var e in layout.entries) _map[e.id] = e;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[FurnitureLayout] No se pudo leer " + LayoutPath + ": " + ex.Message);
            }
        }

        public static bool IsDeleted(int id) =>
            _map != null && _map.TryGetValue(id, out var e) && e.deleted;

        public static bool TryGetTransform(int id, out Vector3 pos, out Vector3 euler, out Vector3 scale)
        {
            pos = euler = scale = default;
            if (_map == null || !_map.TryGetValue(id, out var e) || e.deleted) return false;
            pos = e.pos; euler = e.euler; scale = e.scale;
            return true;
        }

        // ── Save (menu) ───────────────────────────────────────────────────────
        [MenuItem("Tools/Folklore Archives/Save Furniture Layout")]
        public static void SaveFurnitureLayout()
        {
            var groupGo = GameObject.Find(GroupName);
            if (groupGo == null)
            {
                Debug.LogWarning($"[FurnitureLayout] No encontré '{GroupName}' en la escena. Generá el mapa primero.");
                return;
            }
            var group = groupGo.transform;

            // Índice de los muebles presentes (por ID leído del nombre "Furn_##_...").
            var present = new Dictionary<int, Transform>();
            foreach (Transform child in group)
            {
                if (!child.name.StartsWith("Furn_")) continue;
                int us = child.name.IndexOf('_', 5);        // "Furn_" = 5 chars
                string idStr = us > 5 ? child.name.Substring(5, us - 5) : child.name.Substring(5);
                if (int.TryParse(idStr, out int id)) present[id] = child;
            }

            var layout = new FurnLayout();
            int moved = 0, deleted = 0, n = HouseBuilder.FurnitureItems.Length;
            for (int id = 0; id < n; id++)
            {
                if (present.TryGetValue(id, out var t))
                {
                    layout.entries.Add(new FurnEntry
                    {
                        id = id, deleted = false,
                        pos = t.localPosition, euler = t.localEulerAngles, scale = t.localScale
                    });
                    moved++;
                }
                else   // el ID existe en la tabla pero no está en la escena → borrado a mano
                {
                    layout.entries.Add(new FurnEntry { id = id, deleted = true });
                    deleted++;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath));
            File.WriteAllText(LayoutPath, JsonUtility.ToJson(layout, true));
            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[FurnitureLayout] Guardado: {moved} muebles + {deleted} borrados " +
                      $"en {LayoutPath}. Ahora sobreviven al regenerar.</color>");
        }

        // ── Clear (menu) ──────────────────────────────────────────────────────
        [MenuItem("Tools/Folklore Archives/Clear Furniture Layout")]
        public static void ClearFurnitureLayout()
        {
            if (File.Exists(LayoutPath))
            {
                AssetDatabase.DeleteAsset(LayoutPath);
                AssetDatabase.Refresh();
                Debug.Log("<color=orange>[FurnitureLayout] Borrado " + LayoutPath +
                          ". Los muebles vuelven a la colocación por código al regenerar.</color>");
            }
            else Debug.Log("[FurnitureLayout] No hay layout guardado que borrar.");
            _map = null;
        }
    }
}
