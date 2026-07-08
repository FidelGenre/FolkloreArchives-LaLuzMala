// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  RoadsideBuilder.cs — the lakeside of the paved route (the side
//  away from the forest/park): a galvanised W-beam guardrail along
//  the road, and a lake that runs off past the map edge toward the
//  skybox mountain silhouettes. Reference: RN40, Neuquén.
//  Paste into:  Assets/Editor/MapGenerator/RoadsideBuilder.cs
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FolkloreArchives.MapGen
{
    public static class RoadsideBuilder
    {
        public static void Build(Transform parent, Terrain terrain)
        {
            BuildLake(parent, terrain);
            BuildGuardrail(parent, terrain);
            BuildPavedRoadMesh(parent, terrain);
        }

        // ---------------- Paved road surface mesh ----------------
        // TerrainBuilder.PavedRoadLayer paints the asphalt as a terrain LAYER, which
        // tiles in fixed world X/Z and can't rotate to track the curve - so its lane
        // lines drift slightly off-angle through bends (see DEV_LOG.md). This mesh
        // sits a hair above that terrain paint and is built the same way as the lake/
        // guardrail above: sampled along the real curved MapLayout.PavedRoute, with
        // per-vertex UVs driven by actual ARC LENGTH (not raw world position), so the
        // texture's dash/edge-line pattern follows the curve exactly no matter how it
        // bends. Uses the ORIGINAL (un-rotated) Kajaman texture directly - here WE
        // decide which texture axis is "along" vs "across" via the UVs we assign, so
        // no rotated copy is needed (unlike TerrainBuilder's terrain-layer approach).
        static void BuildPavedRoadMesh(Transform parent, Terrain terrain)
        {
            string diffusePath = "Assets/KajamansRoads/Textures/Road_2lane_dark02.png";
            string normalPath = "Assets/KajamansRoads/Textures/Road_2lane_dark02_n.png";
            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            if (diffuse == null) return; // TerrainBuilder's own fallback already covers this case

            var route = MapLayout.PavedRoute;
            const float northHalf = 12f; // matches TerrainBuilder's Strip "full" width (before its fade-to-shoulder)
            const float southHalf = 4.5f;
            const float tileLength = 9f; // along-road dash spacing, matches the pack's design
            const float lift = 0.05f;    // sits a hair above the terrain paint - avoids z-fighting

            var verts = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();

            // Road sits at a fixed height independent of the terrain below.
            // The terrain is already flattened to this level by TerrainBuilder (dPav < 13m),
            // so the mesh never floats and the terrain never pokes through the surface.
            float roadY = MapLayout.RoadSurfaceHeight + lift;
            // Skirts drop down this far on each edge to give the mesh real volume
            // and hide any remaining seam between road and terrain at the berm.
            const float skirtDepth = 2.5f;

            // Layout per cross-section (5 verts): skirted south edge, south edge top,
            // spline centre, north edge top, skirted north edge.
            // Indices: 0=south bottom, 1=south top, 2=centre, 3=north top, 4=north bottom
            float centerU = southHalf / (southHalf + northHalf); // ~0.273 — places Kajaman's centre dash at physical road centre

            float arcLen = 0f;
            for (int i = 0; i < route.Length; i++)
            {
                Vector2 tangent = i == 0 ? (route[1] - route[0]).normalized
                    : i == route.Length - 1 ? (route[i] - route[i - 1]).normalized
                    : (route[i + 1] - route[i - 1]).normalized;
                Vector2 side = new Vector2(-tangent.y, tangent.x); // rotate 90 → points north (+Z)

                Vector2 p      = route[i];
                Vector2 southP = p - side * southHalf;
                Vector2 northP = p + side * northHalf;

                // top surface (flat at road height)
                verts.Add(new Vector3(southP.x, roadY,             southP.y)); // 0 south top
                verts.Add(new Vector3(p.x,      roadY,             p.y));      // 1 centre
                verts.Add(new Vector3(northP.x, roadY,             northP.y)); // 2 north top
                // skirt bottoms (drop below road surface)
                verts.Add(new Vector3(southP.x, roadY - skirtDepth, southP.y)); // 3 south bottom
                verts.Add(new Vector3(northP.x, roadY - skirtDepth, northP.y)); // 4 north bottom

                float v = arcLen / tileLength;
                uvs.Add(new Vector2(0f,       v)); // south top
                uvs.Add(new Vector2(centerU,  v)); // centre
                uvs.Add(new Vector2(1f,       v)); // north top
                uvs.Add(new Vector2(0f,       v)); // south bottom (skirt - same U)
                uvs.Add(new Vector2(1f,       v)); // north bottom (skirt)

                if (i < route.Length - 1)
                {
                    arcLen += Vector2.Distance(route[i], route[i + 1]);
                    int b = i * 5, n = (i + 1) * 5;

                    // top surface (two quads: south half + north half)
                    tris.Add(b+0); tris.Add(b+1); tris.Add(n+0);
                    tris.Add(n+0); tris.Add(b+1); tris.Add(n+1);
                    tris.Add(b+1); tris.Add(b+2); tris.Add(n+1);
                    tris.Add(n+1); tris.Add(b+2); tris.Add(n+2);

                    // south skirt (vertical face facing south)
                    tris.Add(b+3); tris.Add(b+0); tris.Add(n+3);
                    tris.Add(n+3); tris.Add(b+0); tris.Add(n+0);

                    // north skirt (vertical face facing north)
                    tris.Add(b+2); tris.Add(b+4); tris.Add(n+2);
                    tris.Add(n+2); tris.Add(b+4); tris.Add(n+4);
                }
            }

            var mesh = new Mesh { name = "PavedRoadSurface" };
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            string meshPath = MapLayout.GeneratedFolder + "/mesh_PavedRoadSurface.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("PavedRoad_Surface");
            go.transform.SetParent(parent);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var normalMap = BuilderUtils.LoadAsNormalMap(normalPath);
            mr.sharedMaterial = BuilderUtils.MatTextured("pavedroad_mesh", diffuse, Color.white, 0.3f, normalMap);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            // Collider so the player can walk/drive on the road surface (the road mesh
            // sits on a fixed-height embankment above the terrain, so without this the
            // player would fall through to the terrain below).
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ---------------- Lake ----------------
        // A flat water surface whose NORTH edge follows the road curve (offset a few
        // metres south) and whose far edge runs well past the map edge. It sits at
        // LakeLevel; wherever the (uncarved) ground is higher than that, the terrain
        // simply hides the water - so the visible waterline is the carved shoreline,
        // and the mesh never pokes onto the road even though the road curves ~50m in z.
        static void BuildLake(Transform parent, Terrain terrain)
        {
            float y = MapLayout.LakeLevel;
            float xStart = -60f, xEnd = MapLayout.MapSizeX + 60f, step = 12f;
            float farZ = -380f;          // runs off past the south map edge
            float edgeInset = 6f;        // north edge = a few metres south of the road centre

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();

            int cols = 0;
            for (float x = xStart; x <= xEnd + 0.1f; x += step) cols++;

            int ci = 0;
            for (float x = xStart; x <= xEnd + 0.1f; x += step, ci++)
            {
                float northZ = MapLayout.PavedRouteZAt(x) - edgeInset;
                verts.Add(new Vector3(x, y, northZ)); // north (shore) vertex
                verts.Add(new Vector3(x, y, farZ));   // south (far) vertex
                uvs.Add(new Vector2(x * 0.05f, northZ * 0.05f));
                uvs.Add(new Vector2(x * 0.05f, farZ * 0.05f));

                if (ci < cols - 1)
                {
                    int n0 = ci * 2, s0 = ci * 2 + 1, n1 = (ci + 1) * 2, s1 = (ci + 1) * 2 + 1;
                    // wind so the surface faces up (+Y)
                    tris.Add(n0); tris.Add(s0); tris.Add(n1);
                    tris.Add(n1); tris.Add(s0); tris.Add(s1);
                }
            }

            var mesh = new Mesh { name = "LakeSurface" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string meshPath = MapLayout.GeneratedFolder + "/mesh_LakeSurface.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("Lake_Water");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(0f, -3.3f, 0f);  // Y hand-tuned by owner
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // same dark, faintly-emissive water as the river so they read as one body
            var water = BuilderUtils.Mat("lakewater", new Color(0.05f, 0.11f, 0.16f), 0.2f);
            // double-sided: our procedural strip mesh could wind either way, so don't
            // let a flipped normal make the whole lake invisible from above.
            if (water.HasProperty("_Cull")) water.SetFloat("_Cull", 0f);
            water.doubleSidedGI = true;
            mr.sharedMaterial = water;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------- Guardrail ----------------
        // ~232 posts + ~232 beams along the 1390m road used to each be their own
        // GameObject/primitive (~464 draw calls, no static batching) - the same
        // "thousands of unbatched primitives" pattern that caused the original
        // 134M-tri/42FPS incident (DEV_LOG.md), just in props instead of trees.
        // Now baked into 2 combined static meshes (posts, beams) via
        // BuilderUtils.BuildCombinedStatic - same visual result, 2 draw calls total.
        static void BuildGuardrail(Transform parent, Terrain terrain)
        {
            var group = BuilderUtils.Group(parent, "Guardrail", Vector3.zero);

            var beamTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/KajamansRoads/Textures/Guardrails01.png");
            Material beamMat = beamTex != null
                ? BuilderUtils.MatTextured("guardrail_beam", beamTex, Color.white, 0.35f)
                : BuilderUtils.Mat("guardrail_beam", new Color(0.62f, 0.64f, 0.66f));
            beamMat.mainTextureScale = new Vector2(2.5f, 1f); // repeat the W-beam along each ~6m span
            var postMat = BuilderUtils.Mat("guardrail_post", new Color(0.42f, 0.44f, 0.46f));
            var cubeMesh = BuilderUtils.PrimitiveMesh(PrimitiveType.Cube);

            float step = MapLayout.GuardrailPostStep;
            float x0 = 5f, x1 = MapLayout.MapSizeX - 5f;

            Vector3 Base(float x)
            {
                float gz = MapLayout.PavedRouteZAt(x) - MapLayout.GuardrailOffset;
                return BuilderUtils.Ground(terrain, x, gz);
            }

            var postCombines = new List<CombineInstance>();
            var beamCombines = new List<CombineInstance>();

            for (float x = x0; x < x1; x += step)
            {
                Vector3 a = Base(x);

                postCombines.Add(new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(a + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.12f, 0.95f, 0.12f))
                });

                // beam to the next post
                Vector3 b = Base(Mathf.Min(x + step, x1));
                Vector3 dir = b - a;
                if (dir.sqrMagnitude < 0.001f) continue;
                Vector3 mid = (a + b) * 0.5f + Vector3.up * 0.6f;
                Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                beamCombines.Add(new CombineInstance
                {
                    mesh = cubeMesh,
                    transform = Matrix4x4.TRS(mid, rot, new Vector3(0.05f, 0.34f, dir.magnitude))
                });
            }

            BuilderUtils.BuildCombinedStatic(group, "GuardrailPosts", postCombines, postMat);
            // keep a collider on the beams (combined MeshCollider) so the player can't
            // walk off the road into the lake
            BuilderUtils.BuildCombinedStatic(group, "GuardrailBeams", beamCombines, beamMat, addCollider: true);
        }
    }
}
