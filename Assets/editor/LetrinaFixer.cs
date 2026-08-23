// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LetrinaFixer.cs — repone una LETRINA "fresca" (sin static-batch)
//  del FBX original de la granja, en el MISMO lugar donde el owner
//  dejó la letrina rota. La letrina de la escena quedó como
//  "Combined Mesh (root: scene)": al moverla se rompe/deja de
//  texturizar porque sus vértices están horneados en el batch. Este
//  botón instancia una copia limpia y le re-aplica los materiales URP
//  (por nombre) del objeto viejo, así queda con textura y se puede
//  mover libremente.
//
//  USO: seleccioná la letrina rota en la Hierarchy y corré
//       Folklore ▸ Reponer letrina (fresca con texturas).
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class LetrinaFixer
    {
        const string Fbx = "Assets/ExternalAssets/AbandonedFarm/AbandonedFarm.fbx";

        [MenuItem("Folklore/Reponer letrina (fresca con texturas)")]
        static void ReplaceLetrina()
        {
            var old = Selection.activeGameObject;
            if (old == null)
            {
                EditorUtility.DisplayDialog("Letrina",
                    "Seleccioná primero la letrina rota en la Hierarchy y volvé a correr esto.", "OK");
                return;
            }

            // 1) transform MUNDO del viejo (donde la querés dejar)
            var ot = old.transform;
            Vector3 wp = ot.position; Quaternion wr = ot.rotation; Vector3 ws = ot.lossyScale;

            // 2) materiales URP correctos (por nombre) del objeto viejo — para no quedar en magenta
            var mats = new Dictionary<string, Material>();
            foreach (var r in old.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && !mats.ContainsKey(m.name)) mats[m.name] = m;

            // 3) instanciar la granja fresca del FBX y aislar el nodo "letrina"
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null) { EditorUtility.DisplayDialog("Letrina", "No encontré " + Fbx, "OK"); return; }
            var farm = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            var letr = FindLetrina(farm.transform);
            if (letr == null)
            {
                Object.DestroyImmediate(farm);
                EditorUtility.DisplayDialog("Letrina", "No encontré un nodo 'letrina' dentro del FBX.", "OK");
                return;
            }

            letr.SetParent(null, true);
            letr.name = "Letrina_Fresca";
            Object.DestroyImmediate(farm);   // tiramos el resto de la granja instanciada

            // 4) ubicarla EXACTO donde tenías la rota
            letr.position = wp; letr.rotation = wr; letr.localScale = ws;

            // 5) re-aplicar los materiales URP por nombre (evita magenta si el FBX trae built-in)
            foreach (var r in letr.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < src.Length; i++)
                    if (src[i] != null && mats.TryGetValue(src[i].name, out var urp)) { src[i] = urp; changed = true; }
                if (changed) r.sharedMaterials = src;
            }

            // 6) apagamos la rota (la borrás vos cuando confirmes que quedó bien)
            old.SetActive(false);
            Undo.RegisterCreatedObjectUndo(letr.gameObject, "Reponer letrina");
            Selection.activeGameObject = letr.gameObject;
            EditorGUIUtility.PingObject(letr.gameObject);
            Debug.Log("[Letrina] Repuesta 'Letrina_Fresca' en " + wp +
                      ". La vieja quedó DESACTIVADA; si quedó bien, borrala.");
        }

        // busca el nodo de la letrina en el FBX: exacto "letrina" primero; si no, el primero que empiece con "letrina".
        static Transform FindLetrina(Transform root)
        {
            Transform starts = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "letrina") return t;
                if (starts == null && n.StartsWith("letrina")) starts = t;
            }
            return starts;
        }
    }
}
