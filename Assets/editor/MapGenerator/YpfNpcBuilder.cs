// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  YpfNpcBuilder.cs — coloca en cada Generate los 2 NPCs de la YPF (playero
//  Richard en un surtidor, viejo "CreepyOldMan" en el baño). Se llama desde
//  MapGenerator.Generate. owner dejó posición/rotación/escala EXACTAS a mano
//  (ya con la altura y el ajuste que le gustó) -> se hornean tal cual, sin
//  recalcular. Movilidad como los amigos: CapsuleCollider + FriendWander +
//  HumanWalkAnim (solo Richard, que tiene rig; el viejo es OBJ y se desliza).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class YpfNpcBuilder
    {
        public static void Build(Transform parent)
        {
            // owner: "hay un solo playero y quiero que esté en la silla" -- Richard ya NO
            // se para en el surtidor; ahora va SENTADO en la silla de la oficina (lo hace
            // SeatPlayeroOnChair, llamado desde AreaPoiBuilder al crear la silla, o desde
            // el menú Tools sobre la silla que ya está en la escena). Acá queda solo el viejo.
            Place(parent, "Assets/ExternalAssets/CreepyOldMan/CreepyOldMan.prefab", "CreepyOldMan",
                  new Vector3(443.256f, 17.084f, -6.78f), 169.025f, 0.01301718f, rigged: true); // ahora riggeado (auto-rig Blender)
        }

        // owner: "necesito al playero sentado en ella... que no tenga la posición en T,
        // ponele los brazos a los lados". Sienta a Richard en la silla dada y lo EMPARENTA
        // a ella (así lo seguís acomodando junto con la silla). La pose reusa
        // HumanWalkAnim.seated (corrige la T-pose -> brazos abajo + dobla los muslos), pero
        // SIN el achatado que se usaba para caber en el auto (seatedScaleY=1, drop=0). Sin
        // FriendWander -> se queda sentado. Idempotente: si ya había uno sentado, lo saca.
        //
        // ⚠ La silla es un GLB con una rotación HORNEADA de -90° en X (corrección Z-up→Y-up).
        // Si Richard heredara esa rotación quedaría TUMBADO/clavado en el piso (owner: "no
        // aparece el playero sentado"). Por eso: (1) esto debe correr con la silla YA en su
        // transform FINAL (después de ApplySavedLayout -> se llama desde SeatYpfPlayero), y
        // (2) le forzamos rotación MUNDIAL derecha (upright), sin importar la inclinación del
        // padre.
        public static GameObject SeatPlayeroOnChair(GameObject chair)
        {
            if (chair == null) return null;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/Playero_Richard/Playero_Richard.prefab");
            if (pf == null) { Debug.LogWarning("[YpfNpc] falta el prefab de Richard -- ¿corrió el importador?"); return null; }

            var prev = chair.transform.Find("Playero_Richard");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);

            // bounds de la SILLA SOLA -> medir ANTES de colgar a Richard: si lo colgamos
            // primero, su propia malla (grande) entra en WorldBounds(chair) y corre el centro,
            // mandándolo lejos de la silla (owner: "aparece en otra posición").
            Bounds cb = WorldBounds(chair);
            float seatY = cb.min.y + cb.size.y * 0.5f;

            // Object.Instantiate (copia PLANA), NO PrefabUtility.InstantiatePrefab: Unity no
            // deja reparentar huesos internos de una INSTANCIA de prefab (revierte el cambio),
            // y necesitamos reencadenar la mano/pie al brazo/pierna (rig IK) para poder posarlo.
            var go = (GameObject)Object.Instantiate(pf);
            go.name = "Playero_Richard";
            go.transform.SetParent(chair.transform, false);

            // escala MUNDIAL ~1.161 (la que tenía parado), compensando la escala de la silla
            // (uniforme) para que no lo agrande/achique al colgarlo de ella.
            Vector3 cls = chair.transform.lossyScale;
            go.transform.localScale = new Vector3(1.161092f / Mathf.Max(1e-4f, cls.x),
                                                  1.161092f / Mathf.Max(1e-4f, cls.y),
                                                  1.161092f / Mathf.Max(1e-4f, cls.z));

            // DERECHO y MIRANDO A LA PC: rotación MUNDIAL upright, orientado hacia el
            // DesktopPC_YPF (owner: "mirando para adelante, para la pc"). Si no aparece la PC,
            // cae a la dirección horizontal más representativa de la silla.
            Vector3 face = FacePc(chair, cb.center);
            go.transform.rotation = Quaternion.LookRotation(face, Vector3.up);

            // ubicar las CADERAS del modelo sobre el asiento (centro X/Z de la silla, ~mitad de altura)
            Bounds rb = WorldBounds(go);
            float hipY = rb.min.y + rb.size.y * 0.52f;
            go.transform.position = new Vector3(cb.center.x, go.transform.position.y + (seatY - hipY), cb.center.z);

            // El rig de Richard es IK: hand.L/hand.R y foot.L/foot.R cuelgan de los CONTROLES
            // IK (MANIK/PIEIK), NO de foearm/calf. Sin solver en runtime, al bajar el brazo la
            // mano quedaba suelta en T (owner: "las manos siguen en t"). Reencadeno mano->
            // antebrazo y pie->pantorrilla (conservando su pose mundial) para que sigan al hueso
            // padre al posar.
            Reparent(go.transform, "hand.L", "foearm.L");
            Reparent(go.transform, "hand.R", "foearm.R");
            Reparent(go.transform, "foot.L", "calf.L");
            Reparent(go.transform, "foot.R", "calf.R");

            // Pose sentada ESTÁTICA (brazos a los costados + muslos adelante + pantorrillas
            // abajo), visible YA en el editor y en Play, sin script vivo (no camina). Va DESPUÉS
            // de ubicar/rotar/reencadenar. Nombres REALES del rig de Richard: brazo superior
            // "shoulder.L/R" (no "arm.L"), muslo "thigh.L/R", pantorrilla "calf.L/R".
            FolkloreArchives.HumanWalkAnim.PoseSeatedStatic(
                go.transform, face,
                new[] { "shoulder.L", "shoulder.R" },
                new[] { "thigh.L", "thigh.R" },
                new[] { "calf.L", "calf.R" });

            // owner: "metele colisión al playero" -- cápsula parada (valores LOCALES, escalan con
            // Richard). Persiste cuando se para en el screamer, así el jugador no lo atraviesa.
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.7f; col.radius = 0.3f; col.center = new Vector3(0f, 0.85f, 0f);

            return go;
        }

        // reencadena 'child' bajo 'parent' (ambos por nombre, en cualquier profundidad),
        // conservando su transform de MUNDO -> nada se mueve hasta que posamos los huesos.
        static void Reparent(Transform root, string child, string parent)
        {
            var c = FindDeep(root, child);
            var p = FindDeep(root, parent);
            if (c != null && p != null) c.SetParent(p, true);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        // dirección horizontal hacia el DesktopPC_YPF (hermano de la silla); si no está, usa
        // la orientación de la silla.
        static Vector3 FacePc(GameObject chair, Vector3 from)
        {
            Transform pc = null;
            var parent = chair.transform.parent;
            if (parent != null)
                foreach (Transform s in parent)
                    if (s.name == "DesktopPC_YPF") { pc = s; break; }
            if (pc != null)
            {
                Vector3 d = pc.position - from; d.y = 0f;
                if (d.sqrMagnitude > 1e-4f) return d.normalized;
            }
            return HorizontalFacing(chair.transform);
        }

        // dirección horizontal más "plana" de la silla: elige entre ±forward/±right/±up del
        // transform el eje con menor componente vertical. Evita heredar la inclinación del GLB
        // para orientar al playero derecho, con una mira relacionada a cómo está puesta la silla.
        static Vector3 HorizontalFacing(Transform t)
        {
            Vector3[] cand = { t.forward, -t.forward, t.right, -t.right, t.up, -t.up };
            Vector3 best = Vector3.forward; float bestY = float.MaxValue;
            foreach (var c in cand)
            {
                Vector3 f = c; f.y = 0f;
                if (f.sqrMagnitude < 1e-4f) continue;
                if (Mathf.Abs(c.y) < bestY) { bestY = Mathf.Abs(c.y); best = f.normalized; }
            }
            return best;
        }

        // Busca la silla OfficeChair_YPF bajo 'root' y sienta al playero. Se llama DESPUÉS de
        // ApplySavedLayout (MapGenerator), cuando la silla ya está en su posición/rotación final.
        public static void SeatYpfPlayero(Transform root)
        {
            if (root == null) return;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == "OfficeChair_YPF") { SeatPlayeroOnChair(tr.gameObject); return; }
        }

        static void Place(Transform parent, string prefabPath, string name, Vector3 pos, float yaw, float scale, bool rigged)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pf == null) { Debug.LogWarning($"[YpfNpc] falta el prefab {prefabPath} -- ¿corrió el importador?"); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;                          // coords exactas del owner
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;

            AddMobility(go, rigged);
        }

        // Mismos componentes que los 3 amigos: collider + caminar + (si tiene rig) anim.
        static void AddMobility(GameObject go, bool rigged)
        {
            var wb = WorldBounds(go);
            Vector3 ls = go.transform.lossyScale;
            var col = go.AddComponent<CapsuleCollider>();
            col.height = wb.size.y / Mathf.Max(1e-4f, ls.y);
            col.radius = (Mathf.Max(wb.size.x, wb.size.z) * 0.28f) / Mathf.Max(1e-4f, ls.x);
            col.center = go.transform.InverseTransformPoint(wb.center);

            if (rigged)
            {
                var anim = go.AddComponent<FolkloreArchives.HumanWalkAnim>();
                anim.limbs = new[]
                {
                    new FolkloreArchives.HumanWalkAnim.Limb { bone = "thigh.L", phase =  1f },
                    new FolkloreArchives.HumanWalkAnim.Limb { bone = "thigh.R", phase = -1f },
                    new FolkloreArchives.HumanWalkAnim.Limb { bone = "arm.L",   phase = -1f },
                    new FolkloreArchives.HumanWalkAnim.Limb { bone = "arm.R",   phase =  1f },
                };
            }

            var wander = go.AddComponent<FolkloreArchives.FriendWander>();
            wander.minGroundY = MapLayout.RoadSurfaceHeight;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
