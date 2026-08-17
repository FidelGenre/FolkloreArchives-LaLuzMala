// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CriminalCampPersistence.cs — deja que las ediciones manuales del
//  owner al campamento de los ladrones (mover / rotar / escalar /
//  borrar los ranchos, fogata, colchones, chatarra, mesas, sillas)
//  sobrevivan a un regenerate.
//
//  CriminalCampBuilder arma el campamento por código en cada Generate,
//  lo que borra cualquier ajuste a mano. Cada objeto se registra con un
//  ID estable (su orden de creación en Build) → nombre "CrimCamp_##_...".
//  Este script guarda la transform local de cada uno (+ los borrados) en
//  un JSON, y CriminalCampBuilder la re-aplica al generar.
//
//  Flujo:
//    1. Generar el mapa.
//    2. Mover / rotar / escalar / borrar objetos del campamento a mano.
//    3. Tools > Folklore Archives > Save Criminal Camp Layout  ← clic acá.
//    4. Regenerar cuando quieras — el layout vuelve solo.
//    ("Clear Criminal Camp Layout" borra el archivo y vuelve a la
//     colocación por código.)
//
//  Una vez guardado, el JSON es AUTORITATIVO para los IDs que contiene.
//  Si REORDENÁS/AGREGÁS objetos en Build (y el const
//  CriminalCampBuilder.PersistCount), los IDs se desalinean → re-guardar.
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class CriminalCampPersistence
    {
        const string LayoutPath = "Assets/_FolkloreArchives/criminalcamp_layout.json";
        const string GroupName  = "MainCriminalCamp";
        const string Prefix     = "CrimCamp_";

        [System.Serializable]
        public class CrimEntry
        {
            public int id;
            public bool deleted;
            public Vector3 pos, euler, scale;
            // Hijos-prop de una estructura (ej. el inodoro/papel/botella/tacho dentro del
            // baño): se guardan por NOMBRE para que sus ajustes a mano también sobrevivan.
            public List<CrimChild> children = new List<CrimChild>();
        }

        [System.Serializable]
        public class CrimChild
        {
            public string name;
            public Vector3 pos, euler, scale;
        }

        [System.Serializable]
        class CrimLayout { public List<CrimEntry> entries = new List<CrimEntry>(); }

        static Dictionary<int, CrimEntry> _map;
        static int _counter;

        // ── Carga + reset del contador (CriminalCampBuilder.Build lo llama al empezar) ──
        public static void Begin()
        {
            _counter = 0;
            _map = new Dictionary<int, CrimEntry>();
            if (!File.Exists(LayoutPath)) return;
            try
            {
                var layout = JsonUtility.FromJson<CrimLayout>(File.ReadAllText(LayoutPath));
                if (layout?.entries != null)
                    foreach (var e in layout.entries) _map[e.id] = e;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CrimCampLayout] No se pudo leer " + LayoutPath + ": " + ex.Message);
            }
        }

        // Registra un objeto: le pone el ID en el nombre ("CrimCamp_##_...") y aplica el
        // override guardado (transform) o lo borra si estaba marcado.
        public static void Register(GameObject go)
        {
            int id = _counter++;
            if (go == null) return;
            go.name = $"{Prefix}{id:D2}_{go.name}";
            if (_map != null && _map.TryGetValue(id, out var e))
            {
                if (e.deleted) { Object.DestroyImmediate(go); return; }
                go.transform.localPosition    = e.pos;
                go.transform.localEulerAngles = e.euler;
                go.transform.localScale       = e.scale;
                // hijos-prop (interior de una estructura, ej. el baño): override por nombre
                if (e.children != null)
                    foreach (Transform ch in go.transform)
                    {
                        if (!ch.name.StartsWith("CProp_")) continue;
                        var ce = e.children.Find(x => x.name == ch.name);
                        if (ce == null) continue;
                        ch.localPosition    = ce.pos;
                        ch.localEulerAngles = ce.euler;
                        ch.localScale       = ce.scale;
                    }
            }
        }

        // ── (eliminados) Menús "Save/Clear Criminal Camp Layout" ──────────────────
        //  Mover/guardar/borrar objetos del campamento ahora es parte de "Save Map Layout",
        //  unificado con el resto del mapa. El motor de arriba (Begin/Register/Apply) sigue
        //  colocando el campamento en cada Generate; Save Map Layout lo pisa al final.
        //  Para descartar overrides: Tools ▸ Clear Map Layout, o borrá criminalcamp_layout.json.
    }
}
