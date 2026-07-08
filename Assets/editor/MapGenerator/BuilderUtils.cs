// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  BuilderUtils.cs — shared helpers for all builders.
//  Paste into:  Assets/Editor/MapGenerator/BuilderUtils.cs
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FolkloreArchives.MapGen
{
    public static class BuilderUtils
    {
        public static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_FolkloreArchives"))
                AssetDatabase.CreateFolder("Assets", "_FolkloreArchives");
            if (!AssetDatabase.IsValidFolder(MapLayout.GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/_FolkloreArchives", "Generated");
        }

        /// A loose texture used as a normal map (not part of an FBX's own material
        /// import) comes in tagged as a regular sRGB texture by default. Force it
        /// into "Normal map" mode so shaders decode it correctly.
        public static Texture2D LoadAsNormalMap(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // Cache of Unity's built-in primitive meshes (unit cube/sphere/cylinder etc,
        // local space roughly -0.5..0.5) - CreatePrimitive() actually returns a shared
        // mesh instance already, but going through a real GameObject+DestroyImmediate
        // once per type (instead of once per placement) avoids needlessly spawning and
        // killing thousands of temporary GameObjects just to grab their mesh reference.
        static readonly Dictionary<PrimitiveType, Mesh> _primMeshCache = new Dictionary<PrimitiveType, Mesh>();
        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (_primMeshCache.TryGetValue(type, out var cached) && cached != null) return cached;
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            _primMeshCache[type] = mesh;
            return mesh;
        }

        /// Marks a whole subtree static (Unity's automatic static batching only
        /// applies per-GameObject, not inherited from a parent - so a handful of
        /// fixed set-dressing props like LandmarkBuilder's shacks/campfires/tents
        /// need every renderer marked, not just the root). Only use on subtrees that
        /// never move at runtime (do NOT call this on the player or anything with a
        /// Rigidbody/Animator driving it).
        public static void MarkStaticRecursive(Transform t)
        {
            t.gameObject.isStatic = true;
            foreach (Transform child in t) MarkStaticRecursive(child);
        }

        /// Bakes a list of (mesh, world transform) instances into ONE static mesh -
        /// used to replace "hundreds/thousands of individual primitive GameObjects"
        /// patterns (guardrail posts/beams, ground clutter) with a couple of combined,
        /// statically-batched draw calls instead. Returns null if there's nothing to
        /// combine (e.g. an empty stretch of road).
        public static GameObject BuildCombinedStatic(Transform parent, string name,
            List<CombineInstance> combines, Material mat, bool addCollider = false)
        {
            if (combines == null || combines.Count == 0) return null;

            var mesh = new Mesh { name = name };
            mesh.indexFormat = IndexFormat.UInt32; // thousands of combined verts can exceed the 16-bit limit
            mesh.CombineMeshes(combines.ToArray(), true, true);
            mesh.RecalculateBounds();

            string meshPath = MapLayout.GeneratedFolder + "/mesh_" + name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = Vector3.zero;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (addCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// Store-bought textures ship with Read/Write disabled (smaller memory
        /// footprint at runtime); we need CPU pixel access to rotate them, so force
        /// it on. Permanently flips the source asset's import setting (one-time).
        static Texture2D ForceReadable(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// Some road/path textures are authored assuming the road runs along the
        /// texture's V axis (e.g. Kajaman's Roads: lane markings run top-to-bottom
        /// in the source image); Unity terrain layers map U->world X, V->world Z,
        /// so a road running mostly along X (like ours) shows those markings
        /// rotated 90 deg - lines running ACROSS the road instead of along it.
        /// Bakes a 90-degree-rotated copy (cached as a generated asset, like the
        /// other procedural textures) so tileSize.x lines up with "along the road."
        /// For normal maps, also rotates the encoded tangent-space vector (R/G),
        /// not just the pixel grid, so lighting on the bump detail stays correct.
        public static Texture2D Rotate90(string assetPath, bool isNormalMap, string cacheName)
        {
            string outPath = MapLayout.GeneratedFolder + "/tex_" + cacheName + ".asset";
            var cached = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (cached != null) return cached;

            var src = ForceReadable(assetPath);
            if (src == null) return null;

            int w = src.width, h = src.height;
            var srcPixels = src.GetPixels32();
            var dstPixels = new Color32[w * h];
            // 90-degree clockwise: dst(y, w-1-x) = src(x, y); dst size is (h, w)
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = srcPixels[y * w + x];
                    if (isNormalMap)
                    {
                        // rotate the encoded tangent-space normal's XY (R,G) to match
                        // the pixel rotation: (nx, ny) -> (ny, -nx), channels 0..255
                        // representing -1..1 (128 = zero).
                        byte r = c.r, g = c.g;
                        c = new Color32(g, (byte)(255 - r), c.b, c.a);
                    }
                    int nx = y, ny = w - 1 - x;
                    dstPixels[ny * h + nx] = c;
                }
            }
            var dst = new Texture2D(h, w, TextureFormat.RGBA32, true);
            dst.SetPixels32(dstPixels);
            dst.Apply(true);
            AssetDatabase.CreateAsset(dst, outPath);
            return dst;
        }

        /// Bakes a colour-multiplied copy of a texture (cached as a generated asset,
        /// like Rotate90). Used to force the Muddy ground layer to an unmistakable
        /// dirt-brown regardless of the pack texture's native (greenish) tint. `tint`
        /// multiplies each texel; alpha is preserved.
        public static Texture2D Tint(string assetPath, Color tint, string cacheName)
        {
            string outPath = MapLayout.GeneratedFolder + "/tex_" + cacheName + ".asset";
            var cached = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (cached != null) return cached;

            var src = ForceReadable(assetPath);
            if (src == null) return null;

            var px = src.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                px[i] = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
            }
            var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true);
            dst.SetPixels(px);
            dst.Apply(true);
            dst.wrapMode = src.wrapMode;
            AssetDatabase.CreateAsset(dst, outPath);
            return dst;
        }

        public static Vector3 Ground(Terrain t, float x, float z)
        {
            return new Vector3(x, t.SampleHeight(new Vector3(x, 0f, z)), z);
        }

        public static Vector3 Ground(Terrain t, Vector2 p) { return Ground(t, p.x, p.y); }

        public static Transform Group(Transform parent, string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent);
            g.transform.position = pos;
            return g.transform;
        }

        public static GameObject Empty(Transform parent, string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent);
            g.transform.position = pos;
            return g;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent,
            Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent);
            g.transform.position = pos;
            g.transform.localScale = scale;
            if (euler.HasValue) g.transform.eulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        public static void Label(Transform parent, string text, Vector3 pos)
        {
            var g = new GameObject("Label_" + text);
            g.transform.SetParent(parent);
            g.transform.position = pos;
            var tm = g.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 60;
            tm.characterSize = 0.30f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.9f, 0.6f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                g.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        /// Creates (or reuses) a solid-color URP material saved as an asset.
        public static Material Mat(string name, Color c, float emission = 0f)
        {
            string path = MapLayout.GeneratedFolder + "/mat_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = c;
            m.enableInstancing = true; // required for fast instanced terrain trees
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            return m;
        }

        /// Creates (or reuses) a textured, low-gloss URP material (bark, foliage...).
        /// Low smoothness avoids the "shiny plastic ball" look under moonlight.
        public static Material MatTextured(string name, Texture2D tex, Color tint, float smoothness = 0.1f, Texture2D normalMap = null)
        {
            string path = MapLayout.GeneratedFolder + "/mat_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = tint;
            m.mainTexture = tex;
            m.mainTextureScale = Vector2.one;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (normalMap != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normalMap);
                m.EnableKeyword("_NORMALMAP");
            }
            m.enableInstancing = true;
            return m;
        }

        public static float DistToPolyline(Vector2 p, Vector2[] pts)
        {
            float min = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = Vector2.Distance(p, a + ab * t);
                if (d < min) min = d;
            }
            return min;
        }

        public static float DistToScaryPaths(Vector2 p)
        {
            float min = float.MaxValue;
            foreach (var path in MapLayout.ScaryPaths)
                min = Mathf.Min(min, DistToPolyline(p, path));
            return min;
        }

        // Distancia al camino nuevo más cercano (senderos del editor de plano del owner).
        public static float DistToExtraTrails(Vector2 p)
        {
            float min = float.MaxValue;
            foreach (var path in MapLayout.ExtraTrails)
                min = Mathf.Min(min, DistToPolyline(p, path));
            return min;
        }

        /// Strip width helper: 1 inside fullWidth, fades to 0 at edgeWidth.
        public static float Strip(float d, float fullWidth, float edgeWidth)
        {
            if (d <= fullWidth) return 1f;
            if (d >= edgeWidth) return 0f;
            return 1f - (d - fullWidth) / (edgeWidth - fullWidth);
        }
    }
}
