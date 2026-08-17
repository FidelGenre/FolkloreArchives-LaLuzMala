// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  ElectricPoleBuilder.cs — owner: "postes de luz a lo largo de toda la ruta,
//  de inicio a fin". Asset: "Electric Pole" by notsospecialgames (itch.io) —
//  ver ASSET_CREDITS.md. GLB (electric_pole.glb), Unity lo importa nativo.
//
//  Lee la línea central de la ruta DIRECTO de las piezas de asfalto vivas de la
//  escena (base "PavedRoad_Surface" + extensiones "PavedRoad_Surface (N)" que el
//  owner agregó a mano), así cubre TODA la ruta incluida la extensión. Por eso
//  corre en MapGenerator DESPUÉS de ApplySavedLayout (recién ahí existen los
//  duplicados de la extensión). Planta un poste cada Spacing metros sobre UN
//  hombro (SideOffset del centro), apoyado en el piso, con el travesaño cruzado a
//  la ruta. Postes Y cables se generan procedurales UNA VEZ; después el owner los
//  acomoda a mano y el grupo "PostesDeLuz" SOBREVIVE al Generate (lo rescata
//  DeleteMap y se re-parentea tal cual — ver MapGenerator). O sea: este Build() solo
//  corre la PRIMERA vez (o si el owner borra el grupo para rehacerlo). No dependen del
//  layout por índice (era frágil al mover el grupo entero). Ver DEV_LOG.
// ============================================================
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class ElectricPoleBuilder
    {
        const string PoleGlb   = "Assets/ExternalAssets/ElectricPole/electric_pole.glb";
        const string WireGlb   = "Assets/ExternalAssets/ElectricPole/wire.glb";
        const float  Spacing    = 26f;   // separación entre postes (≈ largo del cable del asset, 24m)
        const float  SideOffset = 11f;   // distancia del CENTRO de la ruta al poste (hombro, fuera del asfalto)
        const float  PoleHeight = 9f;    // alto del poste en metros
        const float  AttachDrop = 0.8f;  // cuánto abajo de la punta del poste engancha el cable (≈ travesaño)
        const float  WireSpread = 1.9f;  // medio-ancho: 2 cables, uno en cada punta del travesaño (medido ±1.95 m)

        static readonly Regex RoadRx = new Regex(@"^PavedRoad_Surface( \(\d+\))?$");

        public static void Build(Transform mapRoot)
        {
            var poleModel = AssetDatabase.LoadAssetAtPath<GameObject>(PoleGlb);
            if (poleModel == null)
            {
                Debug.LogWarning("[Postes] falta " + PoleGlb + " — hacé foco en Unity para que importe el GLB y regenerá.");
                return;
            }
            var centerline = ReadRoadCenterline(mapRoot);
            if (centerline.Count < 2) { Debug.LogWarning("[Postes] no encontré piezas de asfalto en la escena."); return; }

            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            var group = new GameObject("PostesDeLuz");
            group.transform.SetParent(mapRoot);

            // enganches IZQUIERDO y DERECHO del travesaño de cada poste (para 2 cables)
            var attachL = new List<Vector3>();
            var attachR = new List<Vector3>();
            int placed = 0;
            float acc = Spacing; // arranca colocando uno en el primer punto
            for (int i = 1; i < centerline.Count; i++)
            {
                Vector3 a = centerline[i - 1], b = centerline[i];
                acc += Vector3.Distance(a, b);
                if (acc < Spacing) continue;
                acc = 0f;

                Vector3 fwd = b - a; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) continue;
                fwd.Normalize();
                Vector3 perp = new Vector3(-fwd.z, 0f, fwd.x);       // ⊥ a la ruta = dirección del travesaño
                Vector3 pos = b + perp * SideOffset;
                float y = GroundY(terrains, pos.x, pos.z, b.y);
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg; // local +Z a lo largo de la ruta → travesaño cruzado
                Vector3 center = SpawnPole(poleModel, group.transform, new Vector3(pos.x, y, pos.z), yaw, placed++);
                attachL.Add(center + perp * WireSpread);
                attachR.Add(center - perp * WireSpread);
            }
            Debug.Log($"<color=cyan>[Postes] {placed} postes de luz a lo largo de la ruta (asset: Electric Pole by notsospecialgames — ver ASSET_CREDITS.md).</color>");

            // DOS CABLES entre CADA par de postes vecinos (uno en cada punta del travesaño), sin
            // saltear ninguno → cables en TODOS los postes. Solo se saltea un salto absurdo (>150 m,
            // ej. una discontinuidad real de la ruta) para no tender un cable gigante cruzando el vacío.
            var wireModel = AssetDatabase.LoadAssetAtPath<GameObject>(WireGlb);
            if (wireModel != null)
            {
                int wires = 0, gaps = 0;
                for (int i = 0; i + 1 < attachL.Count; i++)
                {
                    if (Vector3.Distance(attachL[i], attachL[i + 1]) > 150f) { gaps++; continue; }
                    // NOMBRE ESTABLE por tramo+lado ("Cable_5_L") — no un contador corrido — para que el
                    // layout guardado le caiga SIEMPRE al mismo cable al regenerar (si el owner lo movió a
                    // mano). Si cambia la cantidad de postes, un nombre viejo simplemente no matchea y se
                    // ignora (no descoloca a otro). Ver [[feedback_assets_layout_saveable]].
                    SpawnWire(wireModel, group.transform, attachL[i], attachL[i + 1], $"Cable_{i}_L");
                    SpawnWire(wireModel, group.transform, attachR[i], attachR[i + 1], $"Cable_{i}_R");
                    wires += 2;
                }
                Debug.Log($"<color=cyan>[Postes] {wires} cables tendidos ({gaps} tramos salteados por salto >150 m).</color>");
            }
        }

        // Línea central de TODA la ruta (base + extensiones), leída de la malla real de cada pieza
        // (RoadsideBuilder arma 5 vértices por sección transversal; el índice i*5+1 es el centro).
        static List<Vector3> ReadRoadCenterline(Transform mapRoot)
        {
            var pieces = new List<(float startX, List<Vector3> line)>();
            foreach (Transform child in mapRoot)
            {
                if (!RoadRx.IsMatch(child.name)) continue;
                var mf = child.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var verts = mf.sharedMesh.vertices;
                var line = new List<Vector3>(verts.Length / 5 + 1);
                for (int i = 1; i < verts.Length; i += 5) line.Add(child.TransformPoint(verts[i]));
                if (line.Count < 2) continue;
                if (line[0].x > line[line.Count - 1].x) line.Reverse(); // de X bajo (inicio) a X alto (fin)
                pieces.Add((line[0].x, line));
            }
            pieces.Sort((p, q) => p.startX.CompareTo(q.startX)); // inicio primero
            var pts = new List<Vector3>();
            foreach (var pc in pieces) pts.AddRange(pc.line);
            return pts;
        }

        static float GroundY(Terrain[] terrains, float x, float z, float fallback)
        {
            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 tp = t.transform.position; Vector3 sz = t.terrainData.size;
                if (x >= tp.x && x <= tp.x + sz.x && z >= tp.z && z <= tp.z + sz.z)
                    return t.SampleHeight(new Vector3(x, 0f, z)) + tp.y;
            }
            return fallback; // sin terreno bajo el poste → altura de la ruta
        }

        static Vector3 SpawnPole(GameObject model, Transform parent, Vector3 groundPos, float yaw, int idx)
        {
            var inst = (GameObject)Object.Instantiate(model, parent);
            inst.name = "PosteLuz_" + idx;
            Quaternion baked = inst.transform.rotation;                 // el GLB puede traer su propia rotación → se preserva
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * baked;
            inst.transform.localScale = Vector3.one;
            var b = PoleBounds(inst);
            if (b.size.y > 0.001f) { inst.transform.localScale = Vector3.one * (PoleHeight / b.size.y); b = PoleBounds(inst); }
            inst.transform.position = groundPos;
            b = PoleBounds(inst);
            inst.transform.position += new Vector3(0f, groundPos.y - b.min.y, 0f); // apoyar la base en el piso
            b = PoleBounds(inst);
            return new Vector3(groundPos.x, b.max.y - AttachDrop, groundPos.z); // enganche cerca de la punta
        }

        // Tiende un cable (wire.glb del asset) entre a y b: lo orienta a lo largo de a→b y le escala
        // el eje largo para calzar la distancia. El owner lo acomoda a mano.
        static void SpawnWire(GameObject model, Transform parent, Vector3 a, Vector3 b, string name)
        {
            var w = (GameObject)Object.Instantiate(model, parent);
            w.name = name;
            w.transform.rotation = Quaternion.identity;
            w.transform.position = Vector3.zero;
            w.transform.localScale = Vector3.one;
            float nativeLen = PoleBounds(w).size.z;
            if (nativeLen < 0.01f) nativeLen = 1f;
            Vector3 dir = b - a;
            float dist = dir.magnitude;
            if (dist < 0.01f) { Object.DestroyImmediate(w); return; }
            Vector3 mid = (a + b) * 0.5f;
            w.transform.rotation = Quaternion.LookRotation(dir / dist, Vector3.up);
            w.transform.localScale = new Vector3(1f, 1f, dist / nativeLen);
            w.transform.position = mid;
            var wb = PoleBounds(w);
            w.transform.position += mid - wb.center;
        }

        static Bounds PoleBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
    }
}
