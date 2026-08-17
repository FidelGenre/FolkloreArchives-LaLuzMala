// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TreePersistence.cs — hace que el BORRADO manual de árboles
//  (pincel Paint Trees + Shift) sobreviva al Generate. Se integra
//  con "Save Terrain Paint" (TerrainPaintPersistence), igual que el
//  pasto/texturas.
//
//  Los árboles son TreeInstances del terreno con posición NORMALIZADA
//  (0..1), y el bosque es DETERMINÍSTICO (mismo seed → mismas
//  posiciones cada Generate). Por eso alcanza con guardar las
//  posiciones REMOVIDAS (diff contra el set procedural) y, al
//  regenerar, dropear del terreno los árboles que caen en esas
//  posiciones. El resto del bosque sigue siendo 100% procedural (NO se
//  congela) → si tu compañero cambia la densidad del bosque, se ve;
//  solo quedan pelados los puntos que vos borraste a mano.
//
//  Flujo:
//   - ForestBuilder.Build (tras plantar): CaptureBaseline(td) guarda el
//     set procedural completo; ApplyTreeRemovals(td) dropea lo removido.
//   - Save Terrain Paint: SaveTreeRemovals(td) = baseline ∖ vivo.
//   - Clear Terrain Paint: ClearRemovals().
// ============================================================
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class TreePersistence
    {
        // datos del owner (persistente): qué posiciones de árbol borró a mano.
        const string RemovalsPath = "Assets/_FolkloreArchives/tree_removals.bytes";
        // cache descartable: el set procedural del último Generate (para poder diffear).
        static string BaselinePath => MapLayout.GeneratedFolder + "/tree_baseline.bytes";

        // clave por posición normalizada (0..1) redondeada a 1e-5 (~6 mm en 600 m) →
        // el mismo árbol da la misma clave cada Generate; árboles distintos no colisionan.
        static long Key(Vector3 normPos)
        {
            long ix = (long)Mathf.Round(normPos.x * 100000f);
            long iz = (long)Mathf.Round(normPos.z * 100000f);
            return ix * 1000003L + iz;
        }

        // Guarda el set procedural COMPLETO (posiciones normalizadas) como baseline.
        // Llamar ANTES de aplicar las remociones.
        public static void CaptureBaseline(TerrainData td)
        {
            var inst = td.treeInstances;
            using (var bw = new BinaryWriter(File.Create(BaselinePath)))
            {
                bw.Write(inst.Length);
                foreach (var t in inst) { bw.Write(t.position.x); bw.Write(t.position.z); }
            }
        }

        // Dropea del terreno los árboles cuya posición está en la lista de removidos.
        public static void ApplyTreeRemovals(TerrainData td)
        {
            if (!File.Exists(RemovalsPath)) return;
            var removed = new HashSet<long>();
            using (var br = new BinaryReader(File.OpenRead(RemovalsPath)))
            {
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    float x = br.ReadSingle(), z = br.ReadSingle();
                    removed.Add(Key(new Vector3(x, 0f, z)));
                }
            }
            if (removed.Count == 0) return;

            var inst = td.treeInstances;
            var keep = new List<TreeInstance>(inst.Length);
            foreach (var t in inst)
                if (!removed.Contains(Key(t.position))) keep.Add(t);

            if (keep.Count != inst.Length)
            {
                td.SetTreeInstances(keep.ToArray(), true);
                Debug.Log($"[TreePersist] {inst.Length - keep.Count} árboles borrados a mano re-aplicados (quedan {keep.Count}).");
            }
        }

        // Guarda las posiciones REMOVIDAS = baseline (procedural) ∖ vivo (editado).
        // Devuelve cuántas, o -1 si falta el baseline (regenerá primero).
        public static int SaveTreeRemovals(TerrainData live)
        {
            if (!File.Exists(BaselinePath)) return -1;

            var alive = new HashSet<long>();
            foreach (var t in live.treeInstances) alive.Add(Key(t.position));

            var removed = new List<(float x, float z)>();
            using (var br = new BinaryReader(File.OpenRead(BaselinePath)))
            {
                int n = br.ReadInt32();
                for (int i = 0; i < n; i++)
                {
                    float x = br.ReadSingle(), z = br.ReadSingle();
                    if (!alive.Contains(Key(new Vector3(x, 0f, z)))) removed.Add((x, z));
                }
            }

            using (var bw = new BinaryWriter(File.Create(RemovalsPath)))
            {
                bw.Write(removed.Count);
                foreach (var r in removed) { bw.Write(r.x); bw.Write(r.z); }
            }
            return removed.Count;
        }

        public static void ClearRemovals()
        {
            if (File.Exists(RemovalsPath)) File.Delete(RemovalsPath);
        }
    }
}
