// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CorralBuilder.cs — owner: "hacer las rejas para el granero donde van a estar los
//  pollos... tomar la reja (sin los fierros de adelante) y rodearla con madera para
//  hacer el corral de las gallinas".
//
//  1) NettingMesh(): toma la malla del asset chain-link (PSX Modular Chain-Link Fence,
//     DanglingBat) y arma una versión SOLO-TEJIDO: copia únicamente el/los submesh(es)
//     que NO son de acero (el material "steel/galv" = postes/marco de adelante). Guarda
//     el mesh en Generated/.
//  2) Build(): arma un corral rectangular de madera (postes + travesaños arriba/abajo)
//     con el tejido entre postes, dejando una ENTRADA (gate) en el lado sur. Cerca del
//     gallinero. Grupo "CorralGallinas" bajo FOLKLORE_MAP, nombres únicos, creado ANTES
//     de ApplySavedLayout (se llama desde HouseBuilder) → guardable con Save Map Layout.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class CorralBuilder
    {
        const string FenceFbx = "Assets/ExternalAssets/ChainLinkFence/Models/chain_link_fence_01.fbx";

        // Geometría del corral (todo movible/regenerable; el owner ajusta con Save Map Layout).
        static readonly Vector2 Center = new Vector2(195f, 169f); // cerca del gallinero (CoopSpot 195,170)
        const float HalfX     = 4.5f;   // medio ancho X → 9 m
        const float HalfZ     = 4f;     // medio fondo Z → 8 m
        const float Seg       = 2f;     // largo de cada panel (ancho nativo de la reja)
        const float NetHeight = 1.3f;   // alto del tejido
        const float PostH     = 1.5f;   // alto de los postes de madera
        const float PostW     = 0.12f;  // grosor de los postes
        const float RailT     = 0.08f;  // grosor de los travesaños

        static int _id;

        public static void Build(Transform parent, Terrain terrain)
        {
            var netMesh = NettingMesh();
            if (netMesh == null)
            {
                Debug.LogWarning("[Corral] no pude armar la malla de tejido (¿falta " + FenceFbx +
                                 " o Unity no lo importó?). No armo el corral.");
                return;
            }
            var netMat  = NetMat();
            // Madera texturada: Wood_04 del pack de la granja = pino claro/dorado con veta (owner
            // pidió "más clarita, tipo pino"). Tileable, combina con el resto de la granja.
            var woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/AbandonedFarm/Textures/Wood_04.jpg");
            var woodMat = woodTex != null
                ? BuilderUtils.MatTextured("corral_wood", woodTex, Color.white, 0f)
                : BuilderUtils.Mat("corral_wood", new Color(0.34f, 0.23f, 0.13f), 0f); // fallback color plano

            _id = 0;
            var group = BuilderUtils.Group(parent, "CorralGallinas", Vector3.zero);

            float cx = Center.x, cz = Center.y;
            // esquinas
            Vector2 SW = new Vector2(cx - HalfX, cz - HalfZ);
            Vector2 SE = new Vector2(cx + HalfX, cz - HalfZ);
            Vector2 NE = new Vector2(cx + HalfX, cz + HalfZ);
            Vector2 NW = new Vector2(cx - HalfX, cz + HalfZ);

            // 4 lados. Sur con ENTRADA (se saltea el panel del medio). Postes: cada lado pone
            // el poste al INICIO de cada panel (no el del final) → la esquina la cubre el
            // primer poste del lado siguiente (sin postes dobles ni faltantes).
            BuildSide(group, terrain, netMesh, netMat, woodMat, SW, SE, 0f,  gate: true);   // sur (entrada)
            BuildSide(group, terrain, netMesh, netMat, woodMat, SE, NE, 90f, gate: false);  // este
            BuildSide(group, terrain, netMesh, netMat, woodMat, NE, NW, 0f,  gate: false);  // norte
            BuildSide(group, terrain, netMesh, netMat, woodMat, NW, SW, 90f, gate: false);  // oeste

            BuildRoof(group, terrain, netMesh, netMat, woodMat);                             // techo de reja

            Debug.Log("<color=lime>[Corral] corral de gallinas (madera + tejido, con techo de reja) armado cerca del gallinero. " +
                      "Movelo/ajustalo a mano y Tools ▸ Folklore Archives ▸ Save Map Layout.</color>");
        }

        static void BuildSide(Transform group, Terrain t, Mesh netMesh, Material netMat, Material woodMat,
                              Vector2 A, Vector2 B, float yaw, bool gate)
        {
            Vector2 d = B - A; float L = d.magnitude;
            if (L < 0.01f) return;
            d /= L;
            int n = Mathf.Max(1, Mathf.RoundToInt(L / Seg));
            float seg = L / n;
            int gateIdx = gate ? n / 2 : -1;

            for (int i = 0; i < n; i++)
            {
                // poste de madera al inicio de cada panel
                Vector2 pp = A + d * (i * seg);
                Post(group, woodMat, t, pp);

                if (i == gateIdx) continue; // hueco de la entrada (solo quedan los postes de los lados)

                Vector2 mid = A + d * ((i + 0.5f) * seg);
                float gy = BuilderUtils.Ground(t, mid.x, mid.y).y;

                // TEJIDO (solo malla, sin fierros) escalado a este panel
                var panel = new GameObject("Reja_" + (_id));
                panel.transform.SetParent(group);
                var mf = panel.AddComponent<MeshFilter>(); mf.sharedMesh = netMesh;
                panel.AddComponent<MeshRenderer>().sharedMaterial = netMat;
                Vector3 ns = netMesh.bounds.size;
                float sx = ns.x > 0.001f ? seg / ns.x : 1f;
                float sy = ns.y > 0.001f ? NetHeight / ns.y : 1f;
                panel.transform.localScale = new Vector3(sx, sy, 1f);
                panel.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                panel.transform.position = new Vector3(mid.x, gy, mid.y);
                var pc = panel.AddComponent<BoxCollider>();
                pc.center = new Vector3(0f, NetHeight * 0.5f, 0f);
                pc.size = new Vector3(seg, NetHeight, 0.06f);

                // TRAVESAÑOS de madera (arriba y abajo) a lo largo del panel
                Rail(group, woodMat, t, mid, yaw, seg, NetHeight - RailT * 0.5f); // superior
                Rail(group, woodMat, t, mid, yaw, seg, 0.12f);                     // inferior
                _id++;
            }
            // poste final del lado (cierra el último panel; la esquina la comparte con el lado siguiente
            // salvo el último lado, que cierra contra el primero → ese poste lo pone este)
            Post(group, woodMat, t, B);
        }

        // Techo de REJA: vigas de madera a lo largo de X (una por línea de postes en Z) sobre los
        // postes, y tejido HORIZONTAL tileado (paneles ~2 m acostados) cubriendo toda la planta.
        static void BuildRoof(Transform group, Terrain t, Mesh netMesh, Material netMat, Material woodMat)
        {
            float cx = Center.x, cz = Center.y;

            // vigas de soporte (a lo largo de X) en cada línea de postes en Z, a la altura de los postes
            int nz = Mathf.Max(1, Mathf.RoundToInt(HalfZ * 2f / Seg));
            for (int j = 0; j <= nz; j++)
            {
                float z = cz - HalfZ + j * (HalfZ * 2f / nz);
                float gy = BuilderUtils.Ground(t, cx, z).y + PostH;
                BuilderUtils.Prim(PrimitiveType.Cube, "TechoViga_" + (_id++), group,
                    new Vector3(cx, gy, z), new Vector3(HalfX * 2f, RailT, RailT), woodMat);
            }

            // tejido horizontal tileado (~2 m) cubriendo la planta, apenas por encima de las vigas
            int nxc = Mathf.Max(1, Mathf.RoundToInt(HalfX * 2f / Seg));
            int nzc = Mathf.Max(1, Mathf.RoundToInt(HalfZ * 2f / Seg));
            float cellX = HalfX * 2f / nxc, cellZ = HalfZ * 2f / nzc;
            Vector3 ns = netMesh.bounds.size;
            for (int ix = 0; ix < nxc; ix++)
                for (int iz = 0; iz < nzc; iz++)
                {
                    float px = cx - HalfX + (ix + 0.5f) * cellX;
                    float pz = cz - HalfZ + (iz + 0.5f) * cellZ;
                    float gy = BuilderUtils.Ground(t, px, pz).y + PostH + RailT;

                    var panel = new GameObject("TechoReja_" + (_id++));
                    panel.transform.SetParent(group);
                    panel.AddComponent<MeshFilter>().sharedMesh = netMesh;
                    panel.AddComponent<MeshRenderer>().sharedMaterial = netMat;
                    // acostar el tejido: al rotar -90° en X, el alto del mesh (Y) pasa a ser profundidad (Z)
                    float sx = ns.x > 0.001f ? cellX / ns.x : 1f;
                    float sy = ns.y > 0.001f ? cellZ / ns.y : 1f;
                    panel.transform.localScale = new Vector3(sx, sy, 1f);
                    panel.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                    panel.transform.position = new Vector3(px, gy, pz);
                    var b = PanelBounds(panel);                       // recentrar (el pivote del mesh es base-centro)
                    panel.transform.position += new Vector3(px, gy, pz) - b.center;
                }
        }

        static Bounds PanelBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void Post(Transform group, Material wood, Terrain t, Vector2 p)
        {
            float gy = BuilderUtils.Ground(t, p.x, p.y).y;
            BuilderUtils.Prim(PrimitiveType.Cube, "Poste_" + (_id++), group,
                new Vector3(p.x, gy + PostH * 0.5f, p.y), new Vector3(PostW, PostH, PostW), wood);
        }

        static void Rail(Transform group, Material wood, Terrain t, Vector2 mid, float yaw, float len, float h)
        {
            float gy = BuilderUtils.Ground(t, mid.x, mid.y).y;
            BuilderUtils.Prim(PrimitiveType.Cube, "Travesano_" + (_id++), group,
                new Vector3(mid.x, gy + h, mid.y), new Vector3(len, RailT, RailT), wood,
                new Vector3(0f, yaw, 0f));
        }

        // ── Malla SOLO-TEJIDO: copia del mesh del asset sin el/los submesh(es) de acero ──
        static Mesh _netting;
        static Mesh NettingMesh()
        {
            if (_netting != null) return _netting;
            const string outPath = MapLayout.GeneratedFolder + "/mesh_chainlink_netting.asset";

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FenceFbx);
            if (fbx == null) return null;
            var mf = fbx.GetComponentInChildren<MeshFilter>();
            var mr = fbx.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null || mr == null) return null;
            var src  = mf.sharedMesh;
            var mats = mr.sharedMaterials;

            // submesh de acero = el que tiene material "steel/galv" (los fierros del frente). Se descarta.
            var keep = new List<int>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                string on = (s < mats.Length && mats[s] != null) ? mats[s].name.ToLowerInvariant() : "";
                if (!(on.Contains("steel") || on.Contains("galv"))) keep.Add(s);
            }
            if (keep.Count == 0) return null;

            var m = new Mesh { name = "chainlink_netting" };
            m.indexFormat = src.indexFormat;
            m.vertices = src.vertices;
            if (src.normals != null && src.normals.Length == src.vertexCount) m.normals = src.normals;
            if (src.uv != null && src.uv.Length == src.vertexCount) m.uv = src.uv;
            if (src.tangents != null && src.tangents.Length == src.vertexCount) m.tangents = src.tangents;
            m.subMeshCount = keep.Count;
            for (int k = 0; k < keep.Count; k++) m.SetTriangles(src.GetTriangles(keep[k]), k);
            m.RecalculateBounds();

            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(m, outPath);
            _netting = m;
            return m;
        }

        static Material _netMat;
        static Material NetMat()
        {
            if (_netMat != null) return _netMat;
            // reusar el material del cerco YPF si ya existe (mismo tejido cutout doble cara)
            var saved = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/ChainLinkFence_Chain.mat");
            if (saved != null) { _netMat = saved; return saved; }
            const string TexDir = "Assets/ExternalAssets/ChainLinkFence/Textures/";
            var chainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "chainlink_diffuse_128x128_png_chainlink_alpha_128x128.png");
            var mm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (chainTex != null) mm.SetTexture("_BaseMap", chainTex);
            mm.SetColor("_BaseColor", Color.white);
            mm.SetFloat("_Surface", 0f); mm.SetFloat("_AlphaClip", 1f); mm.SetFloat("_Cutoff", 0.5f);
            mm.EnableKeyword("_ALPHATEST_ON"); mm.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            mm.SetOverrideTag("RenderType", "TransparentCutout");
            mm.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mm.SetFloat("_Smoothness", 0.1f);
            _netMat = mm;
            return mm;
        }
    }
}
