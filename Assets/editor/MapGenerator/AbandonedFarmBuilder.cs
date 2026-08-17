// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  AbandonedFarmBuilder.cs — mete la "granja abandonada" (asset PSX
//  de mcpato, itch.io) en el lugar de la casa de la vieja. El FBX es
//  una ESCENA ENTERA horneada (478 objetos: casas/galpón, molino,
//  tanque de agua, tractor, heno, cultivos, cercas, muebles…).
//
//  - Se instancia ENTERO (478 piezas). Materiales built-in → URP por las
//    dudas (evita magenta en URP) + MeshCollider a cada pieza.
//  - UBICACIÓN, BORRADOS ("ocultar = borrar", incluido el terreno interno
//    del FBX que choca con nuestro terreno) y DUPLICADOS los maneja ahora
//    "Save Map Layout" (MapLayoutPersistence), UNIFICADO con el resto del
//    mapa — se aplica al final de Generate(). La granja ya NO tiene sistema
//    de layout propio (se eliminó farm_layout.bytes + ApplyFarmLayout +
//    los menús Guardar/Resetear Transform de la Granja).
//
//  VOLVER ATRÁS al galpón viejo: HouseBuilder UseAbandonedFarm=false.
//  Resetear el layout de la granja: Tools ▸ Clear Map Layout (afecta a
//    todo el mapa) o editá Assets/_FolkloreArchives/layout_FullMap.json.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class AbandonedFarmBuilder
    {
        const string FbxPath    = "Assets/ExternalAssets/AbandonedFarm/AbandonedFarm.fbx";
        const bool AddFarmColliders = true;   // MeshCollider en cada pieza (que no se atraviese)

        // La UBICACIÓN, los BORRADOS ("ocultar = borrar", incluido el terreno interno del
        // FBX) y los DUPLICADOS de la granja los maneja ahora "Save Map Layout"
        // (MapLayoutPersistence), unificado con el resto del mapa. Se sacó el sistema de
        // layout propio de la granja (farm_layout.bytes + ApplyFarmLayout + Guardar/Resetear).

        public static void Build(Transform parent, Terrain terrain)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Farm] no encontré el FBX en " + FbxPath +
                                 " (¿Unity terminó de importarlo?). No pongo la granja.");
                return;
            }

            var group = BuilderUtils.Group(parent, "AbandonedFarm", Vector3.zero);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            inst.name = "FarmModel";
            // Desconectar del prefab (FBX): Unity NO permite DESTRUIR hijos de una
            // instancia de prefab, y necesitamos eliminar el terreno del asset y los
            // objetos que el owner oculta ("ocultar = borrar"). Tras el unpack son
            // GameObjects normales y DestroyImmediate funciona.
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // La granja se construye COMPLETA (478 piezas). Su UBICACIÓN, los BORRADOS
            // ("ocultar = borrar", incluido el terreno interno del FBX) y los DUPLICADOS
            // los maneja ahora "Save Map Layout" (MapLayoutPersistence), unificado con el
            // resto del mapa — se aplica al final de Generate(). Acá solo dejamos la granja
            // en una posición base razonable por si todavía no hay layout guardado.
            Vector2 farmC = MapLayout.OldLadyHouseCenter;
            float farmY = terrain != null
                ? terrain.SampleHeight(new Vector3(farmC.x, 0f, farmC.y)) + terrain.transform.position.y
                : 20f;
            group.position = new Vector3(farmC.x, farmY, farmC.y);

            ConvertToUrp(inst);                              // built-in → URP (anti-magenta)
            if (AddFarmColliders) AddColliders(inst);        // MeshCollider a cada pieza (no atravesar)
            BuilderUtils.MarkStaticRecursive(group);

            Debug.Log("<color=lime>[Farm] Granja abandonada instanciada (completa). Reacomodá/ocultá/duplicá " +
                      "piezas a mano y clickeá Tools ▸ Folklore Archives ▸ Save Map Layout (ocultar = borrar).</color>");
        }

        // ── Colliders: un MeshCollider por pieza con malla, para que el jugador no
        //    atraviese la granja. Estático + no-convexo (correcto para entorno fijo). ──
        static void AddColliders(GameObject inst)
        {
            int n = 0;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.GetComponent<Collider>() != null) continue; // ya tiene
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                n++;
            }
            if (n > 0) Debug.Log($"[Farm] {n} colliders (MeshCollider) agregados a la granja.");
        }

        // ── Materiales built-in → URP (evita magenta si alguno quedó Standard) ──
        static readonly Dictionary<Material, Material> _urpCache = new Dictionary<Material, Material>();
        static void ConvertToUrp(GameObject inst)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;
            int fixedN = 0;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                var outM = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) outM[i] = ToUrp(src[i], urpLit, ref fixedN);
                r.sharedMaterials = outM;
            }
            if (fixedN > 0) Debug.Log($"[Farm] materiales convertidos a URP: {fixedN}");
        }

        static Material ToUrp(Material s, Shader urpLit, ref int fixedN)
        {
            if (s == null) return null;
            bool alpha = MainTexHasAlpha(s);
            // Ya es URP y NO necesita recorte de alpha → dejar tal cual. (Si tiene alpha
            // pero está opaco, igual le hago una copia con alpha clip más abajo.)
            if (s.shader == urpLit && (!alpha || s.IsKeywordEnabled("_ALPHATEST_ON"))) return s;
            if (_urpCache.TryGetValue(s, out var cached)) return cached;

            var m = new Material(urpLit);
            Texture main = (s.HasProperty("_MainTex") ? s.GetTexture("_MainTex") : null)
                        ?? (s.HasProperty("_BaseMap") ? s.GetTexture("_BaseMap") : null);
            if (main != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", main);
            Color col = s.HasProperty("_Color") ? s.GetColor("_Color")
                      : (s.HasProperty("_BaseColor") ? s.GetColor("_BaseColor") : Color.white);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (s.HasProperty("_BumpMap"))
            {
                var nrm = s.GetTexture("_BumpMap");
                if (nrm != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);

            // FOLLAJE/CULTIVOS: si la textura tiene ALPHA, activar recorte (alpha clip) +
            // doble cara. Sin esto, los planos cruzados de las plantas se ven como
            // rectángulos sólidos ("en 2D") en vez de recortarse a la forma de la planta.
            if (alpha)
            {
                m.EnableKeyword("_ALPHATEST_ON");
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.4f);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // doble cara (ambos lados de la cruz)
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            _urpCache[s] = m;
            fixedN++;
            return m;
        }

        // ¿la textura principal del material tiene canal alpha (transparencia)? → follaje.
        static bool MainTexHasAlpha(Material s)
        {
            if (s == null) return false;
            Texture t = (s.HasProperty("_MainTex") ? s.GetTexture("_MainTex") : null)
                     ?? (s.HasProperty("_BaseMap") ? s.GetTexture("_BaseMap") : null);
            if (t == null) return false;
            var path = AssetDatabase.GetAssetPath(t);
            if (string.IsNullOrEmpty(path)) return false;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            return imp != null && imp.DoesSourceTextureHaveAlpha();
        }
    }
}
