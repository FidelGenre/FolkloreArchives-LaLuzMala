// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  HouseBuilder.cs — "la casa de la vieja" en OldLadyRanch (620,600).
//  FASE 1: cáscara de la casa (estilo casa de campo patagónica:
//  base de canto rodado + columnas de piedra, paredes revoque
//  verde-oliva, techo de chapa a poca pendiente, chimenea de piedra,
//  galería/porche) + valla de madera perimetral con 2 portones.
//  FASE 2 (aparte): muebles de Poly Haven adentro.
//
//  Layout según el esquema del owner (planta, mirando desde arriba;
//  +X = este/derecha = galería+entrada+camino, +Z = norte/arriba):
//    Norte (fondo):  Baño (NO)   | Dormitorio 1 cama simple (NE)
//    Centro:         Living (O)  | Cocina (C) | Comedor (E, junto a galería)
//    Sur (frente):   Dormitorio 2 cama doble
//    Chimenea en la pared oeste. Galería sobresale al este.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class HouseBuilder
    {
        // ── Dimensiones ──────────────────────────────────────────────────────
        const float W = 14f;      // ancho casa (x, oeste→este)
        const float D = 12f;      // profundidad casa (z, sur→norte)
        const float H = 2.7f;     // altura de pared
        const float SB = 1.0f;    // altura de la base de piedra
        const float T = 0.25f;    // espesor de pared
        const float DoorH = 2.05f;
        const float WinSill = 1.0f, WinHead = 1.85f;

        const string TexDir = "Assets/ExternalAssets/HouseTextures/";

        public static void Build(Transform parent, Terrain terrain)
        {
            var group = BuilderUtils.Group(parent, "OldLadyHouse", Vector3.zero);

            // Posición: centro de la casa en OldLadyRanch, apoyada en el terreno.
            Vector2 c = MapLayout.OldLadyRanch;
            float gy = terrain != null ? terrain.SampleHeight(new Vector3(c.x, 0f, c.y)) : 20f;
            group.position = new Vector3(c.x - W * 0.5f, gy + 0.05f, c.y - D * 0.5f);

            var stone   = HouseMat("house_stone",   "PavingStones146", new Color(0.80f, 0.76f, 0.70f), 0.15f, 1.4f);
            var plaster = HouseMat("house_plaster",  "PaintedPlaster017", new Color(0.42f, 0.47f, 0.40f), 0.10f, 1.1f); // verde-oliva
            var roof    = HouseMat("house_roof",     "CorrugatedSteel007A", new Color(0.32f, 0.34f, 0.33f), 0.35f, 3.5f);
            var floor   = HouseMat("house_floor",    "WoodFloor051", new Color(0.68f, 0.58f, 0.44f), 0.12f, 3f);
            var wood    = HouseMat("house_wood",     "WoodFloor064", new Color(0.55f, 0.42f, 0.28f), 0.15f, 2f);

            var cube = BuilderUtils.PrimitiveMesh(PrimitiveType.Cube);
            var stoneCI = new List<CombineInstance>();
            var plasCI  = new List<CombineInstance>();
            var roofCI  = new List<CombineInstance>();
            var woodCI  = new List<CombineInstance>();

            // ── Piso ──
            BuildSlab(group, "HouseFloor", cube, floor,
                new Vector3(W * 0.5f, -0.05f, D * 0.5f), new Vector3(W, 0.1f, D));

            // ── Paredes exteriores (con aberturas) ──
            // Sur (z=0): ventana del dormitorio 2
            Wall(stoneCI, plasCI, cube, 'x', 0f, W, 0f,
                 new[] { Win(W * 0.5f, 1.6f) });
            // Norte (z=D): ventanas baño + dormitorio 1
            Wall(stoneCI, plasCI, cube, 'x', 0f, W, D,
                 new[] { Win(2.5f, 0.9f), Win(9.5f, 1.6f) });
            // Oeste (x=0): ventana del living (la chimenea se le pega por fuera)
            Wall(stoneCI, plasCI, cube, 'z', 0f, D, 0f,
                 new[] { Win(6f, 1.4f) });
            // Este (x=W): puerta de entrada (detrás de la galería) + ventana comedor
            Wall(stoneCI, plasCI, cube, 'z', 0f, D, W,
                 new[] { Door(6.5f, 1.1f), Win(9.5f, 1.4f), Win(2.5f, 1.4f) });

            // ── Paredes interiores (definen los ambientes) ──
            // Divisoria horizontal fondo/centro (z=8): puertas a baño, dorm1
            Wall(stoneCI, plasCI, cube, 'x', 0f, W, 8f,
                 new[] { Door(2.5f, 0.9f), Door(9.5f, 0.9f) }, interior: true);
            // Divisoria horizontal centro/sur (z=4): puerta al dormitorio 2
            Wall(stoneCI, plasCI, cube, 'x', 0f, W, 4f,
                 new[] { Door(7f, 0.9f) }, interior: true);
            // Vertical baño|dorm1 y living|cocina (x=5, z=4..D): puertas
            Wall(stoneCI, plasCI, cube, 'z', 4f, D, 5f,
                 new[] { Door(6.2f, 0.9f), Door(10.5f, 0.9f) }, interior: true);
            // Vertical cocina|comedor (x=9.5, z=4..8)
            Wall(stoneCI, plasCI, cube, 'z', 4f, 8f, 9.5f,
                 new[] { Door(6f, 0.9f) }, interior: true);

            // ── Galería / porche al este ──
            BuildPorch(group, cube, stoneCI, roofCI, woodCI);

            // ── Chimenea de piedra en la pared oeste ──
            AddBox(stoneCI, cube, 'x', -0.6f, 0.9f, 0f, H + 1.6f, 2.0f, cz: 6f); // cuerpo saliente + sube sobre el techo

            // ── Techo de chapa (dos faldones a poca pendiente, con alero) ──
            BuildRoof(group, cube, roof);

            // ── Materiales combinados ──
            BuilderUtils.BuildCombinedStatic(group, "House_Stone",   stoneCI, stone,   addCollider: true);
            BuilderUtils.BuildCombinedStatic(group, "House_Plaster", plasCI,  plaster, addCollider: true);
            BuilderUtils.BuildCombinedStatic(group, "House_Wood",    woodCI,  wood,    addCollider: false);
            BuilderUtils.BuildCombinedStatic(group, "House_RoofExtra", roofCI, roof,   addCollider: false);

            // ── Valla de madera perimetral + 2 portones ──
            BuildFence(group, cube, wood);

            // BuildCombinedStatic pone cada hijo en world (0,0,0); como el grupo está
            // desplazado a OldLadyRanch y la geometría se armó en frame LOCAL (0..W),
            // hay que resetear el localPosition de cada hijo a cero para que la casa
            // quede bajo el grupo (si no, aparece en el origen del mapa, junto al túnel).
            foreach (Transform child in group) child.localPosition = Vector3.zero;

            // ── FASE 2: muebles de Poly Haven adentro ──
            BuildFurniture(group, group.position.y);

            BuilderUtils.MarkStaticRecursive(group);
            Debug.Log("<color=lime>Casa de la vieja (Fase 1: cáscara + valla) construida en OldLadyRanch.</color>");
        }

        // ── Galería/porche (columnas de piedra + techo + piso) al este ──
        static void BuildPorch(Transform group, Mesh cube, List<CombineInstance> stoneCI,
                               List<CombineInstance> roofCI, List<CombineInstance> woodCI)
        {
            float px0 = W, px1 = W + 3.2f;   // sobresale 3.2m al este
            float pz0 = 4.5f, pz1 = 9.5f, pzc = (pz0 + pz1) * 0.5f;
            // piso de la galería (losa de madera, borde este de la casa)
            woodCI.Add(CI(cube, new Vector3((px0 + px1) * 0.5f, -0.04f, pzc),
                          Quaternion.identity, new Vector3(px1 - px0, 0.08f, pz1 - pz0)));
            // 3 columnas de piedra en el borde exterior de la galería
            foreach (float cz in new[] { pz0 + 0.4f, pzc, pz1 - 0.4f })
                AddBox(stoneCI, cube, 'z', cz - 0.25f, cz + 0.25f, 0f, H, 0.5f, cz: px1 - 0.4f);
            // viga superior de madera al frente
            AddBox(woodCI, cube, 'z', pz0, pz1, H - 0.25f, H, 0.3f, cz: px1 - 0.4f);
            // techo de la galería (chapa a poca pendiente, más bajo que el principal)
            roofCI.Add(CI(cube, new Vector3((px0 + px1) * 0.5f, H + 0.15f, pzc),
                          Quaternion.Euler(0f, 0f, -4f), new Vector3(3.6f, 0.12f, pz1 - pz0 + 0.6f)));
        }

        // ── Techo principal de chapa (dos faldones a poca pendiente) ──
        static void BuildRoof(Transform group, Mesh cube, Material roof)
        {
            var ci = new List<CombineInstance>();
            // faldón oeste (cubre x 0..8) y este (x 6..14), leve pendiente y solape,
            // con alero de 0.5m por lado. Ligeramente a distinta altura (como la foto).
            ci.Add(CI(cube, new Vector3(4.0f, H + 0.55f, D * 0.5f),
                      Quaternion.Euler(0f, 0f, 3f), new Vector3(9f, 0.14f, D + 1f)));
            ci.Add(CI(cube, new Vector3(10.5f, H + 0.30f, D * 0.5f),
                      Quaternion.Euler(0f, 0f, -3f), new Vector3(8f, 0.14f, D + 1f)));
            BuilderUtils.BuildCombinedStatic(group, "House_Roof", ci, roof, addCollider: false);
        }

        // ── Valla de madera perimetral + 2 portones ──
        static void BuildFence(Transform group, Mesh cube, Material wood)
        {
            // Lote: casa al oeste/fondo, patio grande al este. Coords locales.
            const float x0 = -6f, x1 = 30f, z0 = -7f, z1 = 19f;
            const float postH = 1.3f, rail = 0.12f, step = 2.5f;
            var ci = new List<CombineInstance>();

            // 2 portones = huecos en la valla ESTE (x=x1): centrados en z=3 y z=13
            var gates = new[] { new Vector2(3f, 2.4f), new Vector2(13f, 2.4f) };
            bool InGate(float z) { foreach (var g in gates) if (Mathf.Abs(z - g.x) < g.y * 0.5f) return true; return false; }

            void Post(float x, float z) =>
                ci.Add(CI(cube, new Vector3(x, postH * 0.5f, z), Quaternion.identity, new Vector3(0.14f, postH, 0.14f)));
            void RailX(float ax0, float ax1, float z) { foreach (float ry in new[] { 0.45f, 1.05f })
                ci.Add(CI(cube, new Vector3((ax0 + ax1) * 0.5f, ry, z), Quaternion.identity, new Vector3(ax1 - ax0, rail, rail))); }
            void RailZ(float x, float az0, float az1) { foreach (float ry in new[] { 0.45f, 1.05f })
                ci.Add(CI(cube, new Vector3(x, ry, (az0 + az1) * 0.5f), Quaternion.identity, new Vector3(rail, rail, az1 - az0))); }

            // Postes
            int nx = Mathf.CeilToInt((x1 - x0) / step);
            for (int i = 0; i <= nx; i++) { float x = Mathf.Lerp(x0, x1, i / (float)nx); Post(x, z0); Post(x, z1); }
            int nz = Mathf.CeilToInt((z1 - z0) / step);
            for (int i = 0; i <= nz; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)nz);
                Post(x0, z);                         // oeste
                if (!InGate(z)) Post(x1, z);         // este (salta portones)
            }

            // Travesaños: lados sur/norte/oeste enteros
            RailX(x0, x1, z0); RailX(x0, x1, z1); RailZ(x0, z0, z1);
            // Este: en tramos, salteando los 2 portones
            float[] cuts = { z0, gates[0].x - gates[0].y * 0.5f, gates[0].x + gates[0].y * 0.5f,
                                 gates[1].x - gates[1].y * 0.5f, gates[1].x + gates[1].y * 0.5f, z1 };
            for (int i = 0; i < cuts.Length; i += 2)
                if (cuts[i + 1] - cuts[i] > 0.1f) RailZ(x1, cuts[i], cuts[i + 1]);

            // Pilares de piedra a los lados de cada portón
            foreach (var g in gates)
                foreach (float dz in new[] { -g.y * 0.5f, g.y * 0.5f })
                    stonePillar(ci, cube, x1, g.x + dz);

            BuilderUtils.BuildCombinedStatic(group, "Fence", ci, wood, addCollider: true);
        }

        static void stonePillar(List<CombineInstance> ci, Mesh cube, float x, float z) =>
            ci.Add(CI(cube, new Vector3(x, 0.8f, z), Quaternion.identity, new Vector3(0.35f, 1.6f, 0.35f)));

        // ── Helpers de pared ─────────────────────────────────────────────────
        struct Op { public float pos, width, sill, head; }
        static Op Door(float pos, float w) => new Op { pos = pos, width = w, sill = 0f, head = DoorH };
        static Op Win(float pos, float w)  => new Op { pos = pos, width = w, sill = WinSill, head = WinHead };

        // Construye una pared (base piedra + revoque arriba) con aberturas.
        static void Wall(List<CombineInstance> stoneCI, List<CombineInstance> plasCI, Mesh cube,
                         char axis, float t0, float t1, float fixedC, Op[] ops, bool interior = false)
        {
            float baseSB = interior ? 0f : SB;   // interiores: todo revoque, sin base de piedra
            System.Array.Sort(ops, (a, b) => a.pos.CompareTo(b.pos));
            float prev = t0;
            foreach (var op in ops)
            {
                float s = op.pos - op.width * 0.5f, e = op.pos + op.width * 0.5f;
                // segmento sólido antes de la abertura
                Solid(stoneCI, plasCI, cube, axis, fixedC, prev, s, baseSB);
                // antepecho (bajo ventana)
                if (op.sill > 0f)
                {
                    if (baseSB > 0f) AddSpan(stoneCI, cube, axis, fixedC, s, e, 0f, Mathf.Min(op.sill, baseSB));
                    if (op.sill > baseSB) AddSpan(plasCI, cube, axis, fixedC, s, e, baseSB, op.sill);
                }
                // dintel (sobre la abertura)
                AddSpan(plasCI, cube, axis, fixedC, s, e, op.head, H);
                prev = e;
            }
            Solid(stoneCI, plasCI, cube, axis, fixedC, prev, t1, baseSB);
        }

        static void Solid(List<CombineInstance> stoneCI, List<CombineInstance> plasCI, Mesh cube,
                          char axis, float fixedC, float a, float b, float baseSB)
        {
            if (b - a < 0.02f) return;
            if (baseSB > 0f) AddSpan(stoneCI, cube, axis, fixedC, a, b, 0f, baseSB);
            AddSpan(plasCI, cube, axis, fixedC, a, b, baseSB, H);
        }

        // Caja de pared entre [a,b] a lo largo del eje, de y0 a y1.
        static void AddSpan(List<CombineInstance> ci, Mesh cube, char axis, float fixedC,
                            float a, float b, float y0, float y1)
        {
            if (b - a < 0.02f || y1 - y0 < 0.02f) return;
            float len = b - a, cy = (y0 + y1) * 0.5f, h = y1 - y0, m = (a + b) * 0.5f;
            Vector3 pos = axis == 'x' ? new Vector3(m, cy, fixedC) : new Vector3(fixedC, cy, m);
            Vector3 scl = axis == 'x' ? new Vector3(len, h, T) : new Vector3(T, h, len);
            ci.Add(CI(cube, pos, Quaternion.identity, scl));
        }

        // Caja genérica (para chimenea/porche): along axis con y0..y1.
        static void AddBox(List<CombineInstance> ci, Mesh cube, char axis, float a, float b,
                           float y0, float y1, float thick, float cz = 0f)
        {
            float len = Mathf.Max(0.01f, b - a), cy = (y0 + y1) * 0.5f, h = Mathf.Max(0.01f, y1 - y0), m = (a + b) * 0.5f;
            Vector3 pos = axis == 'x' ? new Vector3(m, cy, cz) : new Vector3(cz, cy, m);
            Vector3 scl = axis == 'x' ? new Vector3(len, h, thick) : new Vector3(thick, h, len);
            ci.Add(CI(cube, pos, Quaternion.identity, scl));
        }

        static GameObject BuildSlab(Transform group, string name, Mesh cube, Material mat, Vector3 pos, Vector3 scale)
        {
            if (mat == null) return null;
            var ci = new List<CombineInstance> { CI(cube, pos, Quaternion.identity, scale) };
            return BuilderUtils.BuildCombinedStatic(group, name, ci, mat, addCollider: true);
        }

        static CombineInstance CI(Mesh m, Vector3 pos, Quaternion rot, Vector3 scale) =>
            new CombineInstance { mesh = m, transform = Matrix4x4.TRS(pos, rot, scale) };

        // ── Muebles (FASE 2, Poly Haven FBX) ─────────────────────────────────
        const string FurnDir = "Assets/ExternalAssets/HouseFurniture/";

        static void BuildFurniture(Transform group, float floorWorldY)
        {
            // (modelo, x local, z local, rotación Y, altura objetivo en metros).
            // Coords locales de la casa: x 0..14 (O→E), z 0..12 (S→N), piso en y=0.
            var items = new (string m, float x, float z, float ry, float h)[]
            {
                // LIVING (x0..5, z4..8)
                ("Sofa_01",              1.0f, 6.0f,  90f, 0.85f),
                ("ArmChair_01",          3.6f, 4.8f, 210f, 0.90f),
                ("ArmChair_01",          3.6f, 7.2f, 150f, 0.90f),
                ("CoffeeTable_01",       2.3f, 6.0f,   0f, 0.42f),
                // COMEDOR (x9.5..14, z4..8)
                ("WoodenTable_02",      11.7f, 6.0f,   0f, 0.75f),
                ("WoodenChair_01",      10.5f, 6.0f,  90f, 0.92f),
                ("WoodenChair_01",      12.9f, 6.0f, -90f, 0.92f),
                ("WoodenChair_01",      11.7f, 4.9f,   0f, 0.92f),
                ("WoodenChair_01",      11.7f, 7.1f, 180f, 0.92f),
                // DORMITORIO 1 - cama simple (x5..14, z8..12)
                ("GothicBed_01",         8.0f,10.2f,   0f, 1.00f),
                ("ClassicNightstand_01", 5.8f,11.0f,   0f, 0.55f),
                // DORMITORIO 2 - cama doble (x0..14, z0..4)
                ("GothicBed_01",         7.0f, 1.9f, 180f, 1.10f),
                ("GothicCommode_01",     1.6f, 0.8f,   0f, 0.90f),
                ("ClassicNightstand_01", 4.4f, 0.7f,   0f, 0.55f),
                // GALERÍA (este)
                ("Rockingchair_01",     15.5f, 7.0f, -90f, 1.00f),
            };
            foreach (var it in items)
                PlaceFurniture(group, it.m, it.x, it.z, it.ry, it.h, floorWorldY);
        }

        static void PlaceFurniture(Transform group, string model, float lx, float lz,
                                   float rotY, float targetH, float floorWorldY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FurnDir + model + "/" + model + ".fbx");
            if (prefab == null) { Debug.LogWarning("[HouseBuilder] falta mueble: " + model); return; }

            // Holder: se rota/escala el HOLDER, NO el FBX. Los FBX de Poly Haven (Blender
            // Z-up) traen su propia rotación de eje; si la piso seteando la rotación
            // directo, el mueble se acuesta. Envolviéndolo, queda parado y sólo lo yaweo.
            var holder = new GameObject("Furn_" + model);
            holder.transform.SetParent(group, false);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(holder.transform, false);   // conserva rotación propia (parado)

            // material único (Poly Haven usa UN atlas por modelo): Diffuse + Normal
            var mat = FurnitureMat(model);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }

            // escalar a la altura real objetivo (antes de rotar; alto = eje Y del modelo parado)
            Bounds b = FurnitureBounds(holder);
            if (b.size.y > 0.001f) holder.transform.localScale = Vector3.one * (targetH / b.size.y);

            holder.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            holder.transform.localPosition = new Vector3(lx, 0f, lz);

            // asentar la base en el piso (mide bounds ya rotado/escalado)
            b = FurnitureBounds(holder);
            holder.transform.localPosition += Vector3.up * (floorWorldY - b.min.y);
        }

        static Bounds FurnitureBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static Material FurnitureMat(string model)
        {
            string dir = FurnDir + model + "/";
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + model + "_diff.jpg");
            var nor  = BuilderUtils.LoadAsNormalMap(dir + model + "_nor.jpg");
            if (diff == null) return BuilderUtils.Mat("furn_" + model, new Color(0.55f, 0.48f, 0.4f), 0f);
            return BuilderUtils.MatTextured("furn_" + model, diff, Color.white, 0.18f, nor);
        }

        static Material HouseMat(string name, string folder, Color tint, float smoothness, float tiling)
        {
            string dir = TexDir + folder + "/" + folder + "_1K-JPG_";
            var col = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Color.jpg");
            var nrm = BuilderUtils.LoadAsNormalMap(dir + "NormalGL.jpg");
            Material m = col != null
                ? BuilderUtils.MatTextured(name, col, tint, smoothness, nrm)
                : BuilderUtils.Mat(name, tint, 0f);
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            return m;
        }
    }
}
