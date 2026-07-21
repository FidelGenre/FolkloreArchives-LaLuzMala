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
//  Cómo identifica cada objeto: "nombre#ocurrencia" en cada nivel de la
//  jerarquía, donde "ocurrencia" es cuántos hermanos ANTERIORES tienen
//  el MISMO nombre (0 si el nombre es único entre sus hermanos). Ej.
//  "OldLadyHouse_ALP#0" (nombre único → estable pase lo que pase);
//  "Roca0#0".."Roca17#0" (cada uno ya único por el número EN el
//  nombre); si dos hermanos se llamaran literalmente igual, el
//  segundo sería "#1".
//
//  ¡OJO, esto NO es el índice de hermano crudo (GetSiblingIndex)! La
//  primera versión usaba eso y tenía un bug real (owner: "no se
//  guardaron las posiciones de la casa de la abuela, el granero, el
//  puente") -- la casa/galpón/puente son hermanos DIRECTOS de la raíz
//  del mapa junto con TERRENO, AMBIENTE, BOSQUE, etc., y cualquier
//  objeto condicional creado ANTES en ese mismo padre (ej. "si existe
//  tal asset, crear tal cosa") corre el índice de todo lo que viene
//  después -- rompiendo la coincidencia entre lo guardado y lo
//  regenerado. Indexar por OCURRENCIA DEL MISMO NOMBRE en vez de
//  posición cruda hace que la ruta de un objeto con nombre único no
//  dependa en absoluto de sus hermanos.
//  ⚠ Sigue existiendo un caso límite: si el código cambia CUÁNTOS
//  hermanos comparten el MISMO nombre antes que este objeto (ej. antes
//  había 3 "Roca" y ahora hay 5, antes de este punto), la ocurrencia
//  de los que vienen después de esos sí se corre. Mucho menos frecuente
//  que antes (solo afecta a nombres repetidos, no a todo el árbol), pero
//  no imposible.
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
            Walk(root.transform, "", layout.entries);

            string dir = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SavePath, JsonUtility.ToJson(layout, true));
            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[MapLayoutPersistence] Guardadas {layout.entries.Count} posiciones en {SavePath}</color>");
        }

        static void Walk(Transform t, string parentPath, List<Entry> entries)
        {
            var nameCounts = new Dictionary<string, int>();
            foreach (Transform child in t)
            {
                int occ = nameCounts.TryGetValue(child.name, out var n) ? n : 0;
                nameCounts[child.name] = occ + 1;
                string path = parentPath + "/" + child.name + "#" + occ;
                entries.Add(new Entry { path = path, pos = child.localPosition, euler = child.localEulerAngles, scale = child.localScale });
                Walk(child, path, entries);
            }
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
            ApplyWalk(root.transform, "", map, ref applied);
            Debug.Log($"<color=lime>[MapLayoutPersistence] Aplicadas {applied}/{layout.entries.Count} posiciones guardadas.</color>");
        }

        static void ApplyWalk(Transform t, string parentPath, Dictionary<string, Entry> map, ref int applied)
        {
            var nameCounts = new Dictionary<string, int>();
            foreach (Transform child in t)
            {
                int occ = nameCounts.TryGetValue(child.name, out var n) ? n : 0;
                nameCounts[child.name] = occ + 1;
                string path = parentPath + "/" + child.name + "#" + occ;
                if (map.TryGetValue(path, out var e))
                {
                    child.localPosition = e.pos;
                    child.localEulerAngles = e.euler;
                    child.localScale = e.scale;
                    applied++;
                }
                ApplyWalk(child, path, map, ref applied);
            }
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
