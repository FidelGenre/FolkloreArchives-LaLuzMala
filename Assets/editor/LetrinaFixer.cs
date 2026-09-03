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
//  USO: Folklore ▸ Reponer letrina (fresca con texturas) -- ya NO hace falta
//       seleccionar nada: busca sola las piezas "letrina*" activas en la
//       escena (las que dejó AbandonedFarmBuilder, Combined Mesh) y las
//       repone. También la llama sola HouseBuilder en cada Generate (ver
//       RanchoNpcSetup.EnsureAllRanchoDoors) -- owner: "no quiero tener que
//       tocar todas las cosas y armar de nuevo, quiero que las puertas sean
//       parte del mapa".
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
        static void ReplaceLetrinaMenu() => ReplaceLetrinaInternal(interactive: true);

        // interactive:true = botón manual (diálogos + Undo + Selection). interactive:false =
        // llamada automática desde Generate (solo logs, nunca bloquea con un diálogo).
        public static void ReplaceLetrinaInternal(bool interactive)
        {
            // 1) juntar las piezas VIEJAS "letrina*" activas en la escena -- ya no depende de
            // selección: son las que dejó AbandonedFarmBuilder (Combined Mesh) o, si esto ya se
            // corrió antes en esta sesión, cualquier resto que haya quedado activo.
            var old = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.activeSelf) continue;
                if (t.name == "Letrina_Fresca" || (t.parent != null && t.root.name == "Letrina_Fresca")) continue;
                if (!t.name.ToLowerInvariant().StartsWith("letrina")) continue;
                old.Add(t);
            }
            if (old.Count == 0)
            {
                if (interactive)
                    EditorUtility.DisplayDialog("Letrina", "No encontré piezas 'letrina*' activas en la escena.", "OK");
                else
                    Debug.Log("[Letrina] Auto: no hay piezas 'letrina*' activas para reponer (¿ya está reemplazada?).");
                return;
            }

            // posición ancla: centro por bounds de TODAS las piezas viejas (reemplaza la vieja
            // dependencia de "la posición del objeto seleccionado")
            Bounds? oldBb = null;
            foreach (var p in old)
            {
                var rr = p.GetComponent<Renderer>();
                if (rr == null) continue;
                if (oldBb == null) oldBb = rr.bounds; else { var b = oldBb.Value; b.Encapsulate(rr.bounds); oldBb = b; }
            }
            Vector3 wp = oldBb.HasValue ? oldBb.Value.center : old[0].position;

            // 2) materiales URP correctos (por nombre) de las piezas viejas — para no quedar en magenta
            var mats = new Dictionary<string, Material>();
            foreach (var p in old)
            {
                var r = p.GetComponent<Renderer>();
                if (r == null) continue;
                foreach (var m in r.sharedMaterials)
                    if (m != null && !mats.ContainsKey(m.name)) mats[m.name] = m;
            }

            // 3) instanciar la granja fresca del FBX y DESEMPAQUETARLA (si no, no se puede reparentar)
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (fbx == null)
            {
                if (interactive) EditorUtility.DisplayDialog("Letrina", "No encontré " + Fbx, "OK");
                else Debug.LogWarning("[Letrina] Auto: no encontré " + Fbx);
                return;
            }
            var farm = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(farm, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // 4) juntar TODAS las piezas de la letrina (letrina, letrina.001 ... letrina.006)
            var pieces = new List<Transform>();
            foreach (var t in farm.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.ToLowerInvariant().StartsWith("letrina")) pieces.Add(t);
            if (pieces.Count == 0)
            {
                Object.DestroyImmediate(farm);
                if (interactive) EditorUtility.DisplayDialog("Letrina", "No encontré piezas 'letrina*' dentro del FBX.", "OK");
                else Debug.LogWarning("[Letrina] Auto: no encontré piezas 'letrina*' dentro del FBX.");
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

            Debug.Log("[Letrina] Repuesta 'Letrina_Fresca' (" + pieces.Count + " piezas) en " + wp +
                      ". " + deactivated + " pieza(s) 'letrina*' vieja(s) quedaron DESACTIVADAS.");

            if (!interactive) return;   // el resto (Undo/Selection) es solo para el botón manual

            Undo.RegisterCreatedObjectUndo(group, "Reponer letrina");
            Selection.activeGameObject = group;
            EditorGUIUtility.PingObject(group);
        }
    }
}
