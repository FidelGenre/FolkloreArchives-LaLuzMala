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

        // Repite el segmento a lo largo de "path" (una curva ya ondulada, no 2-3 puntos
        // de control), separado "offset" metros hacia un costado.
        static void BuildFenceLine(Transform parent, Terrain t, GameObject seg, Vector2[] path, float offset)
        {
            if (path == null || path.Length < 2) return;

            // longitud real del segmento: mido los bounds del template una vez y uso el
            // eje horizontal más largo (auto-detecta si el FBX exportó el largo en X o
            // en Z, no asumo una convención fija del pack).
            var rends = seg.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            bool lengthOnX = b.size.x >= b.size.z;
            float segLen = Mathf.Max(0.5f, lengthOnX ? b.size.x : b.size.z);
            // yaw extra si el largo del mesh viene en X en vez de Z (adelante por defecto):
            // sin esto la valla quedaría de canto, mirando para el costado equivocado.
            float axisYawFix = lengthOnX ? 90f : 0f;

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

                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + axisYawFix;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SegmentFbx));
                go.name = "FenceSeg_" + segIdx++;
                go.transform.SetParent(parent);
                go.transform.position = BuilderUtils.Ground(t, finalXZ.x, finalXZ.y);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = rends[0].sharedMaterial;
                    r.sharedMaterials = arr;
                }
            }
        }
    }
}
