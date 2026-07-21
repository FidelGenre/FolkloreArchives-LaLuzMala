// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  FenceBuilder.cs — valla de madera baja corriendo a lo largo de
//  los caminos (owner: "ponla por todo el camino desde la ruta hasta
//  el campamento y desde el camino del campamento hasta la casa de
//  la vieja"). Repite un segmento recto ("wooden_fence_open") a lo
//  largo de la curva REAL del camino (no la línea recta entre
//  puntos de control), separado unos metros a un costado.
//
//  Crédito: "PSX style modular walls & fences" by valsekamerplant
//  (itch.io, CC0 / dominio público).
//
//  CÓMO PRENDER/APAGAR (owner pidió que quede documentado):
//   - Apagar TODO: MapLayout.BuildFences = false, regenerar.
//   - Apagar de un camino puntual: comentar esa línea dentro de
//     Build() más abajo (cada camino es una llamada separada a
//     BuildFenceLine).
//   - Agregar a un camino nuevo: sumar una línea
//     BuildFenceLine(group, t, MapLayout.ELCAMINO, offset) — sirve
//     cualquier Vector2[] ya ondulado (un ExtraTrails, un PathA, etc.).
//   - Ya generado el mapa y solo querés sacarlas rápido sin
//     regenerar: buscá el grupo "WoodenFences" en la escena (adentro
//     de FOLKLORE_MAP) y borralo a mano — es autocontenido, no hace
//     falta tocar nada más.
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class FenceBuilder
    {
        const string ModelDir = "Assets/ExternalAssets/WoodenFence/models/";
        const string Tex      = "Assets/ExternalAssets/WoodenFence/textures/low_wooden_wall.jpg";
        const string SegmentFbx = ModelDir + "wooden_fence_open.fbx";

        public static void Build(Transform parent, Terrain t)
        {
            if (!MapLayout.BuildFences) return;

            var seg = LoadSegment();
            if (seg == null) { Debug.LogWarning("[FenceBuilder] no encontré " + SegmentFbx + " — bajá/descomprimí el pack primero."); return; }

            var group = BuilderUtils.Group(parent, "WoodenFences", Vector3.zero);

            // ── acá se decide QUÉ caminos llevan valla — comentar una línea la saca ──
            BuildFenceLine(group, t, seg, MapLayout.DirtRoad,     MapLayout.FenceOffsetDirtRoad);   // ruta → campamento
            BuildFenceLine(group, t, seg, MapLayout.Camino10Path, MapLayout.FenceOffsetCamino10);   // campamento → casa de la vieja

            Object.DestroyImmediate(seg); // era solo la plantilla para medir/clonar
        }

        // Carga wooden_fence_open.fbx UNA vez, lo textura y lo deja listo para clonar
        // (no queda en la escena — Build() lo destruye al final).
        static GameObject LoadSegment()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentFbx);
            if (src == null) return null;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = "FenceSegmentTemplate";

            var tex = LoadTex();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            string matPath = "Assets/Settings/WoodenFence.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = mat;
                r.sharedMaterials = arr;
            }
            return inst;
        }

        static Texture2D LoadTex()
        {
            var imp = AssetImporter.GetAtPath(Tex) as TextureImporter;
            if (imp != null && imp.filterMode == FilterMode.Point)
            {
                // este pack no es PSX pixelado -- filtro normal (bilinear), no Point.
                imp.filterMode = FilterMode.Bilinear;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Tex);
        }

        // Altura real objetivo (owner: "deberian ser mas grandes tambien y sobresalir"
        // -- la primera pasada no escalaba NADA, quedaban al tamaño crudo de
        // importación del FBX, chiquitos). Una valla baja de campo ronda ~1.1-1.3m.
        const float FenceTargetHeight = 1.2f;
        // Ajuste de yaw a mano, en grados (0/90/180/270), por si después de ver el
        // resultado en el Editor la valla sigue mirando para el lado que no es --
        // más rápido que tocar la lógica de detección de ejes.
        const float FenceYawTweak = 0f;

        // Repite el segmento a lo largo de "path" (una curva ya ondulada, no 2-3 puntos
        // de control), separado "offset" metros hacia un costado.
        static void BuildFenceLine(Transform parent, Terrain t, GameObject seg, Vector2[] path, float offset)
        {
            if (path == null || path.Length < 2) return;

            // mido los bounds CRUDOS del template (sin escalar) para saber la
            // proporción largo/alto real del mesh tal como vino del FBX.
            var rends = seg.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds raw = rends[0].bounds;
            foreach (var r in rends) raw.Encapsulate(r.bounds);

            // escala uniforme para que la ALTURA quede en FenceTargetHeight -- así el
            // segmento sobresale del pasto en vez de quedar diminuto (owner: "deberian
            // ser mas grandes... y sobresalir").
            float scale = FenceTargetHeight / Mathf.Max(0.001f, raw.size.y);

            // eje horizontal más largo del mesh CRUDO (auto-detecta si el FBX exportó
            // el largo en X o en Z, no asumo una convención fija del pack) -- ya
            // escalado, es la distancia real que ocupa cada segmento a lo largo del camino.
            bool lengthOnX = raw.size.x >= raw.size.z;
            float segLen = Mathf.Max(0.3f, (lengthOnX ? raw.size.x : raw.size.z) * scale);
            // yaw extra si el largo del mesh viene en X en vez de Z (adelante por
            // defecto): sin esto la valla queda de canto en vez de a lo largo del
            // camino. (Owner: "estan metidos de lado" -- la primera pasada tenía este
            // ajuste invertido; probar este signo primero.)
            float axisYawFix = lengthOnX ? 0f : 90f;

            float total = 0f;
            for (int i = 0; i < path.Length - 1; i++) total += Vector2.Distance(path[i], path[i + 1]);

            int count = Mathf.FloorToInt(total / segLen);
            if (count < 1) return;

            float acc = 0f;
            int segIdx = 0, pt = 0;
            for (int i = 0; i < count; i++)
            {
                float targetDist = i * segLen + segLen * 0.5f; // centro de CADA segmento
                // avanzar por la polilínea hasta encontrar el punto que contiene targetDist
                while (pt < path.Length - 2 && acc + Vector2.Distance(path[pt], path[pt + 1]) < targetDist)
                {
                    acc += Vector2.Distance(path[pt], path[pt + 1]);
                    pt++;
                }
                Vector2 a = path[pt], c = path[Mathf.Min(pt + 1, path.Length - 1)];
                float segFullLen = Mathf.Max(0.0001f, Vector2.Distance(a, c));
                float distIntoSeg = Mathf.Clamp01((targetDist - acc) / segFullLen);
                Vector2 center = Vector2.Lerp(a, c, distIntoSeg);
                Vector2 dir = (c - a).sqrMagnitude > 0.0001f ? (c - a).normalized : Vector2.up;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                Vector2 finalXZ = center + perp * offset;

                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + axisYawFix + FenceYawTweak;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SegmentFbx));
                go.name = "FenceSeg_" + segIdx++;
                go.transform.SetParent(parent);
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                float groundY = BuilderUtils.Ground(t, finalXZ.x, finalXZ.y).y;
                go.transform.position = new Vector3(finalXZ.x, groundY, finalXZ.y);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = rends[0].sharedMaterial;
                    r.sharedMaterials = arr;
                }
                // replantar la BASE real en el piso (los bounds cambiaron con la
                // escala/rotación nuevas) -- si no, con un mesh chico previamente
                // sumergido, seguía quedando medio metido en el pasto.
                var goRends = go.GetComponentsInChildren<Renderer>();
                if (goRends.Length > 0)
                {
                    Bounds gb = goRends[0].bounds;
                    foreach (var r in goRends) gb.Encapsulate(r.bounds);
                    go.transform.position += new Vector3(0f, groundY - gb.min.y, 0f);
                }
            }
        }
    }
}
