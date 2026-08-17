// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  HauntedNaturePackImporter.cs — owner: "agregame los arboles y bushes que
//  trae este archivo asi los puedo ir agregando a mano" (pack PS1
//  fortunaliquida "Haunted Dreams Nature", un .blend con 5 árboles + 6
//  arbustos low-poly con texturas de alfa recortado).
//
//  El .blend se exportó a FBX + PNG (con Blender headless) a
//  Assets/ExternalAssets/HauntedNature/{Models,Textures}. Este script HORNEA
//  la parte de Unity, SOLO (sin botón), vía AssetPostprocessor:
//   · Texturas: filtro Point (pixelado PS1), alpha is transparency, wrap clamp.
//   · Materiales URP: copa/arbustos = cutout (AlphaClip) DOBLE CARA (los planos
//     se ven de los dos lados); troncos = opaco normal.
//   · Prefabs listos en HauntedNature/Prefabs/ (HN_bush1.. HN_tree5) para
//     arrastrarlos al pincel "Paint Trees" (Edit Trees ▸ Add Tree) y pintarlos
//     a mano. NO se auto-colocan instancias.
//
//  Idempotente: si el prefab ya existe, no lo re-crea. Corre solo al importar
//  los FBX y también en cada recompilado (por si ya estaban importados).
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FolkloreArchives.MapGen
{
    public class HauntedNaturePackImporter : AssetPostprocessor
    {
        const string Root    = "Assets/ExternalAssets/HauntedNature";
        const string TexDir  = Root + "/Textures/";
        const string ModelDir= Root + "/Models/";
        const string MatDir  = Root + "/Materials/";
        const string PrefabDir=Root + "/Prefabs/";

        // owner: "dejalos a la misma altura [que los pinos]" + "aumenta los bushes en la misma
        // proporción" → base ×6. Después: "aumentame un 1.5 el tamaño de los arboles nuevos, los
        // otros dejalos como estan" → árboles ×9 (6×1.5), arbustos siguen en ×6. Escala en la RAÍZ
        // del prefab (mismo criterio que BigPine). HN_tree* usa HN_TreeScale; HN_bush* HN_BushScale.
        const float HN_TreeScale = 9f;   // 6 × 1.5
        const float HN_BushScale = 6f;

        // fbx -> slots en orden de submesh: (textura sin extensión, esTronco).
        // Sacado del .blend (material_slots): árboles = [tronco, copa]; arbustos = [copa].
        static readonly (string fbx, (string tex, bool trunk)[] slots)[] Defs =
        {
            ("bush1", new[]{("pngwing_com_7", false)}),
            ("bush2", new[]{("pngwing_com_6", false)}),
            ("bush3", new[]{("pngwing_com_5", false)}),
            ("bush4", new[]{("pngwing_com_4", false)}),
            ("bush5", new[]{("pngwing_com_9", false)}),
            ("bush6", new[]{("pngwing_com_8", false)}),
            ("tree1", new[]{("d0c668be7c4333515ed21590b5cbdf90", true), ("tree_png_images_pictures_becuo_20", false)}),
            ("tree2", new[]{("d0c668be7c4333515ed21590b5cbdf90", true), ("pngimg_com_tree_PNG92781", false)}),
            ("tree3", new[]{("d0c668be7c4333515ed21590b5cbdf90", true), ("pngimg_com_tree_PNG92703", false)}),
            ("tree4", new[]{("dfb", true), ("pngimg_com_tree_PNG92699", false)}),
            ("tree5", new[]{("dfb", true), ("meye_prunus_padus_s8592_225x300", false)}),
        };

        // ── Texturas: pixelado PS1 + alfa ──────────────────────────────────
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/HauntedNature/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Point;   // look PS1 (sin suavizar)
            ti.wrapMode = TextureWrapMode.Clamp; // son recortes sueltos, no tiles
            ti.mipmapEnabled = true;             // sin mips titila de lejos
        }

        // ── Al importar los FBX, armar materiales + prefabs (diferido para no
        //    reentrar en el pipeline de import) ─────────────────────────────
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var a in imported)
            {
                var p = a.Replace('\\', '/');
                if (p.Contains("/HauntedNature/Models/") && p.EndsWith(".fbx"))
                {
                    EditorApplication.delayCall -= Setup;
                    EditorApplication.delayCall += Setup;
                    return;
                }
            }
        }

        // Fallback: si los FBX ya estaban importados antes de existir este script,
        // corré el setup al recompilar (idempotente: no re-crea prefabs ni re-agrega prototipos).
        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "tree1.fbx") == null) return; // pack no importado
                Setup();
            };
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        // Bumpeá esto cuando cambie CÓMO se arman los prefabs (materiales/escala). El próximo load
        // reconstruye (overwrite, GUID intacto) los prefabs ya existentes con la lógica nueva.
        //  v2: materiales por NOMBRE (no por posición de submesh) — fix copas marrones tree3/4/5.
        //  v3: escala separada árboles (×9) vs arbustos (×6).
        //  v4: copas con shader Folklore/TreeWind (se mueven con el viento como los pinos PSX).
        const int PrefabVersion = 4;
        const string VersionPref = "Folklore_HN_PrefabVersion";

        static void Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "tree1.fbx") == null) return;
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");

            bool rebuildAll = EditorPrefs.GetInt(VersionPref, 0) < PrefabVersion;
            int built = 0;
            foreach (var d in Defs)
            {
                string prefabPath = PrefabDir + "HN_" + d.fbx + ".prefab";
                bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
                if (exists && !rebuildAll) continue; // ya está y no cambió la versión

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + d.fbx + ".fbx");
                if (model == null) continue;

                // material de TRONCO y de COPA por separado (no por índice de submesh)
                Material trunkMat = null, foliageMat = null;
                foreach (var s in d.slots)
                {
                    var mm = MakeMaterial(s.tex, s.trunk);
                    if (s.trunk) trunkMat = mm; else foliageMat = mm;
                }

                var inst = (GameObject)Object.Instantiate(model);
                inst.name = "HN_" + d.fbx;
                float scale = d.fbx.StartsWith("tree") ? HN_TreeScale : HN_BushScale;
                inst.transform.localScale = Vector3.one * scale;
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
                {
                    var src = r.sharedMaterials;
                    var arr = new Material[src.Length];
                    for (int i = 0; i < src.Length; i++)
                    {
                        // El exportador FBX NO garantiza el orden de submesh: en tree3/4/5 la copa
                        // (quad grande) quedó en el slot 0 y el tronco en el 1, al revés que tree1/2.
                        // Por eso asignamos por el NOMBRE del material original ("wood1"/"WOOD2" = tronco),
                        // no por posición. Así la corteza va SIEMPRE al tronco y la copa al quad.
                        string on = src[i] != null ? src[i].name.ToLowerInvariant() : "";
                        bool isWood = on.Contains("wood");
                        arr[i] = isWood ? (trunkMat ?? foliageMat) : (foliageMat ?? trunkMat);
                    }
                    r.sharedMaterials = arr;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, prefabPath); // overwrite si existía → GUID intacto
                Object.DestroyImmediate(inst);
                built++;
            }

            if (rebuildAll) EditorPrefs.SetInt(VersionPref, PrefabVersion);

            if (built > 0)
            {
                AssetDatabase.SaveAssets();
                // refrescar prototipos → las instancias ya pintadas toman materiales/escala nuevos
                foreach (var terr in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                    if (terr != null && terr.terrainData != null) { terr.terrainData.RefreshPrototypes(); terr.Flush(); }
                Debug.Log($"<color=lime>[HauntedNature] {built} prefab(s) HN armados/actualizados " +
                    $"(materiales por nombre, árboles ×{HN_TreeScale} / arbustos ×{HN_BushScale}). " +
                    "Instancias pintadas refrescadas. Guardá con Save Terrain.</color>");
            }
            EnsurePrototypes();
        }

        // Agrega los 11 prefabs HN como prototipos de árbol (pincel Paint Trees) a TODOS los
        // terrenos, UNA sola vez por máquina (guard EditorPrefs) — así aparecen para pintar sin
        // tener que sumarlos a mano, pero si después borrás alguno del pincel no te lo re-agrega.
        // Agregar prototipos NO afecta las instancias ya pintadas (índices existentes intactos).
        const string ProtosAddedPref = "Folklore_HN_ProtosAdded";
        static void EnsurePrototypes()
        {
            if (EditorPrefs.GetBool(ProtosAddedPref, false)) return;

            var hn = new List<GameObject>();
            foreach (var d in Defs)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "HN_" + d.fbx + ".prefab");
                if (pf != null) hn.Add(pf);
            }
            if (hn.Count < Defs.Length) return; // todavía no están todos los prefabs; reintenta el próximo load

            var allTerr = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (allTerr.Length == 0) return; // escena/terreno no cargado aún; no marco el guard, reintenta

            int added = 0, terrs = 0;
            foreach (var terr in allTerr)
            {
                var td = terr != null ? terr.terrainData : null;
                if (td == null) continue;
                var protos = new List<TreePrototype>(td.treePrototypes);
                bool changed = false;
                foreach (var pf in hn)
                {
                    bool has = false;
                    foreach (var p in protos) if (p != null && p.prefab == pf) { has = true; break; }
                    if (!has) { protos.Add(new TreePrototype { prefab = pf, bendFactor = 0f }); changed = true; added++; }
                }
                if (changed) { td.treePrototypes = protos.ToArray(); EditorUtility.SetDirty(td); terrs++; }
            }
            EditorPrefs.SetBool(ProtosAddedPref, true);
            if (added > 0)
                Debug.Log($"<color=lime>[HauntedNature] {added} prototipo(s) HN agregados al pincel de {terrs} " +
                    "terreno(s). Ya aparecen en Paint Trees para pintar. Guardá con Save Terrain para hornearlo.</color>");
        }

        // Material del pack. TRONCO = URP/Lit opaco. COPA/arbusto = shader "Folklore/TreeWind"
        // (cutout + doble cara + BALANCEO por viento, el mismo que usan las copas de los pinos PSX)
        // para que los HN se muevan con el viento. Si el shader no estuviera, cae a URP/Lit cutout.
        static Material MakeMaterial(string tex, bool trunk)
        {
            string path = MatDir + (trunk ? "HN_trunk_" : "HN_leaf_") + tex + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;

            var wind = trunk ? null : Shader.Find("Folklore/TreeWind");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var sh = wind != null ? wind : lit;
            if (isNew) m = new Material(sh);
            else if (sh != null && m.shader != sh) m.shader = sh;

            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex + ".png");
            if (t != null) m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", Color.white);

            if (!trunk && wind != null)
            {
                // shader de viento: cutout y doble cara ya vienen en el shader; solo props
                if (m.HasProperty("_Cutoff"))       m.SetFloat("_Cutoff", 0.5f);
                if (m.HasProperty("_WindStrength"))  m.SetFloat("_WindStrength", 0.68f); // = copas PSX
                if (m.HasProperty("_WindSpeed"))     m.SetFloat("_WindSpeed", 1.0f);
            }
            else if (!trunk)
            {
                // fallback SIN el shader de viento: URP/Lit con cutout + doble cara (sin balanceo)
                m.SetFloat("_Surface", 0f); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", (float)CullMode.Off);
                m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                // tronco opaco
                m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f);
                m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)CullMode.Back); m.renderQueue = -1;
            }

            if (isNew) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }
    }
}
