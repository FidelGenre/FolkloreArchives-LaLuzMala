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
            // true = objeto oculto = BORRAR al regenerar ("ocultar = borrar"). Default
            // false = se conserva. Como es el default, cualquier JSON viejo (sin este
            // campo) queda con deleted=false → nada se borra de más. Compatible hacia atrás.
            public bool deleted;
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

        // owner: "sigue diminuto el perro" -- el perro (DOG/Model) recalcula su propia
        // escala CADA Generate midiendo la malla real (ver TestPlayerBuilder/
        // NetworkBuilder). Si alguna vez se guardó el layout completo con el perro en un
        // estado roto (chiquito/gigante de algún intento anterior), ese JSON viejo
        // pisaba la escala recién calculada en cada Generate posterior -- ningún fix de
        // escala podía sobrevivir a esto. Se excluye "DOG" (y sus hijos) de guardar/
        // restaurar layout: su transform es 100% procedural, nunca manual.
        //
        // MISMO bug, esta vez en los 3 amigos ("al bajarse quedan bajo tierra y
        // enanos"): "Model" es el hijo que usan TODOS los personajes humanoides
        // (amigos, criminales, jugador de red, la vieja -- FriendNpcBuilder/
        // CriminalNpcBuilder/NetworkBuilder/OldLadyNpcBuilder/TestPlayerBuilder) y
        // HumanWalkAnim lo achica/hunde cada frame mientras va sentado (seatedScaleY/
        // seatedModelDrop). El layout se guardó en algún momento con los amigos
        // sentados adentro del auto -- esa pose achicada quedó CONGELADA en el JSON,
        // y HumanWalkAnim.Awake() la lee como si fuera la altura normal de parado (su
        // "escala base"), así que StandFriend() nunca lograba devolverlos a su tamaño
        // real. "Model" es 100% procedural (animación), nunca se ajusta a mano.
        static bool SkipSubtree(string name) => name == "DOG" || name == "Model";

        // Igual que DOG pero SOLO el transform del propio objeto (sus HIJOS sí se guardan):
        // el auto (Renault12) lo posiciona 100% el código en cada Generate
        // (CarBuilder.SnapToRoadExtensionTip lo reubica en la punta de la ruta). Si se
        // guardara su transform, quedaría CONGELADO en una punta vieja y, al mover la ruta,
        // el auto aparecería DESFASADO hasta el próximo Generate. No guardamos su transform,
        // pero SÍ el de los amigos que van sentados adentro (hijos, con su pose a mano).
        static bool SkipOwnTransform(string name) => name == "Renault12";

        static void Walk(Transform t, string parentPath, List<Entry> entries)
        {
            var nameCounts = new Dictionary<string, int>();
            foreach (Transform child in t)
            {
                if (SkipSubtree(child.name)) continue;
                int occ = nameCounts.TryGetValue(child.name, out var n) ? n : 0;
                nameCounts[child.name] = occ + 1;
                string path = parentPath + "/" + child.name + "#" + occ;
                if (!SkipOwnTransform(child.name))
                    entries.Add(new Entry
                    {
                        path = path,
                        pos = child.localPosition,
                        euler = child.localEulerAngles,
                        scale = child.localScale,
                        // desmarcar el objeto en el Inspector (SetActive false) = borrarlo al regenerar
                        deleted = !child.gameObject.activeSelf
                    });
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
            var toDelete = new List<GameObject>();
            ApplyWalk(root.transform, "", map, ref applied, toDelete);

            int removed = 0;
            foreach (var go in toDelete)
                if (go != null) { Object.DestroyImmediate(go); removed++; }

            int cloned = RecreateClones(root.transform, map);

            Debug.Log($"<color=lime>[MapLayoutPersistence] Aplicadas {applied} posiciones, " +
                      $"borrados {removed} objetos ocultos, {cloned} duplicados recreados.</color>");
        }

        static void ApplyWalk(Transform t, string parentPath, Dictionary<string, Entry> map,
                              ref int applied, List<GameObject> toDelete)
        {
            var nameCounts = new Dictionary<string, int>();
            // copiamos los hijos a una lista antes de recorrer: vamos a marcar borrados y
            // no se puede modificar la jerarquía mientras se itera con foreach directo.
            var children = new List<Transform>();
            foreach (Transform c in t) children.Add(c);

            foreach (var child in children)
            {
                if (child == null || SkipSubtree(child.name)) continue;
                int occ = nameCounts.TryGetValue(child.name, out var n) ? n : 0;
                nameCounts[child.name] = occ + 1;
                string path = parentPath + "/" + child.name + "#" + occ;

                if (map.TryGetValue(path, out var e))
                {
                    child.localPosition = e.pos;
                    child.localEulerAngles = e.euler;
                    child.localScale = e.scale;
                    applied++;
                    if (e.deleted) { toDelete.Add(child.gameObject); continue; } // ocultar = borrar
                }
                else if (path.StartsWith("/AbandonedFarm"))
                {
                    // Objeto DE LA GRANJA que no quedó guardado = lo borraste a mano (o es
                    // el terreno interno del FBX, que nunca queremos). La granja se
                    // construye COMPLETA (478 piezas) y acá reproducimos esos borrados:
                    // "lo que no está en el layout, no va". Es seguro porque la granja se
                    // arma 100% del FBX (nadie le agrega objetos por código) y todos sus
                    // nombres son únicos, así que el índice #ocurrencia nunca se corre.
                    toDelete.Add(child.gameObject);
                    continue;
                }
                ApplyWalk(child, path, map, ref applied, toDelete);
            }
        }

        // ── Duplicados hechos a mano ("cosechas (1)", "Roca0 (2)"...) ─────────────────
        //    El código no genera esos clones; los recreamos clonando el objeto base "X"
        //    del mismo padre y aplicándoles el transform guardado.
        static readonly System.Text.RegularExpressions.Regex CloneRx =
            new System.Text.RegularExpressions.Regex(@"^(.*) \(\d+\)$");

        static int RecreateClones(Transform root, Dictionary<string, Entry> map)
        {
            // ordenar por profundidad para que el objeto base (más "arriba") ya exista
            var cloneEntries = new List<Entry>();
            foreach (var e in map.Values)
                if (!e.deleted && CloneRx.IsMatch(LeafName(e.path))) cloneEntries.Add(e);
            cloneEntries.Sort((a, b) => Depth(a.path).CompareTo(Depth(b.path)));

            int n = 0;
            foreach (var e in cloneEntries)
            {
                if (FindByLayoutPath(root, e.path) != null) continue; // ya existe, no recrear

                var parent = FindByLayoutPath(root, ParentPath(e.path));
                if (parent == null) continue;

                string baseName = CloneRx.Match(LeafName(e.path)).Groups[1].Value;
                Transform baseObj = null;
                foreach (Transform c in parent) if (c.name == baseName) { baseObj = c; break; }
                if (baseObj == null) continue;

                var clone = Object.Instantiate(baseObj.gameObject, parent);
                clone.name = LeafName(e.path); // "cosechas (1)"
                clone.transform.localPosition = e.pos;
                clone.transform.localEulerAngles = e.euler;
                clone.transform.localScale = e.scale;
                n++;
            }
            return n;
        }

        static int Depth(string path) { int d = 0; foreach (char c in path) if (c == '/') d++; return d; }

        static string LeafName(string path)
        {
            string last = path.Substring(path.LastIndexOf('/') + 1);
            int h = last.LastIndexOf('#');
            return h >= 0 ? last.Substring(0, h) : last;
        }

        static string ParentPath(string path)
        {
            int i = path.LastIndexOf('/');
            return i <= 0 ? "" : path.Substring(0, i);
        }

        // Navega el árbol siguiendo "nombre#ocurrencia" en cada nivel (misma clave que Walk).
        static Transform FindByLayoutPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            Transform cur = root;
            foreach (var seg in path.Split('/'))
            {
                if (seg.Length == 0) continue;
                int h = seg.LastIndexOf('#');
                string name = h >= 0 ? seg.Substring(0, h) : seg;
                int occ = (h >= 0 && int.TryParse(seg.Substring(h + 1), out var o)) ? o : 0;
                Transform found = null; int count = 0;
                foreach (Transform c in cur)
                    if (c.name == name) { if (count == occ) { found = c; break; } count++; }
                if (found == null) return null;
                cur = found;
            }
            return cur;
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
