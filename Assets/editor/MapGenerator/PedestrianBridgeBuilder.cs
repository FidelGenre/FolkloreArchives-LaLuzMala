// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PedestrianBridgeBuilder.cs — puente COLGANTE de sogas (peatonal)
//  que cruza el río al lado del campamento, del lado oeste (campamento)
//  al este. Modelo: Procedural Bridges Asset Pack (NOX, itch.io),
//  horneado a ~33m de largo y exportado a FBX desde Blender.
//
//  El río corre en x≈301 a la altura del campamento (z≈232, ver
//  MapLayout.Campsite / RiverControls). El puente cruza en X.
//
//  Ajuste fino (lo afinamos viéndolo): CrossX/CrossZ (dónde), YawDeg
//  (orientación), YOffset (altura), Scale (si el FBX entra chico/grande).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class PedestrianBridgeBuilder
    {
        const string FbxPath = "Assets/ExternalAssets/RopeBridge/RopeBridge.fbx";

        // ── Ajustes (tweak y regenerar) ─────────────────────────────────────
        const float YawDeg   = 0f;     // orientación (cruza en X; ajustar si hace falta)
        const float YOffset  = 0f;     // subir/bajar el tablero respecto de la orilla
        const float Scale    = 1f;     // si entra chico (FBX en cm) subir a 100
        const bool  AddCollider = true;

        public static void Build(Transform parent, Terrain terrain)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (prefab == null)
            {
                Debug.LogWarning("[RopeBridge] no encontré el FBX en " + FbxPath +
                                 " (¿Unity terminó de importarlo?). No pongo el puente peatonal.");
                return;
            }

            // Cruce EXACTO: donde el Camino14 (campo de caza ↔ mirador este) cruza el río
            // real — mismo cálculo que usaba el FootBridge viejo, ahora reemplazado por este.
            Vector2 crossPt = MapLayout.Camino14[1];
            float bestDist = float.MaxValue;
            for (int i = 0; i <= 60; i++)
            {
                Vector2 s = Vector2.Lerp(MapLayout.Camino14[1], MapLayout.Camino14[2], i / 60f);
                float d = BuilderUtils.DistToPolyline(s, MapLayout.River);
                if (d < bestDist) { bestDist = d; crossPt = s; }
            }
            float bx = crossPt.x, bz = crossPt.y, halfLen = 15f;
            float wy = terrain != null ? terrain.SampleHeight(new Vector3(bx - halfLen, 0f, bz)) : 18f;
            float ey = terrain != null ? terrain.SampleHeight(new Vector3(bx + halfLen, 0f, bz)) : 18f;
            float deckY = Mathf.Max(wy, ey) + (terrain != null ? terrain.transform.position.y : 0f) + YOffset;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "RopeBridge_Pedestrian";
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.position = new Vector3(bx, deckY, bz);
            go.transform.rotation = Quaternion.Euler(0f, YawDeg, 0f);
            if (Scale != 1f) go.transform.localScale = Vector3.one * Scale;

            ConvertToUrp(go);                               // built-in → URP (anti-magenta)
            if (AddCollider) AddColliders(go);
            BuilderUtils.MarkStaticRecursive(go.transform);

            Debug.Log("<color=lime>[RopeBridge] Puente colgante peatonal puesto cruzando el río en el campamento. " +
                      "Si quedó corrido/torcido, avisá y ajusto CrossX/CrossZ/YawDeg/YOffset/Scale.</color>");
        }

        // Materiales built-in → URP (si el FBX vino con Standard, evita el magenta).
        static void ConvertToUrp(GameObject inst)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;
            // Cargo las texturas del PROYECTO (Unity no extrae bien las embebidas del FBX,
            // por eso las tablas salían grises). Se asignan por nombre de material.
            var ropeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/RopeBridge/Textures/RopeBridge_Rope.png");
            var woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/RopeBridge/Textures/RopeBridge_Planks.png");
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.shader != urpLit) m.shader = urpLit;
                    var tex = m.name.ToLowerInvariant().Contains("plank") ? woodTex : ropeTex; // M_Planks → madera
                    if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    if (tex != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    // DOBLE CARA (por si las tablas tienen normales invertidas) + mate.
                    if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                }
            }
        }

        static void AddColliders(GameObject inst)
        {
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }
        }
    }
}
