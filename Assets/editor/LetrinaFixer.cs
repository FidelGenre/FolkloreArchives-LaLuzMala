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

            // 3) instanciar la granja fresca del FBX y DESEMPAQUETARLA (si no, no se puede reparentar)
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null) { EditorUtility.DisplayDialog("Letrina", "No encontré " + Fbx, "OK"); return; }
            var farm = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(farm, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 4) juntar TODAS las piezas de la letrina (letrina, letrina.001 ... letrina.006)
            var pieces = new List<Transform>();
            foreach (var t in farm.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.ToLowerInvariant().StartsWith("letrina")) pieces.Add(t);
            if (pieces.Count == 0)
            {
                Object.DestroyImmediate(farm);
                EditorUtility.DisplayDialog("Letrina", "No encontré piezas 'letrina*' dentro del FBX.", "OK");
                return;
            }

            // centro (por bounds de los renderers) para usar de pivote del grupo
            Bounds? bb = null;
            foreach (var p in pieces)
            {
                var rr = p.GetComponent<Renderer>();
                if (rr == null) continue;
                if (bb == null) bb = rr.bounds; else { var b = bb.Value; b.Encapsulate(rr.bounds); bb = b; }
            }
            Vector3 anchor = bb.HasValue ? bb.Value.center : pieces[0].position;

            // grupo nuevo en el anchor; metemos las piezas manteniendo su layout (worldPositionStays)
            var group = new GameObject("Letrina_Fresca");
            group.transform.position = anchor;
            group.transform.rotation = Quaternion.identity;
            foreach (var p in pieces) p.SetParent(group.transform, true);

            Object.DestroyImmediate(farm);   // tiramos el resto de la granja

            // 5) mover el grupo a donde tenías la rota (dejo rotación/escala AUTORAL: derecha y tamaño OK)
            group.transform.position = wp;

            // 6) re-aplicar los materiales URP por nombre (evita magenta si el FBX trae built-in)
            foreach (var r in group.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < src.Length; i++)
                    if (src[i] != null && mats.TryGetValue(src[i].name, out var urp)) { src[i] = urp; changed = true; }
                if (changed) r.sharedMaterials = src;
            }

            // 7) apagamos TODAS las piezas "letrina*" viejas que sigan activas (antes solo apagaba
            // la seleccionada -- si se corría esto con OTRA pieza seleccionada, el resto de las
            // viejas quedaban activas superpuestas con las frescas, causando duplicados confusos:
            // dos objetos con el mismo nombre, uno roto/Combined Mesh y otro sano).
            int deactivated = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeSelf) continue;
                if (t.IsChildOf(group.transform)) continue;   // no apagar las piezas frescas recién puestas
                if (!t.name.ToLowerInvariant().StartsWith("letrina")) continue;
                t.gameObject.SetActive(false);
                deactivated++;
            }

            Undo.RegisterCreatedObjectUndo(group, "Reponer letrina");
            Selection.activeGameObject = group;
            EditorGUIUtility.PingObject(group);
            Debug.Log("[Letrina] Repuesta 'Letrina_Fresca' (" + pieces.Count + " piezas) en " + wp +
                      ". " + deactivated + " pieza(s) 'letrina*' vieja(s) quedaron DESACTIVADAS " +
                      "(no solo la seleccionada); si quedó bien, borralas. Ajustá Y y rotación si hace falta.");
            _ = wr; _ = ws;
        }
    }
}
