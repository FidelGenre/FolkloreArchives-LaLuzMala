// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CampsitePersistence.cs — deja que las ediciones manuales del owner
//  al dressing del campamento (mover / rotar / escalar / borrar los
//  troncos, carpas, fogata, leña, mesa) sobrevivan a un regenerate.
//
//  CampsiteBuilder arma el campamento por código en cada Generate, lo
//  que borra cualquier ajuste a mano. Cada objeto de dressing se
//  registra con un ID estable (su orden de creación en Build) → nombre
//  "Camp_##_nombre". Este script guarda la transform local de cada uno
//  (+ los borrados) en un JSON, y CampsiteBuilder la re-aplica al generar.
//
//  Flujo:
//    1. Generar el mapa.
//    2. Mover / rotar / escalar / borrar objetos del campamento a mano.
//    3. Tools > Folklore Archives > Save Campsite Layout   ← clic acá.
//    4. Regenerar cuando quieras — el layout vuelve solo.
//    (Re-clic en 3 después de más ediciones. "Clear Campsite Layout"
//     borra el archivo y vuelve a la colocación por código.)
//
//  Igual que el de muebles: una vez guardado, el JSON es AUTORITATIVO
//  para los IDs que contiene. Si REORDENÁS/AGREGÁS objetos en Build (y el
//  const CampsiteBuilder.PersistCount), los IDs se desalinean → re-guardar.
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class CampsitePersistence
    {
        const string LayoutPath = "Assets/_FolkloreArchives/campsite_layout.json";
        const string GroupName  = "Campsite";

        [System.Serializable]
        public class CampEntry
        {
            public int id;
            public bool deleted;
            public Vector3 pos, euler, scale;
        }

        [System.Serializable]
        class CampLayout { public List<CampEntry> entries = new List<CampEntry>(); }

        static Dictionary<int, CampEntry> _map;
        static int _counter;

        // ── Carga + reset del contador (CampsiteBuilder.Build lo llama al empezar) ──
        public static void Begin()
        {
            _counter = 0;
            _map = new Dictionary<int, CampEntry>();
            if (!File.Exists(LayoutPath)) return;
            try
            {
                var layout = JsonUtility.FromJson<CampLayout>(File.ReadAllText(LayoutPath));
                if (layout?.entries != null)
                    foreach (var e in layout.entries) _map[e.id] = e;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CampsiteLayout] No se pudo leer " + LayoutPath + ": " + ex.Message);
            }
        }

        // Registra un objeto de dressing: le pone el ID en el nombre ("Camp_##_...")
        // y aplica el override guardado (transform) o lo borra si estaba marcado.
        public static void Register(GameObject go)
        {
            int id = _counter++;
            if (go == null) return;
            go.name = $"Camp_{id:D2}_{go.name}";
            if (_map != null && _map.TryGetValue(id, out var e))
            {
                if (e.deleted) { Object.DestroyImmediate(go); return; }
                go.transform.localPosition    = e.pos;
                go.transform.localEulerAngles = e.euler;
                go.transform.localScale       = e.scale;
            }
        }

        // ── Save (menú) ───────────────────────────────────────────────────────
        [MenuItem("Tools/Folklore Archives/Save Campsite Layout")]
        public static void SaveCampsiteLayout()
        {
            var groupGo = GameObject.Find(GroupName);
            if (groupGo == null)
            {
                Debug.LogWarning($"[CampsiteLayout] No encontré '{GroupName}' en la escena. Generá el mapa primero.");
                return;
            }

            var present = new Dictionary<int, Transform>();
            // Camino A (post-regenerate): objetos ya con ID "Camp_##_...".
            foreach (Transform child in groupGo.transform)
            {
                if (!child.name.StartsWith("Camp_")) continue;
                int us = child.name.IndexOf('_', 5);       // "Camp_" = 5 chars
                string idStr = us > 5 ? child.name.Substring(5, us - 5) : child.name.Substring(5);
                if (int.TryParse(idStr, out int id)) present[id] = child;
            }
            // Camino B (escena vieja, sin IDs): matchear por nombre base EN ORDEN contra
            // PersistNames (asume que están todos y en el orden de creación — cierto justo
            // después de generar sin borrar nada) y migrar el nombre a "Camp_##_...".
            if (present.Count == 0)
            {
                var names = CampsiteBuilder.PersistNames;
                int ptr = 0;
                foreach (Transform child in groupGo.transform)
                {
                    if (ptr >= names.Length) break;
                    if (child.name == names[ptr])
                    {
                        present[ptr] = child;
                        child.name = $"Camp_{ptr:D2}_{names[ptr]}";
                        ptr++;
                    }
                }
                if (ptr < names.Length)
                    Debug.LogWarning($"[CampsiteLayout] Sólo se reconocieron {ptr}/{names.Length} objetos por " +
                                     "nombre (¿se borró o renombró alguno?). Guardo lo encontrado.");
            }

            var layout = new CampLayout();
            int saved = 0, deleted = 0, n = CampsiteBuilder.PersistCount;
            for (int id = 0; id < n; id++)
            {
                if (present.TryGetValue(id, out var tr))
                {
                    layout.entries.Add(new CampEntry
                    {
                        id = id, deleted = false,
                        pos = tr.localPosition, euler = tr.localEulerAngles, scale = tr.localScale
                    });
                    saved++;
                }
                else
                {
                    layout.entries.Add(new CampEntry { id = id, deleted = true });
                    deleted++;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath));
            File.WriteAllText(LayoutPath, JsonUtility.ToJson(layout, true));
            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[CampsiteLayout] Guardado: {saved} objetos + {deleted} borrados " +
                      $"en {LayoutPath}. Ahora sobreviven al regenerar.</color>");
        }

        // ── Clear (menú) ──────────────────────────────────────────────────────
        [MenuItem("Tools/Folklore Archives/Clear Campsite Layout")]
        public static void ClearCampsiteLayout()
        {
            if (File.Exists(LayoutPath))
            {
                AssetDatabase.DeleteAsset(LayoutPath);
                AssetDatabase.Refresh();
                Debug.Log("<color=orange>[CampsiteLayout] Borrado " + LayoutPath +
                          ". El campamento vuelve a la colocación por código al regenerar.</color>");
            }
            else Debug.Log("[CampsiteLayout] No hay layout guardado que borrar.");
            _map = null;
        }
    }
}
