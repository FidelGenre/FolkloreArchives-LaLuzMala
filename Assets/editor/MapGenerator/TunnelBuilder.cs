// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TunnelBuilder.cs — west-end tunnel (game start), built from the
//  CGTrader "Road Tunnel" FBX (Assets/ExternalAssets/TunnelAsset/Tunnel.fbx).
//
//  Measured FBX layout (from the OBJ export of the same model):
//    • Tunnel_walls  : arch tube, ±5.46 m wide, 6.93 m tall, runs along
//                      local +Z from z≈6.4 to z≈199.4  (≈193 m long)
//    • Road_Plane    : interior roadway at local y = 0 (the floor)
//    • Sidewalk / curbs / lights / pipe / traffic lights: interior detail
//    • Cube.002      : a giant 29×29×255 m enclosing box — REMOVED here
//                      (it is what rendered as the big white slab before)
//
//  Placement: rotated -90° on Y so local +Z → world -X (west).  The tube
//  opening (east face) lands at TunnelEntranceX and the tunnel runs west
//  past the map edge.  Floor local y=0 → world y = RoadSurfaceHeight, so
//  the interior roadway lines up with the terrain flatten + road mesh.
//
//  The road mesh has NO collider (driving uses the terrain collider, and
//  terrain ends at x=0) so every kept FBX part gets a MeshCollider — that
//  is what makes the tunnel actually drivable (spawn is at x=0, inside).
//
//  Around the FBX: procedural stone portal frame + cliff boxes + far-end
//  cap so the mountain reads solid and the car can't fall out the back.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FolkloreArchives.MapGen
{
    public static class TunnelBuilder
    {
        const string TunnelFbxPath = "Assets/ExternalAssets/TunnelAsset/Tunnel.fbx";

        // Exact set of Tunnel.fbx sub-parts the owner kept after deleting some by
        // hand in the scene (read from the saved SampleScene.unity). On regenerate
        // any FBX child NOT in this set is destroyed, so the hand-trimmed tunnel is
        // reproduced. If you delete/restore more parts in-editor, re-read the
        // TunnelMesh children and update this list.
        static readonly System.Collections.Generic.HashSet<string> KeepParts =
            new System.Collections.Generic.HashSet<string>
        {
            "Above utility pipe",
            "Area", "Area.001", "Area.002", "Area.003", "Area.004", "Area.005",
            "Area.006", "Area.007", "Area.008", "Area.009", "Area.010", "Area.011",
            "Area.012", "Area.013", "Area.014", "Area.015", "Area.016", "Area.017",
            "Area.018", "Area.019", "Area.020", "Area.021", "Area.022", "Area.023",
            "Area.024", "Area.025", "Area.026", "Area.027", "Area.028", "Area.029",
            "Area.030", "Area.031", "Area.032", "Area.033", "Area.034", "Area.035",
            "Area.036", "Area.037", "Area.038", "Area.039", "Area.040", "Area.041",
            "Camera.001",
            "Cube", "Cube.001",
            "Light.001", "Light.002",
            "Sidewalk", "Sidewalk.001",
            "Traffic lights", "Traffic lights.001", "Traffic lights.002",
            "Tunnel walls",
        };

        // Expected X-extent (m) of the model at scale 1 (walls+road ≈ 204 m).
        // If the imported size differs (unit mismatch), we auto-rescale.
        const float ExpectedLength = 204f;

        // Tube height measured from the FBX (top ≈ 6.93 m); used only to place the
        // interior lights near the ceiling.
        const float TubeHeight = 7.0f;

        public static void Build(Transform parent, Terrain terrain)
        {
            var group = BuilderUtils.Group(parent, "Tunnel", Vector3.zero);

            float ex    = MapLayout.TunnelEntranceX;                 // 30
            float ez    = MapLayout.PavedRouteZAt(ex);
            float roadY = MapLayout.RoadSurfaceHeight;               // 17

            GameObject fbx = PlaceFbxTunnel(group, ex, ez, roadY);
            if (fbx == null)
            {
                Debug.LogError("[TunnelBuilder] " + TunnelFbxPath +
                               " missing — tunnel NOT built. Re-import the asset.");
                return;
            }

            // Owner nudged the tube forward — override PlaceFbxTunnel's computed
            // transform with the hand-tuned one (read from the saved scene).
            fbx.transform.localPosition = TunnelMeshPos;
            fbx.transform.localRotation = Quaternion.Euler(0f, TunnelMeshYaw, 0f);
            fbx.transform.localScale    = TunnelMeshScale;

            AddInteriorLights(group, ex, ez, roadY);

            // Stone portal facade (arched opening, cornice, merlons) + a soil/rock
            // mound over the top, so the entrance reads as a tunnel bored into a
            // hillside (ref photo: brick railway portal in a scrub hill). Built in
            // the same pre-transform frame as the FBX tube so the group transform
            // below keeps them glued to the tube mouth.
            BuildStonePortal (group, ex, ez, roadY);
            BuildMountainMound(group, ex, ez, roadY);

            // Apply the owner's hand-tuned placement to the whole group. Everything
            // above is built in world coords under a group at the origin, so moving,
            // rotating + scaling the group now transforms it all together — identical
            // to nudging the "Tunnel" object in the Inspector (where these came from).
            group.localPosition = MapLayout.TunnelGroupOffset;
            group.localRotation = Quaternion.Euler(0f, MapLayout.TunnelGroupYaw, 0f);
            group.localScale    = MapLayout.TunnelGroupScale;

            BuilderUtils.MarkStaticRecursive(group);

            // Grass + small trees + bushes over the mound. Built as a SIBLING group
            // in world space (not under the Tunnel group) so the trees aren't skewed
            // by the group's non-uniform scale. Positions are computed from the same
            // baked transforms, so they land on the mound surface.
            BuildMoundVegetation(parent, ex, ez, roadY);

            // Grass on the ACTUAL Unity terrain the owner sculpted around the tunnel
            // entrance (samples real terrain height; skips road/water/steep).
            BuildTerrainGrassNearTunnel(parent, terrain, ex, ez, roadY);
        }

        // ── FBX placement ─────────────────────────────────────────────────────
        static GameObject PlaceFbxTunnel(Transform parent, float ex, float ez, float roadY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TunnelFbxPath);
            if (prefab == null) return null;

            // Plain clone (not PrefabUtility) so we are allowed to delete children.
            var go = Object.Instantiate(prefab);
            go.name = "TunnelMesh";
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.rotation   = Quaternion.Euler(0f, -90f, 0f); // local +Z → world -X (west)
            go.transform.localScale = Vector3.one;
            go.transform.position   = new Vector3(0f, roadY, ez);     // floor (local y=0) at road height

            // 1. Remove the giant enclosing box: the only part whose LOCAL mesh
            //    bounds exceed 15 m in both X and Y (box is 29×29×255; the tube
            //    is 10.9×7.5 and everything else is smaller).
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Vector3 s = mf.sharedMesh.bounds.size;
                if (s.x > 15f && s.y > 15f)
                    Object.DestroyImmediate(mf.gameObject);
            }

            // 2. Measure what's left and auto-fix scale if units are off.
            Bounds wb = WorldBounds(go);
            float lenX = wb.size.x;                       // road axis after rotation
            if (lenX < ExpectedLength * 0.9f || lenX > ExpectedLength * 1.1f)
            {
                float k = ExpectedLength / Mathf.Max(lenX, 0.001f);
                go.transform.localScale = Vector3.one * k;
                Debug.LogWarning($"[TunnelBuilder] FBX length was {lenX:F1} m — auto-rescaled ×{k:F3}.");
                wb = WorldBounds(go);
            }

            // 3. Snap: east face of the tube → portal (+0.3 overlap hides the rim
            //    behind the stone frame), tube centre → road centre line.
            var pos = go.transform.position;
            pos.x += (ex + 0.3f) - wb.max.x;
            pos.z += ez - wb.center.z;
            go.transform.position = pos;

            // 3b. Remove the FBX sub-parts the owner deleted by hand in the scene.
            //     KeepParts is the exact set of TunnelMesh children present after
            //     that edit (read from the saved scene). Done AFTER the snap above
            //     so measurement/centering is unchanged — deleting parts here does
            //     not move anything that remains.
            foreach (Transform child in System.Linq.Enumerable.ToArray(
                         System.Linq.Enumerable.Cast<Transform>(go.transform)))
            {
                if (!KeepParts.Contains(child.name))
                    Object.DestroyImmediate(child.gameObject);
            }

            // 4. Materials (FBX ships with none → renders white) + colliders so
            //    the car can drive on the interior roadway and not clip the walls.
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[mr.sharedMaterials.Length];
                var m    = PartMat(mr.gameObject.name);
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                mr.sharedMaterials = mats;

                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mr.GetComponent<MeshCollider>() == null)
                    mr.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            }

            Debug.Log($"[TunnelBuilder] FBX tunnel placed: east face x={WorldBounds(go).max.x:F1}, " +
                      $"west end x={WorldBounds(go).min.x:F1}, floor y={roadY:F1}, centre z={ez:F1}");
            return go;
        }

        // Material per FBX part, chosen by node name (fallback = concrete).
        static Material PartMat(string nodeName)
        {
            string n = nodeName.ToLowerInvariant();
            if (n.Contains("traffic"))  return BuilderUtils.Mat("tunnel_trafficlight", new Color(0.10f, 0.10f, 0.11f), 0f);
            if (n.Contains("light"))    return BuilderUtils.Mat("tunnel_lightstrip",  new Color(1.00f, 0.93f, 0.78f), 1.6f);
            if (n.Contains("road"))     return BuilderUtils.Mat("tunnel_asphalt",     new Color(0.13f, 0.13f, 0.14f), 0f);
            if (n.Contains("sidewalk")) return BuilderUtils.Mat("tunnel_sidewalk",    new Color(0.38f, 0.37f, 0.35f), 0f);
            if (n.Contains("pipe"))     return BuilderUtils.Mat("tunnel_pipe",        new Color(0.22f, 0.23f, 0.25f), 0f);
            var c = BuilderUtils.Mat("tunnel_concrete", new Color(0.33f, 0.32f, 0.30f), 0f);
            if (c.HasProperty("_Cull")) c.SetFloat("_Cull", 0f);   // tube visible from inside
            c.doubleSidedGI = true;
            return c;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<MeshRenderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // ── Interior lights ───────────────────────────────────────────────────
        // Spawn is at x=0 (30 m inside). Three warm points cover the drivable
        // stretch so the FBX light-strip meshes read as actually lit.
        static void AddInteriorLights(Transform parent, float ex, float ez, float roadY)
        {
            float[] xs = { ex - 12f, ex - 42f, ex - 72f };
            foreach (float lx in xs)
            {
                var li = new GameObject("TunnelLight").AddComponent<Light>();
                li.transform.SetParent(parent);
                li.transform.position = new Vector3(lx, roadY + TubeHeight - 0.8f, ez);
                li.type      = LightType.Point;
                li.color     = new Color(1.00f, 0.87f, 0.62f);
                li.intensity = 2.4f;
                li.range     = 30f;
                li.shadows   = LightShadows.None;
            }
        }

        // ══ Stone portal + mountain ══════════════════════════════════════════
        // Opening dimensions (pre-group-transform), sized to the FBX tube mouth.
        const float OpenHalfW = 5.7f;    // ±z of the arch opening (tube is ±5.46)
        const float OpenRectH = 1.5f;    // straight side height before the arch springs
        const float OpenArchR = 5.7f;    // arch radius (= half width → semicircle)
        // Facade (the flat stone wall around the opening).
        const float FacWingW  = 4.5f;    // stone width to each side of the opening
        const float FacParapet = 3.0f;   // stone height above the arch top
        const float FacDepth  = 2.5f;    // wall thickness (extends west into the hill)

        // ── Stone portal facade ──────────────────────────────────────────────
        static void BuildStonePortal(Transform parent, float ex, float ez, float roadY)
        {
            float floor    = roadY;
            float springY  = floor + OpenRectH;              // where the arch curve starts
            float openTop  = springY + OpenArchR;            // top of the opening
            float facTop   = openTop + FacParapet;           // top of the stone wall
            float facHalfW = OpenHalfW + FacWingW;           // half width of the wall
            float frontX   = ex;                             // facade front (faces east)
            float backX    = ex - FacDepth;

            var verts = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();

            // Sample columns across the facade width; each column has a bottom edge
            // (the arch curve inside the opening, or the floor outside it) and a top
            // edge at facTop. Front + back faces + arch soffit + top + side caps.
            const int cols = 72;
            var botY = new float[cols + 1];
            var zAt  = new float[cols + 1];
            for (int i = 0; i <= cols; i++)
            {
                float z  = ez - facHalfW + (2f * facHalfW) * i / cols;
                float dz = z - ez;
                float bottom;
                if (Mathf.Abs(dz) < OpenHalfW)
                    bottom = springY + Mathf.Sqrt(Mathf.Max(0f, OpenArchR * OpenArchR - dz * dz));
                else
                    bottom = floor;
                zAt[i]  = z;
                botY[i] = bottom;
            }

            // Front face (x = frontX): 2 verts per column (bottom, top)
            int frontBase = verts.Count;
            for (int i = 0; i <= cols; i++)
            {
                verts.Add(new Vector3(frontX, botY[i], zAt[i])); uvs.Add(new Vector2(zAt[i] * 0.5f, botY[i] * 0.5f));
                verts.Add(new Vector3(frontX, facTop,  zAt[i])); uvs.Add(new Vector2(zAt[i] * 0.5f, facTop  * 0.5f));
            }
            for (int i = 0; i < cols; i++)
            {
                int a = frontBase + i * 2;
                tris.Add(a); tris.Add(a + 1); tris.Add(a + 2);
                tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 3);
            }

            // Back face (x = backX): reversed winding
            int backBase = verts.Count;
            for (int i = 0; i <= cols; i++)
            {
                verts.Add(new Vector3(backX, botY[i], zAt[i])); uvs.Add(new Vector2(zAt[i] * 0.5f, botY[i] * 0.5f));
                verts.Add(new Vector3(backX, facTop,  zAt[i])); uvs.Add(new Vector2(zAt[i] * 0.5f, facTop  * 0.5f));
            }
            for (int i = 0; i < cols; i++)
            {
                int a = backBase + i * 2;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }

            // Arch soffit + floor underside (connect front-bottom to back-bottom):
            // this is the visible depth of the stone around the mouth.
            for (int i = 0; i < cols; i++)
            {
                int f0 = frontBase + i * 2, f1 = frontBase + (i + 1) * 2;
                int b0 = backBase  + i * 2, b1 = backBase  + (i + 1) * 2;
                tris.Add(f0); tris.Add(b0); tris.Add(f1);
                tris.Add(f1); tris.Add(b0); tris.Add(b1);
            }
            // Top edge (front-top to back-top)
            for (int i = 0; i < cols; i++)
            {
                int f0 = frontBase + i * 2 + 1, f1 = frontBase + (i + 1) * 2 + 1;
                int b0 = backBase  + i * 2 + 1, b1 = backBase  + (i + 1) * 2 + 1;
                tris.Add(f0); tris.Add(f1); tris.Add(b0);
                tris.Add(b0); tris.Add(f1); tris.Add(b1);
            }
            // Side caps (leftmost + rightmost columns)
            AddQuad(verts, uvs, tris,
                new Vector3(frontX, botY[0], zAt[0]), new Vector3(frontX, facTop, zAt[0]),
                new Vector3(backX,  botY[0], zAt[0]), new Vector3(backX,  facTop, zAt[0]));
            AddQuad(verts, uvs, tris,
                new Vector3(backX,  botY[cols], zAt[cols]), new Vector3(backX,  facTop, zAt[cols]),
                new Vector3(frontX, botY[cols], zAt[cols]), new Vector3(frontX, facTop, zAt[cols]));

            var mesh = new Mesh { name = "TunnelPortalFacade", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            var stone = StonePortalMat();
            var facadeGo = MakeMeshObject(parent, "PortalFacade", mesh, stone);
            ApplyLocal(facadeGo, PortalFacadePos, PortalFacadeScale);

            // Cornice: a slab proud of the facade across the top.
            var cube = BuilderUtils.PrimitiveMesh(PrimitiveType.Cube);
            var comb = new List<CombineInstance>
            {
                CI(cube, new Vector3(ex + 0.4f, openTop + FacParapet * 0.5f, ez),
                   Quaternion.identity, new Vector3(FacDepth + 1.4f, 1.1f, (facHalfW + 0.6f) * 2f)),
            };
            // Merlons (small blocks) along the top of the cornice.
            int merlonN = 7;
            for (int i = 0; i < merlonN; i++)
            {
                float t = merlonN == 1 ? 0.5f : i / (float)(merlonN - 1);
                float z = ez - facHalfW + t * 2f * facHalfW;
                comb.Add(CI(cube, new Vector3(ex + 0.4f, facTop + 0.6f, z),
                    Quaternion.identity, new Vector3(FacDepth + 1.2f, 1.2f, 1.6f)));
            }
            var corniceGo = BuilderUtils.BuildCombinedStatic(parent, "PortalCornice", comb, stone);
            ApplyLocal(corniceGo, PortalCornicePos, PortalCorniceScale);
        }

        // ── Mountain mound over the tunnel ───────────────────────────────────
        // Noisy dome sitting above/behind the facade, sloping to the ground on the
        // sides, so the tube reads as bored into a hill. Height field: high over the
        // centre (front rim ≈ facade top), rising behind, falling to floor at edges.
        const float MoundDepth    = 60f;                          // how far back the hill extends (west)
        const float MoundHalfW    = OpenHalfW + FacWingW + 20f;    // hill half-width
        const float MoundExtraPk  = 16f;                          // extra height of the hilltop behind

        // One point on the mound surface, in the mound's local build frame.
        // back ∈ [0,1] (0 = front rim, 1 = far back); zi01 ∈ [0,1] (0..1 across width).
        static Vector3 MoundLocalPoint(float ex, float ez, float floor, float back, float zi01)
        {
            float facTop = floor + OpenRectH + OpenArchR + FacParapet;
            float x   = ex - back * MoundDepth;
            float z   = ez - MoundHalfW + (2f * MoundHalfW) * zi01;
            float dzN = Mathf.Clamp01(Mathf.Abs(z - ez) / MoundHalfW);
            float side = Mathf.Cos(dzN * Mathf.PI * 0.5f);              // 1 centre → 0 edge
            float bulge = Mathf.Sin(Mathf.Clamp01(back) * Mathf.PI);   // 0 front/back → 1 mid
            float baseH = (facTop - floor) + MoundExtraPk * bulge;
            float noise = (Mathf.PerlinNoise(x * 0.03f + 11.3f, z * 0.03f + 4.1f) - 0.5f) * 6f
                        + (Mathf.PerlinNoise(x * 0.09f + 2.7f,  z * 0.09f + 8.9f) - 0.5f) * 2f;
            float y = floor + Mathf.Max(0f, baseH * side + noise * side);
            return new Vector3(x, y, z);
        }

        static void BuildMountainMound(Transform parent, float ex, float ez, float roadY)
        {
            float floor = roadY;
            const int NX = 34, NZ = 44;

            var verts = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();

            for (int xi = 0; xi <= NX; xi++)
            {
                float back = xi / (float)NX;
                for (int zi = 0; zi <= NZ; zi++)
                {
                    Vector3 p = MoundLocalPoint(ex, ez, floor, back, zi / (float)NZ);
                    verts.Add(p);
                    // 0.45 → ~4 m tiles after the group scale, so the soil detail shows.
                    uvs.Add(new Vector2(p.z * 0.45f, p.x * 0.45f));
                }
            }
            int rowLen = NZ + 1;
            for (int xi = 0; xi < NX; xi++)
                for (int zi = 0; zi < NZ; zi++)
                {
                    int a = xi * rowLen + zi, b = a + 1, c = a + rowLen, d = c + 1;
                    // Winding gives UP-facing normals (was reversed → faced down, so
                    // the texture/lighting showed on the underside only).
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(c); tris.Add(d); tris.Add(b);
                }

            var mesh = new Mesh { name = "TunnelMound", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            var moundGo = MakeMeshObject(parent, "TunnelMound", mesh, MoundMat());
            ApplyLocal(moundGo, TunnelMoundPos, TunnelMoundScale);
        }

        // ── Vegetation over the mound (grass + small trees + bushes) ─────────
        // Placed in world space under a sibling "TunnelVegetation" group so the
        // Tunnel group's non-uniform scale doesn't skew the trees. Surface points
        // come from MoundLocalPoint transformed by the same baked group/mound TRS.
        static void BuildMoundVegetation(Transform mapRoot, float ex, float ez, float roadY)
        {
            Matrix4x4 M =
                Matrix4x4.TRS(MapLayout.TunnelGroupOffset,
                              Quaternion.Euler(0f, MapLayout.TunnelGroupYaw, 0f),
                              MapLayout.TunnelGroupScale)
              * Matrix4x4.TRS(TunnelMoundPos, Quaternion.identity, TunnelMoundScale);

            var veg = BuilderUtils.Group(mapRoot, "TunnelVegetation", Vector3.zero);

            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(
                           MapLayout.GeneratedFolder + "/ALanTree.prefab");
            var bushes = new List<GameObject>();
            for (int i = 1; i <= 5; i++)
            {
                var b = AssetDatabase.LoadAssetAtPath<GameObject>(
                            MapLayout.GeneratedFolder + $"/YughuesBush_P_Bush0{i}.prefab");
                if (b != null) bushes.Add(b);
            }

            // Small trees (avoid the front rim over the opening + steep edges).
            if (tree != null)
            {
                int placed = 0, tries = 0;
                while (placed < 12 && tries < 250)
                {
                    tries++;
                    if (!SurfacePoint(M, ex, ez, roadY, Random.Range(0.28f, 0.9f),
                                      Random.Range(0.22f, 0.78f), 33f, out Vector3 wp)) continue;
                    float h = Random.Range(2.4f, 3.9f);
                    PlaceVeg(veg, tree, wp, h / MapLayout.RealTreeTargetHeight);
                    placed++;
                }
            }

            // Bushes / dry scrub (like the reference-photo hillside).
            if (bushes.Count > 0)
            {
                int placed = 0, tries = 0;
                while (placed < 24 && tries < 500)
                {
                    tries++;
                    if (!SurfacePoint(M, ex, ez, roadY, Random.Range(0.2f, 0.95f),
                                      Random.Range(0.12f, 0.88f), 42f, out Vector3 wp)) continue;
                    var bp = bushes[Random.Range(0, bushes.Count)];
                    float h = Random.Range(0.9f, 1.7f);
                    PlaceVeg(veg, bp, wp, h / MapLayout.BushTargetHeight);
                    placed++;
                }
            }

            BuildMoundGrass(veg, M, ex, ez, roadY);
        }

        // Grass (+ a few bushes) scattered on the REAL Unity terrain around the
        // tunnel entrance, so the ground the owner sculpted to the west reads as
        // vegetated. Samples terrain height; skips the road, water and steep slopes.
        static void BuildTerrainGrassNearTunnel(Transform mapRoot, Terrain terrain,
                                                float ex, float ez, float roadY)
        {
            if (terrain == null) return;

            // World XZ of the tunnel entrance, via the same baked group + facade TRS.
            Matrix4x4 groupM = Matrix4x4.TRS(MapLayout.TunnelGroupOffset,
                                   Quaternion.Euler(0f, MapLayout.TunnelGroupYaw, 0f),
                                   MapLayout.TunnelGroupScale);
            Matrix4x4 facM = Matrix4x4.TRS(PortalFacadePos, Quaternion.identity, PortalFacadeScale);
            Vector3 entrance = groupM.MultiplyPoint3x4(facM.MultiplyPoint3x4(new Vector3(ex, roadY + 3f, ez)));

            var group = BuilderUtils.Group(mapRoot, "TunnelTerrainGrass", Vector3.zero);
            float tY = terrain.GetPosition().y;

            var bushes = new List<GameObject>();
            for (int i = 1; i <= 5; i++)
            {
                var b = AssetDatabase.LoadAssetAtPath<GameObject>(
                            MapLayout.GeneratedFolder + $"/YughuesBush_P_Bush0{i}.prefab");
                if (b != null) bushes.Add(b);
            }

            var verts = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();

            const float R = 40f;          // radius around the entrance to vegetate
            int grass = 0, bush = 0, tries = 0;
            while (grass < 340 && tries < 2000)
            {
                tries++;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float rad = Mathf.Sqrt(Random.value) * R;
                float wx  = entrance.x + Mathf.Cos(ang) * rad;
                float wz  = entrance.z + Mathf.Sin(ang) * rad;

                if (BuilderUtils.DistToPolyline(new Vector2(wx, wz), MapLayout.PavedRoute) < 6.5f) continue;
                float h = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + tY;
                if (h < MapLayout.LakeLevel + 0.6f) continue;   // skip the lake side
                Vector3 nrm = terrain.terrainData.GetInterpolatedNormal(wx / MapLayout.MapSizeX, wz / MapLayout.MapSize);
                if (Vector3.Angle(nrm, Vector3.up) > 40f) continue;

                Vector3 wp = new Vector3(wx, h, wz);
                float a = Random.Range(0f, Mathf.PI);
                float w = Random.Range(0.5f, 0.9f), hh = Random.Range(0.4f, 0.8f);
                Vector3 d1 = new Vector3(Mathf.Cos(a),           0f, Mathf.Sin(a))           * w * 0.5f;
                Vector3 d2 = new Vector3(Mathf.Cos(a + 1.5708f), 0f, Mathf.Sin(a + 1.5708f)) * w * 0.5f;
                AddGrassQuad(verts, uvs, tris, wp, d1, hh);
                AddGrassQuad(verts, uvs, tris, wp, d2, hh);
                grass++;

                if (bushes.Count > 0 && bush < 14 && Random.value < 0.06f)
                {
                    PlaceVeg(group, bushes[Random.Range(0, bushes.Count)], wp,
                             Random.Range(0.9f, 1.6f) / MapLayout.BushTargetHeight);
                    bush++;
                }
            }

            if (verts.Count > 0)
            {
                var mesh = new Mesh { name = "TunnelTerrainGrass", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path = MapLayout.GeneratedFolder + "/mesh_TunnelTerrainGrass.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject("TerrainGrassMesh");
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = GrassMat();
                go.isStatic = true;
            }
            Debug.Log($"[TunnelBuilder] Terrain grass near entrance " +
                      $"({entrance.x:F1},{entrance.z:F1}): {grass} tufts, {bush} bushes.");
        }

        // True + world position if the mound surface at (back,zi01) is flatter than maxSlope°.
        static bool SurfacePoint(Matrix4x4 M, float ex, float ez, float floor,
                                 float back, float zi01, float maxSlope, out Vector3 world)
        {
            Vector3 l0 = MoundLocalPoint(ex, ez, floor, back, zi01);
            Vector3 l1 = MoundLocalPoint(ex, ez, floor, Mathf.Min(1f, back + 0.01f), zi01);
            Vector3 l2 = MoundLocalPoint(ex, ez, floor, back, Mathf.Min(1f, zi01 + 0.01f));
            world = M.MultiplyPoint3x4(l0);
            Vector3 w1 = M.MultiplyPoint3x4(l1), w2 = M.MultiplyPoint3x4(l2);
            Vector3 nrm = Vector3.Cross(w2 - world, w1 - world).normalized;
            if (nrm.y < 0f) nrm = -nrm;
            return Vector3.Angle(nrm, Vector3.up) <= maxSlope;
        }

        static void PlaceVeg(Transform parent, GameObject prefab, Vector3 worldPos, float scaleMul)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(parent, false);
            inst.transform.position   = worldPos;
            inst.transform.rotation   = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            inst.transform.localScale *= scaleMul;   // prefab is already height-normalised
        }

        static void BuildMoundGrass(Transform veg, Matrix4x4 M, float ex, float ez, float floor)
        {
            var verts = new List<Vector3>();
            var uvs   = new List<Vector2>();
            var tris  = new List<int>();
            int placed = 0, tries = 0;
            while (placed < 140 && tries < 700)
            {
                tries++;
                if (!SurfacePoint(M, ex, ez, floor, Random.Range(0.15f, 0.97f),
                                  Random.Range(0.08f, 0.92f), 40f, out Vector3 wp)) continue;
                float a = Random.Range(0f, Mathf.PI);
                float w = Random.Range(0.5f, 0.9f), h = Random.Range(0.4f, 0.8f);
                Vector3 d1 = new Vector3(Mathf.Cos(a),           0f, Mathf.Sin(a))           * w * 0.5f;
                Vector3 d2 = new Vector3(Mathf.Cos(a + 1.5708f), 0f, Mathf.Sin(a + 1.5708f)) * w * 0.5f;
                AddGrassQuad(verts, uvs, tris, wp, d1, h);
                AddGrassQuad(verts, uvs, tris, wp, d2, h);
                placed++;
            }
            if (verts.Count == 0) return;

            var mesh = new Mesh { name = "TunnelGrass", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = MapLayout.GeneratedFolder + "/mesh_TunnelGrass.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("TunnelGrass");
            go.transform.SetParent(veg, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GrassMat();
            go.isStatic = true;
        }

        static void AddGrassQuad(List<Vector3> v, List<Vector2> uv, List<int> t,
                                 Vector3 c, Vector3 d, float h)
        {
            int i = v.Count;
            Vector3 up = Vector3.up * h;
            v.Add(c - d); v.Add(c + d); v.Add(c - d + up); v.Add(c + d + up);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0));
            uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1));
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i + 1); t.Add(i + 2); t.Add(i + 3);
        }

        static Material GrassMat()
        {
            var tex = GrassTuftTex();
            var m = BuilderUtils.MatTextured("tunnel_grass", tex, Color.white, 0.05f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.4f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // double-sided blades
            m.doubleSidedGI = true;
            return m;
        }

        // Small procedural grass-blade texture (transparent bg + a few green blades),
        // so we don't depend on an external cutout grass asset.
        static Texture2D GrassTuftTex()
        {
            string path = MapLayout.GeneratedFolder + "/tex_tunnelgrass.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int S = 64;
            var t  = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            var rnd = new System.Random(1234);
            for (int b = 0; b < 7; b++)
            {
                int bx = 5 + rnd.Next(S - 10);
                int bw = 2 + rnd.Next(2);
                int topH = (int)(S * (0.5f + (float)rnd.NextDouble() * 0.45f));
                var col = Color.Lerp(new Color(0.16f, 0.28f, 0.09f),
                                     new Color(0.42f, 0.56f, 0.20f), (float)rnd.NextDouble());
                for (int y = 0; y < topH; y++)
                {
                    float taper = 1f - y / (float)topH;
                    int half = Mathf.Max(1, (int)(bw * taper + 0.5f));
                    int sway = (int)(Mathf.Sin(y * 0.15f) * 2f);
                    for (int x = bx - half + sway; x <= bx + half + sway; x++)
                    {
                        if (x < 0 || x >= S) continue;
                        var c = col * (0.7f + 0.3f * (y / (float)topH));
                        c.a = 1f;
                        px[(S - 1 - y) * S + x] = c;
                    }
                }
            }
            t.SetPixels(px); t.Apply();
            AssetDatabase.CreateAsset(t, path);
            return t;
        }

        // ── Hand-tuned local transforms for the portal pieces ────────────────
        // Read from the saved scene after the owner nudged each piece by hand.
        // Applied (relative to the Tunnel group) right after each piece is built,
        // so a regenerate reproduces the manual placement. All rotations identity.
        // If you move these pieces again, re-read their Transforms and update here.
        // The FBX tube itself, nudged forward by the owner (overrides the transform
        // PlaceFbxTunnel computes). Scale is the auto-rescale value; yaw −90 as built.
        static readonly Vector3 TunnelMeshPos   = new Vector3(2.7094848f, 17f, 70.48999f);
        static readonly Vector3 TunnelMeshScale = new Vector3(0.80110174f, 0.80110174f, 0.80110174f);
        const float TunnelMeshYaw = -90f;

        static readonly Vector3 PortalFacadePos    = new Vector3(-31.13f, -0.39f, 10.56f);
        static readonly Vector3 PortalFacadeScale  = new Vector3(1f, 0.9461074f, 0.831884f);
        static readonly Vector3 PortalCornicePos   = new Vector3(-31.13f, -0.5f, 10.69f);
        static readonly Vector3 PortalCorniceScale = new Vector3(1f, 0.9461074f, 0.831884f);
        static readonly Vector3 TunnelMoundPos     = new Vector3(-32.4f, -1.38f, 13.34f);
        static readonly Vector3 TunnelMoundScale   = new Vector3(1f, 0.9461074f, 0.831884f);

        static void ApplyLocal(GameObject go, Vector3 pos, Vector3 scale)
        {
            if (go == null) return;
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
        }

        // ── Small helpers ────────────────────────────────────────────────────
        static GameObject MakeMeshObject(Transform parent, string name, Mesh mesh, Material mat)
        {
            string path = MapLayout.GeneratedFolder + "/mesh_" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void AddQuad(List<Vector3> v, List<Vector2> uv, List<int> t,
                            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1));
            uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i + 2); t.Add(i + 1); t.Add(i + 3);
        }

        static CombineInstance CI(Mesh m, Vector3 pos, Quaternion rot, Vector3 scale) =>
            new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, rot, scale) };

        static Texture2D LoadTex(params string[] paths)
        {
            foreach (var p in paths)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (t != null) return t;
            }
            return null;
        }

        static Material StonePortalMat()
        {
            var tex = LoadTex(
                "Assets/ExternalAssets/ForestPack/Texture/Rock Texture/Rock2/Rock2_2K_Color.png",
                "Assets/YughuesFreePavementsMaterials/Textures/T_YFPM_StonesRough_d.tga",
                "Assets/ExternalAssets/ForestPack/Texture/Rock Texture/Rock3/Rock3_2K_Color.png");
            Material m = tex != null
                ? BuilderUtils.MatTextured("portal_stone", tex, new Color(0.74f, 0.71f, 0.65f), 0.05f)
                : BuilderUtils.Mat("portal_stone", new Color(0.50f, 0.47f, 0.43f));
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
            return m;
        }

        static Material MoundMat()
        {
            var tex = LoadTex(
                "Assets/TerrainSampleAssets/Textures/Terrain/Soil_Rocks_BaseColor.tif",
                "Assets/ExternalAssets/ForestPack/Texture/Rock Texture/Rock3/Rock3_2K_Color.png",
                "Assets/ExternalAssets/ForestPack/Texture/Rock Texture/Rock2/Rock2_2K_Color.png");
            var m = tex != null
                ? BuilderUtils.MatTextured("portal_mound", tex, new Color(0.60f, 0.56f, 0.47f), 0.02f)
                : BuilderUtils.Mat("portal_mound", new Color(0.45f, 0.40f, 0.32f));
            // Double-sided so the hill is visible from above no matter the winding
            // (belt-and-suspenders with the flipped triangles). Also overrides the
            // manual "Render Face = Back" the owner had set on the .mat asset.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
            return m;
        }

    }
}
