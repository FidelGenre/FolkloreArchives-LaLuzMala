// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  ManualLayoutPersistence.cs — versión GENÉRICA de
//  CriminalCampPersistence: deja que las ediciones manuales del
//  owner (mover / rotar / escalar / borrar) a los objetos que
//  coloca CUALQUIER builder del mapa sobrevivan a un regenerate,
//  sin que cada builder tenga que escribir su propio JSON.
//
//  Cómo se suma un builder nuevo (ver AreaPoiBuilder.cs como ejemplo):
//    1. Al empezar Build(): ManualLayoutPersistence.Begin("NombreDeGrupo");
//    2. Envolver cada objeto de nivel superior que quiera protegerse:
//         var g = ManualLayoutPersistence.Register("NombreDeGrupo", ObjetoQueArme(...));
//       (si Register() devuelve null es porque el owner lo borró a mano — no seguir
//       construyendo cosas encima de ese grupo)
//    3. Una constante con cuántos Register(...) hay, EN ORDEN de creación.
//    4. Dos [MenuItem] cortos (copiar/pegar) que llamen a Save/Clear para ese grupo.
//
//  Flujo del owner:
//    Generar el mapa → mover/rotar/escalar/borrar objetos a mano → Tools >
//    Folklore Archives > Save <Grupo> Layout → regenerar cuando quiera, el
//    acomodo vuelve solo. ("Clear <Grupo> Layout" borra el archivo y vuelve
//    a la colocación por código.)
//
//  Un JSON por grupo en Assets/_FolkloreArchives/layout_<grupo>.json. Los IDs
//  son el orden de los Register(...) en el código — si reordenás/agregás/sacás
//  llamadas hay que volver a guardar el layout de ese grupo.
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class ManualLayoutPersistence
    {
        [System.Serializable]
        public class Entry
        {
            public int id;
            public bool deleted;
            public Vector3 pos, euler, scale;
        }

        [System.Serializable]
        class Layout { public List<Entry> entries = new List<Entry>(); }

        const string Prefix = "ML_";

        static readonly Dictionary<string, Dictionary<int, Entry>> _maps = new Dictionary<string, Dictionary<int, Entry>>();
        static readonly Dictionary<string, int> _counters = new Dictionary<string, int>();

        static string PathFor(string group) => $"Assets/_FolkloreArchives/layout_{group}.json";

        // Carga + reinicia el contador de un grupo (llamar al empezar el Build() de ese builder).
        public static void Begin(string group)
        {
            _counters[group] = 0;
            var map = new Dictionary<int, Entry>();
            _maps[group] = map;
            string path = PathFor(group);
            if (!File.Exists(path)) return;
            try
            {
                var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
                if (layout?.entries != null)
                    foreach (var e in layout.entries) map[e.id] = e;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ManualLayout:{group}] No pude leer {path}: {ex.Message}");
            }
        }

        // Registra un objeto YA CONSTRUIDO (con todos sus hijos): le pone el ID en el
        // nombre y aplica la transform local guardada, o lo borra si estaba marcado.
        // Devuelve null si lo borró — el caller no debe seguir usando ese objeto.
        public static GameObject Register(string group, GameObject go)
        {
            if (go == null) return null;
            int id = _counters.TryGetValue(group, out var c) ? c : 0;
            _counters[group] = id + 1;
            go.name = $"{Prefix}{id:D3}_{go.name}";
            if (_maps.TryGetValue(group, out var map) && map.TryGetValue(id, out var e))
            {
                if (e.deleted) { Object.DestroyImmediate(go); return null; }
                go.transform.localPosition    = e.pos;
                go.transform.localEulerAngles = e.euler;
                go.transform.localScale       = e.scale;
            }
            return go;
        }

        // Overload de conveniencia para builders que trabajan con Transform.
        public static Transform Register(string group, Transform tr) =>
            tr == null ? null : Register(group, tr.gameObject)?.transform;

        // ── Save/Clear genéricos — cada builder los llama desde su propio [MenuItem] ──
        public static void Save(string group, string rootObjectName, int persistCount)
        {
            var groupGo = GameObject.Find(rootObjectName);
            if (groupGo == null)
            {
                Debug.LogWarning($"[ManualLayout:{group}] No encontré '{rootObjectName}' en la escena. Generá el mapa primero.");
                return;
            }

            var present = new Dictionary<int, Transform>();
            foreach (Transform child in groupGo.transform)
            {
                if (!child.name.StartsWith(Prefix)) continue;
                int start = Prefix.Length;
                int us = child.name.IndexOf('_', start);
                string idStr = us > start ? child.name.Substring(start, us - start) : child.name.Substring(start);
                if (int.TryParse(idStr, out int id)) present[id] = child;
            }

            if (present.Count == 0)
            {
                Debug.LogWarning($"[ManualLayout:{group}] No encontré objetos '{Prefix}###_' en la escena. " +
                                  "Regenerá el mapa (para que se numeren) antes de guardar.");
                return;
            }

            var layout = new Layout();
            int saved = 0, deleted = 0;
            for (int id = 0; id < persistCount; id++)
            {
                if (present.TryGetValue(id, out var tr))
                {
                    layout.entries.Add(new Entry
                    {
                        id = id, deleted = false,
                        pos = tr.localPosition, euler = tr.localEulerAngles, scale = tr.localScale
                    });
                    saved++;
                }
                else
                {
                    layout.entries.Add(new Entry { id = id, deleted = true });
                    deleted++;
                }
            }

            string path = PathFor(group);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(layout, true));
            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[ManualLayout:{group}] Guardado: {saved} objetos + {deleted} borrados " +
                      $"en {path}. Ahora sobreviven al regenerar.</color>");
        }

        public static void Clear(string group)
        {
            string path = PathFor(group);
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
                Debug.Log($"<color=orange>[ManualLayout:{group}] Borrado {path}. " +
                          "Vuelve a la colocación por código al regenerar.</color>");
            }
            else Debug.Log($"[ManualLayout:{group}] No hay layout guardado que borrar.");
            _maps.Remove(group);
        }
    }
}
