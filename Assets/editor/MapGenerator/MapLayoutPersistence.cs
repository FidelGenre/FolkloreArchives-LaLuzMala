// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  MapLayoutPersistence.cs — owner: "necesito que todo el mapa sea
//  guardable con un boton en tools". A diferencia de
//  ManualLayoutPersistence (por builder, hay que registrar cada
//  objeto a mano en el código) y CriminalCampPersistence (propio del
//  campamento criminal), este es GENÉRICO y cubre TODO el mapa de una:
//  camina toda la jerarquía bajo el root generado y guarda la
//  posición/rotación/escala LOCAL de cada Transform, sin que ningún
//  builder tenga que registrarse.
//
//  Cómo identifica cada objeto: "nombre#índice de hermano" en cada
//  nivel de la jerarquía (ej. "AreasAndPOIs/Estepa/MolinoOxidado#1").
//  El índice desambigua nombres repetidos entre hermanos (ej. varios
//  "Roca0".."Roca17"). ⚠ Si el CÓDIGO cambia el orden en que crea los
//  hijos de un mismo padre (agrega/saca uno en el medio), los índices
//  de todo lo que viene después se corren y un edit guardado puede
//  terminar aplicándose al objeto equivocado — mismo trade-off que ya
//  acepta ManualLayoutPersistence con sus IDs por orden de registro.
//
//  Uso: Tools > Folklore Archives > Save Map Layout (después de mover
//  cosas a mano) y Clear Map Layout (para descartar lo guardado).
//  ApplySavedLayout() se llama solo al final de Generate(), después de
//  que todos los builders ya armaron el mapa desde MapLayout.cs.
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class MapLayoutPersistence
    {
        const string SavePath = "Assets/_FolkloreArchives/layout_FullMap.json";

        [System.Serializable]
        class Entry
        {
            public string path;
            public Vector3 pos, euler, scale;
        }

        [System.Serializable]
        class Layout { public List<Entry> entries = new List<Entry>(); }

        [MenuItem("Tools/Folklore Archives/Save Map Layout")]
        public static void SaveMapLayout()
        {
            var root = GameObject.Find(MapLayout.RootName);
            if (root == null)
            {
                Debug.LogWarning("[MapLayoutPersistence] no encontré " + MapLayout.RootName + " — generá el mapa primero.");
                return;
            }

            var layout = new Layout();
            foreach (Transform child in root.transform)
                Walk(child, "", layout.entries);

            string dir = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SavePath, JsonUtility.ToJson(layout, true));
            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[MapLayoutPersistence] Guardadas {layout.entries.Count} posiciones en {SavePath}</color>");
        }

        static void Walk(Transform t, string parentPath, List<Entry> entries)
        {
            string path = parentPath + "/" + t.name + "#" + t.GetSiblingIndex();
            entries.Add(new Entry { path = path, pos = t.localPosition, euler = t.localEulerAngles, scale = t.localScale });
            foreach (Transform child in t) Walk(child, path, entries);
        }

        // Llamado automáticamente desde MapGenerator.Generate(), al final, después de
        // que TODOS los builders ya construyeron el mapa desde MapLayout.cs.
        public static void ApplySavedLayout()
        {
            if (!File.Exists(SavePath)) return;

            Layout layout;
            try { layout = JsonUtility.FromJson<Layout>(File.ReadAllText(SavePath)); }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[MapLayoutPersistence] no pude leer " + SavePath + ": " + ex.Message);
                return;
            }
            if (layout?.entries == null || layout.entries.Count == 0) return;

            var root = GameObject.Find(MapLayout.RootName);
            if (root == null) return;

            var map = new Dictionary<string, Entry>();
            foreach (var e in layout.entries) map[e.path] = e;

            int applied = 0;
            foreach (Transform child in root.transform) ApplyWalk(child, "", map, ref applied);
            Debug.Log($"<color=lime>[MapLayoutPersistence] Aplicadas {applied}/{layout.entries.Count} posiciones guardadas.</color>");
        }

        static void ApplyWalk(Transform t, string parentPath, Dictionary<string, Entry> map, ref int applied)
        {
            string path = parentPath + "/" + t.name + "#" + t.GetSiblingIndex();
            if (map.TryGetValue(path, out var e))
            {
                t.localPosition = e.pos;
                t.localEulerAngles = e.euler;
                t.localScale = e.scale;
                applied++;
            }
            foreach (Transform child in t) ApplyWalk(child, path, map, ref applied);
        }

        [MenuItem("Tools/Folklore Archives/Clear Map Layout")]
        public static void ClearMapLayout()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                AssetDatabase.Refresh();
                Debug.Log("[MapLayoutPersistence] Layout completo del mapa borrado.");
            }
            else Debug.Log("[MapLayoutPersistence] No había ningún layout guardado.");
        }
    }
}
