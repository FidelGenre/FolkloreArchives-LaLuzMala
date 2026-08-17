// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  YpfNpcImporter.cs — owner: importa 2 personajes PSX bajados de Sketchfab
//  para la YPF:
//   · Playero "Richard" (ps1low-poly-human-richard) -> el que atiende la estación.
//   · "CreepyOldMan" (cursed-lo-fi-nightmare-fuel) -> viejo que sale del baño.
//  Los .blend/.obj + PNG ya están en Assets/ExternalAssets/{Playero_Richard,
//  CreepyOldMan}. Este script (igual criterio que HauntedNaturePackImporter):
//   · Texturas: filtro Point (pixelado PS1), mipmaps, alpha is transparency.
//   · Material URP/Lit opaco con la textura.
//   · Prefab listo al lado del modelo, para arrastrar a la escena.
//  Idempotente: corre al importar y en cada load; no re-crea si ya existe (salvo
//  que se suba PrefabVersion).
// ============================================================
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FolkloreArchives.MapGen
{
    public class YpfNpcImporter : AssetPostprocessor
    {
        const string PlayeroDir = "Assets/ExternalAssets/Playero_Richard/";
        const string CreepyDir  = "Assets/ExternalAssets/CreepyOldMan/";

        // (modelo, textura, prefab, nombre)
        static readonly (string model, string tex, string prefab, string name)[] Defs =
        {
            (PlayeroDir + "Richard.fbx",     PlayeroDir + "richard_tex.png", PlayeroDir + "Playero_Richard.prefab", "Playero_Richard"),
            (CreepyDir  + "CreepyOldMan_rigged.fbx", CreepyDir  + "creepy_tex.png",  CreepyDir  + "CreepyOldMan.prefab",    "CreepyOldMan"),
        };

        const int PrefabVersion = 2; // v2: viejo desde FBX riggeado (auto-rig de Blender)
        const string VersionPref = "Folklore_YpfNpc_PrefabVersion";

        // ── Texturas: pixelado PS1 ──────────────────────────────────────────
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!(p.Contains("/Playero_Richard/") || p.Contains("/CreepyOldMan/"))) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Point;    // look PS1 (sin suavizar)
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.mipmapEnabled = true;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var a in imported)
            {
                var p = a.Replace('\\', '/');
                if (p.Contains("/Playero_Richard/") || p.Contains("/CreepyOldMan/"))
                {
                    EditorApplication.delayCall -= Setup;
                    EditorApplication.delayCall += Setup;
                    return;
                }
            }
        }

        [InitializeOnLoadMethod]
        static void OnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Defs[0].model) == null) return;
                Setup();
            };
        }

        static void Setup()
        {
            bool rebuild = EditorPrefs.GetInt(VersionPref, 0) < PrefabVersion;
            int built = 0;

            foreach (var d in Defs)
            {
                bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(d.prefab) != null;
                if (exists && !rebuild) continue;

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(d.model);
                if (model == null) continue;

                var mat = MakeMaterial(d.name, d.tex);

                var inst = (GameObject)Object.Instantiate(model);
                inst.name = d.name;
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var arr = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                    r.sharedMaterials = arr;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, d.prefab);
                Object.DestroyImmediate(inst);
                built++;
            }

            if (rebuild) EditorPrefs.SetInt(VersionPref, PrefabVersion);
            if (built > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=lime>[YpfNpc] {built} prefab(s) armados: Playero_Richard / CreepyOldMan. " +
                          "Arrastralos a la escena (playero en los surtidores, viejo en el baño).</color>");
            }
        }

        // URP/Lit opaco con la textura, matte (PSX). Si no hay URP/Lit, cae a Standard.
        static Material MakeMaterial(string name, string texPath)
        {
            string matPath = texPath.Substring(0, texPath.LastIndexOf('/') + 1) + "MAT_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = m == null;

            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (isNew) m = new Material(sh);
            else if (sh != null && m.shader != sh) m.shader = sh;

            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (t != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f); // matte, sin brillo
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off); // doble cara (modelos low-poly a veces vienen con normales invertidas)

            if (isNew) AssetDatabase.CreateAsset(m, matPath);
            else EditorUtility.SetDirty(m);
            return m;
        }
    }
}
