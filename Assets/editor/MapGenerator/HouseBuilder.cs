// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  HouseBuilder.cs — "la casa de la vieja" en OldLadyRanch (ver MapLayout.cs).
//  Rediseño en PLANTA EN "L" (opción A del owner), estilo casa de
//  campo patagónica: base de canto rodado + revoque verde-oliva,
//  techos de chapa a DOS AGUAS que se cruzan a distinta altura,
//  chimenea de piedra, y galería techada abrigada en el codo interior.
//
//  Volumetría (planta desde arriba; +X = este/derecha = patio+entrada,
//  +Z = norte/arriba). Bounding box 16 (x) × 14 (z):
//
//     x0 ─────── x8 ──────── x16
//   z14 ┌─────────┬───────────┐
//       │  BAÑO   │           │
//       │ ┌───────┤           │   ← ala NORESTE = GALERÍA techada
//       │ │ DORM2 │  galería  │     (abierta al este y al norte,
//   z10 ├─┴───────┤  (codo)   │      columnas de piedra + deck)
//       │         │           │
//       │ LIVING  ╞═══════════╡  ← entrada por la galería al living
//    z7 │ (chim.  ├───────────┤
//       │  oeste) │           │
//    z5 ├─────────┤  COCINA-  │   ← ala ESTE (cuerpo perpendicular)
//       │  DORM   │  COMEDOR  │
//       │ PRINC.  │           │
//    z0 └─────────┴───────────┘
//     cuerpo principal (N-S)   ala este (E-W)
//
//  El cuerpo principal (x0..8) lleva un dos aguas con cumbrera N-S
//  (más alto). El ala este (x8..16, z0..7) lleva un dos aguas con
//  cumbrera E-W (más bajo) → se cruzan y dan la silueta rica. La
//  galería (x8..16, z7..14) tiene techo a un agua sobre columnas.
//  Chimenea de piedra saliente en la pared oeste, sube sobre la
//  cumbrera. Valla de madera perimetral con 2 portones al este.
//
//  MUEBLES: Kenney Furniture Kit (CC0, low-poly) en
//  Assets/ExternalAssets/HouseFurniture_Kenney/. El kit no trae textura;
//  cada submalla usa un material de color plano por nombre (wood/metal/
//  carpet/…). Se remapean por NOMBRE a materiales URP propios (paleta de
//  15 colores) para no salir rosa en URP. Ver BuildFurnitureKenney.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class HouseBuilder
    {
        // ── Dimensiones ──────────────────────────────────────────────────────
        const float BW = 16f;     // ancho del bounding box (x, oeste→este)
        const float BD = 14f;     // profundidad del bounding box (z, sur→norte)
        const float MBx1 = 8f;    // borde este del cuerpo principal / oeste del ala
        const float Wz  = 7f;     // borde norte del ala este / sur de la galería
        const float H = 2.7f;     // altura de pared (alero)
        const float SB = 1.0f;    // altura de la base de piedra
        const float T = 0.25f;    // espesor de pared
        const float DoorH = 2.05f;
        const float WinSill = 1.0f, WinHead = 1.85f;

        // Alturas de cumbrera (los dos aguas se cruzan a distinta altura)
        const float MainRidgeY = 4.6f;   // cumbrera del cuerpo principal (N-S), más alta
        const float WingRidgeY = 3.95f;  // cumbrera del ala este (E-W), más baja
        const float GalHiY = 2.7f, GalLoY = 2.3f;   // techo a un agua de la galería

        const string TexDir = "Assets/ExternalAssets/HouseTextures/";

        public static void Build(Transform parent, Terrain terrain)
        {
            if (UseAlpHouse) { BuildAlpHouse(parent, terrain); return; }

            var group = BuilderUtils.Group(parent, "OldLadyHouse", Vector3.zero);

            // Posición: bounding box centrado en OldLadyRanch, apoyado en el terreno.
            Vector2 c = MapLayout.OldLadyRanch;
            float gy = terrain != null ? terrain.SampleHeight(new Vector3(c.x, 0f, c.y)) : 20f;
            group.position = new Vector3(c.x - BW * 0.5f, gy + 0.05f, c.y - BD * 0.5f);

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

            // ── Piso (losa completa del bounding box; la galería lleva deck de madera) ──
            BuildSlab(group, "HouseFloor", cube, floor,
                new Vector3(BW * 0.5f, -0.05f, BD * 0.5f), new Vector3(BW, 0.1f, BD));
            woodCI.Add(CI(cube, new Vector3((MBx1 + BW) * 0.5f, 0.02f, (Wz + BD) * 0.5f),
                          Quaternion.identity, new Vector3(BW - MBx1, 0.06f, BD - Wz)));  // deck galería

            // ── Paredes exteriores del perímetro en L (con aberturas) ────────────
            // Sur (z=0, x0..16): ventana dorm. principal + ventana cocina
            Wall(stoneCI, plasCI, cube, 'x', 0f, BW, 0f,
                 new[] { Win(2.5f, 1.6f), Win(12f, 1.4f) });
            // Este del ala (x=16, z0..7): ventana del comedor al patio
            Wall(stoneCI, plasCI, cube, 'z', 0f, Wz, BW,
                 new[] { Win(3.5f, 1.4f) });
            // Norte del ala / sur de la galería (z=7, x8..16): puerta galería→cocina
            Wall(stoneCI, plasCI, cube, 'x', MBx1, BW, Wz,
                 new[] { Door(12f, 1.0f) });
            // Este del cuerpo principal, frente a la galería (x=8, z7..14):
            //   PUERTA DE ENTRADA (al living, z=8.5) + ventana
            Wall(stoneCI, plasCI, cube, 'z', Wz, BD, MBx1,
                 new[] { Door(8.5f, 1.1f), Win(12f, 1.2f) });
            // Norte (z=14, x0..8): ventana baño + ventana dorm2
            Wall(stoneCI, plasCI, cube, 'x', 0f, MBx1, BD,
                 new[] { Win(1.8f, 0.8f), Win(5.5f, 1.4f) });
            // Oeste (x=0, z0..14): ventana dorm. principal + ventana dorm2 (chimenea aparte)
            Wall(stoneCI, plasCI, cube, 'z', 0f, BD, 0f,
                 new[] { Win(2.5f, 1.4f), Win(11.5f, 1.4f) });

            // ── Paredes interiores (definen los ambientes) ───────────────────────
            // Divisoria cuerpo principal | ala este (x=8, z0..7): puerta living→cocina
            Wall(stoneCI, plasCI, cube, 'z', 0f, Wz, MBx1,
                 new[] { Door(6f, 1.0f) }, interior: true);
            // Dorm. principal (S) | living (z=5, x0..8): puerta
            Wall(stoneCI, plasCI, cube, 'x', 0f, MBx1, 5f,
                 new[] { Door(4f, 1.0f) }, interior: true);
            // Living | ambientes norte (z=10, x0..8): puerta
            Wall(stoneCI, plasCI, cube, 'x', 0f, MBx1, 10f,
                 new[] { Door(5.5f, 1.0f) }, interior: true);
            // Baño (O) | dorm2 (E) (x=3.5, z10..14): puerta
            Wall(stoneCI, plasCI, cube, 'z', 10f, BD, 3.5f,
                 new[] { Door(11f, 0.85f) }, interior: true);

            // ── Chimenea de piedra saliente en la pared oeste (living) ───────────
            AddBox(stoneCI, cube, 'z', 6.7f, 8.3f, 0f, MainRidgeY + 1.0f, 1.0f, cz: -0.1f);

            // ── Galería en el codo NE: columnas de piedra + viga + deck ya puesto ─
            BuildGallery(group, cube, stoneCI, woodCI);

            // ── Techos a dos aguas (cuerpo principal + ala) + hastiales + galería ─
            BuildRoofs(group, cube, roof, plasCI, roofCI);

            // ── Materiales combinados ──
            BuilderUtils.BuildCombinedStatic(group, "House_Stone",   stoneCI, stone,   addCollider: true);
            BuilderUtils.BuildCombinedStatic(group, "House_Plaster", plasCI,  plaster, addCollider: true);
            BuilderUtils.BuildCombinedStatic(group, "House_Wood",    woodCI,  wood,    addCollider: false);
            BuilderUtils.BuildCombinedStatic(group, "House_Roof",    roofCI,  roof,    addCollider: false);

            // ── Valla de madera perimetral + 2 portones ──
            BuildFence(group, cube, wood);

            // BuildCombinedStatic pone cada hijo en world (0,0,0); como el grupo está
            // desplazado a OldLadyRanch y la geometría se armó en frame LOCAL (0..BW),
            // hay que resetear el localPosition de cada hijo a cero para que la casa
            // quede bajo el grupo (si no, aparece en el origen del mapa, junto al túnel).
            foreach (Transform child in group) child.localPosition = Vector3.zero;

            // ── Muebles low-poly (Kenney). Va DESPUÉS del reset: sus localPosition
            //    son relativas al grupo (piso local y=0), no hay que resetearlas.
            BuildFurnitureKenney(group, group.position.y);

            BuilderUtils.MarkStaticRecursive(group);
            Debug.Log("<color=lime>Casa de la vieja (planta en L + muebles Kenney) construida en OldLadyRanch.</color>");
        }

        // ── Casa de la vieja: coloca el modelo ALP (Country house) en OldLadyRanch,
        //    convierte sus materiales Standard a URP (si no, magenta) y lo apoya en el
        //    terreno. Trae interior + colliders → se puede caminar adentro.
        static void BuildAlpHouse(Transform parent, Terrain terrain)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlpHousePrefab);
            if (prefab == null)
            {
                Debug.LogError("[HouseBuilder] no encontré la casa ALP en " + AlpHousePrefab +
                               " — ¿la importaste del Asset Store?");
                return;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "OldLadyHouse_ALP";
            inst.transform.SetParent(parent, true);

            Vector2 c = MapLayout.OldLadyHouseCenter;
            float gy = terrain != null
                ? terrain.SampleHeight(new Vector3(c.x, 0f, c.y)) + terrain.transform.position.y
                : 20f;
            inst.transform.position = new Vector3(c.x, gy + AlpHouseDropY, c.y);
            inst.transform.rotation = Quaternion.Euler(0f, AlpHouseYaw, 0f);
            inst.transform.localScale = Vector3.one * AlpHouseScale;

            // materiales built-in → URP (conserva textura + normal + emisión)
            int fixedMats = 0;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials;
                var outM = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    outM[i] = NappinUrp(src[i]);
                    if (outM[i] != src[i]) fixedMats++;
                }
                r.sharedMaterials = outM;
            }
            BuilderUtils.MarkStaticRecursive(inst.transform);

            // apoyar la BASE real en el suelo (robusto al pivote y a la escala): calculo
            // los bounds y bajo/subo la casa para que su punto más bajo quede en el terreno.
            Bounds hb = new Bounds(inst.transform.position, Vector3.one);
            var rb = inst.GetComponentsInChildren<Renderer>();
            if (rb.Length > 0)
            {
                hb = rb[0].bounds; foreach (var r in rb) hb.Encapsulate(r.bounds);
                float delta = (gy + AlpHouseDropY) - hb.min.y;
                inst.transform.position += new Vector3(0f, delta, 0f);
                hb.center += new Vector3(0f, delta, 0f);
            }

            // luces interiores cálidas (la casa viene a oscuras) + galpón
            // (muebles nappin desactivados: el owner quiere un pack de muebles viejos)
            AddInteriorLights(inst.transform, hb);
            if (UseNappinFurniture) BuildAlpFurniture(inst.transform, hb);
            BuildBarn(parent, terrain);

            Debug.Log($"<color=lime>Casa de la vieja: modelo ALP en OldLadyRanch (materiales a URP: {fixedMats}).</color>");
        }

        // Muebles nappin dentro de la casa ALP. 1er pase: agrupo un living en el CENTRO
        // (menos riesgo de chocar paredes interiores que no conozco). Se afina por captura.
        static void BuildAlpFurniture(Transform parent, Bounds hb)
        {
            float floorY = hb.min.y + 0.05f;
            // (nombre nappin, fx, fz, yaw) — fx/fz = fracción del footprint (0..1).
            // Repartido en zonas: LIVING (centro, ya andaba), DORMITORIO (frente-izq),
            // COCINA (frente-der), COMEDOR (fondo-der), BAÑO (fondo-izq). 1er pase: puede
            // que alguno atraviese una pared interior → se reubica por captura.
            var items = new (string n, float fx, float fz, float yaw)[]
            {
                // LIVING (centro)
                ("Sofa",         0.45f, 0.52f,   0f),
                ("CoffeTable",   0.50f, 0.44f,   0f),
                ("MediaConsole", 0.50f, 0.30f, 180f),
                ("Chair1",       0.58f, 0.55f, 210f),
                ("Lamp",         0.62f, 0.34f,   0f),
                ("WaterGarden",  0.62f, 0.62f,   0f),
                // DORMITORIO (frente-izquierda)
                ("DoubleBed",    0.22f, 0.26f,   0f),
                ("BedsideTable", 0.12f, 0.22f,   0f),
                ("Wardrobe",     0.13f, 0.42f,  90f),
                ("Dresser",      0.30f, 0.15f, 180f),
                // COCINA (frente-derecha)
                ("Stove",        0.80f, 0.16f, 180f),
                ("Fridge",       0.88f, 0.24f, -90f),
                ("KitchenSink",  0.72f, 0.16f, 180f),
                ("KitchenIsland",0.80f, 0.34f,   0f),
                // COMEDOR (fondo-derecha)
                ("LaunchTable",  0.75f, 0.72f,   0f),
                ("DiningChair",  0.68f, 0.72f,  90f),
                ("DiningChair",  0.82f, 0.72f, -90f),
                // BAÑO (fondo-izquierda)
                ("Toilet",       0.15f, 0.80f,  90f),
                ("BathroomSink", 0.15f, 0.68f,  90f),
                // varios
                ("Shelf1",       0.40f, 0.86f, 180f),
                ("CoatHanger",   0.52f, 0.88f, 180f),
                ("Storage1",     0.30f, 0.80f,   0f),
            };
            foreach (var it in items)
            {
                var pos = new Vector3(Mathf.Lerp(hb.min.x, hb.max.x, it.fx), floorY,
                                      Mathf.Lerp(hb.min.z, hb.max.z, it.fz));
                PlaceNappinWorld(parent, it.n, pos, it.yaw, floorY);
            }
        }

        // Instancia un prefab nappin en una posición del mundo, convierte sus materiales a
        // URP y apoya su base en floorY.
        static void PlaceNappinWorld(Transform parent, string napName, Vector3 pos, float yaw, float floorY)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NappinDir + napName + ".prefab");
            if (prefab == null) { Debug.LogWarning("[HouseBuilder] falta mueble nappin: " + napName); return; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "Furn_" + napName;
            inst.transform.SetParent(parent, true);
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.position = pos;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var src = r.sharedMaterials;
                var o = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) o[i] = NappinUrp(src[i]);
                r.sharedMaterials = o;
            }
            // apoyar la base en el piso
            var rs = inst.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                inst.transform.position += Vector3.up * (floorY - b.min.y);
            }
        }

        // Luces cálidas tenues repartidas dentro de la casa (a la altura del techo), con
        // parpadeo en algunas para el clima de terror. Sin sombras (perf en el bosque).
        static void AddInteriorLights(Transform parent, Bounds hb)
        {
            float y = hb.min.y + hb.size.y * 0.72f;   // cerca del techo
            const int nx = 2, nz = 2;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    float fx = (ix + 1f) / (nx + 1f), fz = (iz + 1f) / (nz + 1f);
                    var p = new Vector3(Mathf.Lerp(hb.min.x, hb.max.x, fx), y,
                                        Mathf.Lerp(hb.min.z, hb.max.z, fz));
                    var go = new GameObject($"HouseLight_{ix}{iz}");
                    go.transform.SetParent(parent, true);
                    go.transform.position = p;
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.72f, 0.42f);   // bombita cálida vieja
                    l.intensity = 2.0f;
                    l.range = 7.5f;
                    l.shadows = LightShadows.None;
                    if ((ix + iz) % 2 == 0) go.AddComponent<FolkloreArchives.LightFlicker>();
                }
        }

        // ── Galpón/granero rústico de madera + chapa, techo a DOS AGUAS (procedural) ──
        //    Detrás de la casa y mirando para el mismo lado (portón en +Z local).
        const float BarnYaw = 180f;
        static void BuildBarn(Transform parent, Terrain terrain)
        {
            var g = BuilderUtils.Group(parent, "OldLadyBarn", Vector3.zero);
            Vector2 c = MapLayout.OldLadyBarnCenter;
            float gy = terrain != null
                ? terrain.SampleHeight(new Vector3(c.x, 0f, c.y)) + terrain.transform.position.y
                : 20f;
            g.position = new Vector3(c.x, gy, c.y);
            g.rotation = Quaternion.Euler(0f, BarnYaw, 0f);

            // MISMA madera que la casa ALP (su propia textura), para que combine.
            const string AlpTex = "Assets/ALP_Assets/country house01/Textures/";
            var woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlpTex + "OldHouseMapWood01.png");
            var woodNrm = BuilderUtils.LoadAsNormalMap(AlpTex + "OldHouseMapWood01_N.png");
            var wood = woodTex != null
                ? BuilderUtils.MatTextured("barn_wood", woodTex, Color.white, 0.1f, woodNrm)
                : HouseMat("barn_wood", "WoodFloor064", new Color(0.46f, 0.36f, 0.24f), 0.1f, 2f);
            wood.mainTextureScale = new Vector2(3f, 2f);
            if (wood.HasProperty("_BaseMap")) wood.SetTextureScale("_BaseMap", new Vector2(3f, 2f));
            var roof = HouseMat("barn_roof", "CorrugatedSteel007A", new Color(0.32f, 0.33f, 0.32f), 0.3f, 2.5f);
            var beam = BuilderUtils.Mat("barn_beam", new Color(0.22f, 0.16f, 0.10f), 0f); // madera oscura (postes/vigas)
            // puerta = tablones (otra textura de madera del pack ALP), para que se note
            var doorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlpTex + "WoodPlank01.png");
            var doorNrm = BuilderUtils.LoadAsNormalMap(AlpTex + "WoodPlank01_N.png");
            var door = doorTex != null
                ? BuilderUtils.MatTextured("barn_door", doorTex, Color.white, 0.1f, doorNrm) : wood;
            door.mainTextureScale = new Vector2(1f, 2.5f);
            if (door.HasProperty("_BaseMap")) door.SetTextureScale("_BaseMap", new Vector2(1f, 2.5f));

            const float W = 7f, D = 9f, eaveH = 3.4f, ridgeY = 5.4f, t = 0.2f;

            // piso + paredes
            BarnBox(g, wood, new Vector3(0f, 0.05f, 0f), new Vector3(W, 0.1f, D));            // piso
            BarnBox(g, wood, new Vector3(-W / 2f, eaveH / 2f, 0f), new Vector3(t, eaveH, D)); // lateral izq
            BarnBox(g, wood, new Vector3(W / 2f, eaveH / 2f, 0f), new Vector3(t, eaveH, D));  // lateral der
            BarnBox(g, wood, new Vector3(0f, eaveH / 2f, -D / 2f), new Vector3(W, eaveH, t)); // hastial trasero
            // hastial frontal con PORTÓN doble (hueco 3.0)
            float sideW = W / 2f - 1.5f;
            BarnBox(g, wood, new Vector3(-(1.5f + sideW / 2f), eaveH / 2f, D / 2f), new Vector3(sideW, eaveH, t));
            BarnBox(g, wood, new Vector3(1.5f + sideW / 2f, eaveH / 2f, D / 2f), new Vector3(sideW, eaveH, t));
            BarnBox(g, wood, new Vector3(0f, (2.8f + eaveH) / 2f, D / 2f), new Vector3(3.0f, eaveH - 2.8f, t)); // dintel
            // hojas del portón (un poco entornadas, madera oscura)
            // hojas del portón con BISAGRA en el marco (no giran sobre su centro). Una
            // bien abierta, la otra entornada (galpón abandonado). Sin collider → se entra.
            BarnDoorLeaf(g, door, -1.5f, +1f, -70f, D / 2f, 1.45f, 2.7f, 1.35f);  // izquierda, abierta
            BarnDoorLeaf(g, door, 1.5f, -1f, 22f, D / 2f, 1.45f, 2.7f, 1.35f);    // derecha, entornada

            // TECHO a dos aguas: dos planos inclinados que se juntan en la cumbrera
            float run = W / 2f, rise = ridgeY - eaveH;
            float planeLen = Mathf.Sqrt(run * run + rise * rise);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var rL = BarnBox(g, roof, new Vector3(-run / 2f, (eaveH + ridgeY) / 2f, 0f), new Vector3(planeLen, 0.16f, D + 1.2f));
            rL.transform.localRotation = Quaternion.Euler(0f, 0f, ang);   // sube hacia el centro
            var rR = BarnBox(g, roof, new Vector3(run / 2f, (eaveH + ridgeY) / 2f, 0f), new Vector3(planeLen, 0.16f, D + 1.2f));
            rR.transform.localRotation = Quaternion.Euler(0f, 0f, -ang);
            BarnBox(g, beam, new Vector3(0f, ridgeY, 0f), new Vector3(0.25f, 0.25f, D + 1.2f)); // viga de cumbrera

            // cerrar los HASTIALES (triángulos bajo el techo) con tablones escalonados,
            // así no queda abierto arriba. Frente y fondo.
            GableFill(g, wood, D / 2f, W, eaveH, ridgeY, t);
            GableFill(g, wood, -D / 2f, W, eaveH, ridgeY, t);

            // postes de esquina (madera oscura) — dan estructura de galpón
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    BarnBox(g, beam, new Vector3(sx * (W / 2f - 0.15f), eaveH / 2f, sz * (D / 2f - 0.15f)),
                            new Vector3(0.3f, eaveH, 0.3f));

            // luz tenue parpadeante dentro del galpón
            var lg = new GameObject("BarnLight");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = new Vector3(0f, eaveH - 0.4f, 0f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.66f, 0.36f);
            l.intensity = 1.4f; l.range = 7f; l.shadows = LightShadows.None;
            lg.AddComponent<FolkloreArchives.LightFlicker>();

            BuilderUtils.MarkStaticRecursive(g);
        }

        static GameObject BarnBox(Transform g, Material m, Vector3 localCenter, Vector3 size)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);  // trae BoxCollider (paredes sólidas)
            cube.transform.SetParent(g, false);
            cube.transform.localPosition = localCenter;
            cube.transform.localScale = size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = m;
            return cube;
        }

        // Hoja de portón que gira sobre la BISAGRA (en el marco), no sobre su centro:
        // un pivote vacío en el marco + la hoja colgada a un costado. dir=+1 la hoja va
        // hacia +x desde la bisagra, dir=-1 hacia -x.
        static void BarnDoorLeaf(Transform g, Material m, float hingeX, float dir, float openDeg,
                                 float z, float w, float h, float yc)
        {
            var hinge = new GameObject("BarnDoor");
            hinge.transform.SetParent(g, false);
            hinge.transform.localPosition = new Vector3(hingeX, yc, z);
            hinge.transform.localRotation = Quaternion.Euler(0f, openDeg, 0f);
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.transform.SetParent(hinge.transform, false);
            leaf.transform.localPosition = new Vector3(dir * w / 2f, 0f, 0f);
            leaf.transform.localScale = new Vector3(w, h, 0.08f);
            leaf.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(leaf.GetComponent<Collider>());
        }

        // Rellena el triángulo del hastial (entre el alero y la cumbrera) con tablones
        // horizontales que se angostan hacia arriba → tapa el hueco sin dejarlo abierto.
        static void GableFill(Transform g, Material mat, float zPlane, float W, float eaveH, float ridgeY, float t)
        {
            const int N = 7;
            float dh = (ridgeY - eaveH) / N;
            for (int i = 0; i < N; i++)
            {
                float yTop = eaveH + (i + 1) * dh;                       // ancho según el borde superior
                float w = W * (ridgeY - yTop) / (ridgeY - eaveH);         // 0 en la cumbrera
                if (w < 0.15f) continue;
                BarnBox(g, mat, new Vector3(0f, eaveH + (i + 0.5f) * dh, zPlane), new Vector3(w, dh + 0.03f, t));
            }
        }

        // ── Galería del codo NE (x8..16, z7..14): abierta al este y al norte ─────
        static void BuildGallery(Transform group, Mesh cube, List<CombineInstance> stoneCI,
                                 List<CombineInstance> woodCI)
        {
            const float postH = 2.5f, postW = 0.4f;
            void Post(float px, float pz) =>
                stoneCI.Add(CI(cube, new Vector3(px, postH * 0.5f, pz), Quaternion.identity,
                               new Vector3(postW, postH, postW)));
            // columnas en los bordes abiertos (este x=16, norte z=14)
            Post(BW, Wz + 0.5f); Post(BW, 10.5f); Post(BW, BD);
            Post(12f, BD);
            // vigas de madera sobre las columnas (borde este y borde norte)
            AddBox(woodCI, cube, 'z', Wz + 0.5f, BD, postH - 0.2f, postH, 0.28f, cz: BW);
            AddBox(woodCI, cube, 'x', MBx1, BW, postH - 0.2f, postH, 0.28f, cz: BD);
        }

        // ── Techos a dos aguas + hastiales (triángulos) + techo de galería ───────
        static void BuildRoofs(Transform group, Mesh cube, Material roof,
                               List<CombineInstance> plasCI, List<CombineInstance> roofCI)
        {
            // Cuerpo principal: dos aguas con cumbrera N-S en x=4 (centro de 0..8).
            //   Faldón oeste (x-0.5..4.2) y este (x3.8..8.5), leve solape en la cumbrera.
            AddSlope(roofCI, cube, 'x', -0.5f, GalHiY - 0.24f, 4.2f, MainRidgeY + 0.1f,
                     zc: BD * 0.5f, depth: BD + 1f);
            AddSlope(roofCI, cube, 'x', 8.5f, GalHiY - 0.24f, 3.8f, MainRidgeY + 0.1f,
                     zc: BD * 0.5f, depth: BD + 1f);
            // Hastiales (triángulos de revoque) del cuerpo principal: sur (z=0) y norte (z=14)
            AddGable(plasCI, 'x', 0f,  0f, MBx1, H, 4f, MainRidgeY);
            AddGable(plasCI, 'x', BD,  0f, MBx1, H, 4f, MainRidgeY);

            // Ala este: dos aguas con cumbrera E-W en z=3.5 (centro de 0..7).
            //   Faldón sur (z-0.5..3.7) y norte (z3.3..7.5).
            AddSlope(roofCI, cube, 'z', -0.5f, GalHiY - 0.18f, 3.7f, WingRidgeY + 0.07f,
                     zc: (MBx1 + BW) * 0.5f, depth: BW - MBx1 + 1f);   // "zc/depth" = eje X aquí
            AddSlope(roofCI, cube, 'z', 7.5f, GalHiY - 0.18f, 3.3f, WingRidgeY + 0.07f,
                     zc: (MBx1 + BW) * 0.5f, depth: BW - MBx1 + 1f);
            // Hastial del ala mirando al patio (x=16); el otro extremo (x=8) muere contra
            // el cuerpo principal, no lleva triángulo visible.
            AddGable(plasCI, 'z', BW,  0f, Wz, H, 3.5f, WingRidgeY);

            // Galería: techo a un agua, cae del cuerpo principal (x=8, alto) al este (x=16).
            AddSlope(roofCI, cube, 'x', MBx1 - 0.3f, GalHiY + 0.05f, BW + 0.6f, GalLoY,
                     zc: (Wz + BD) * 0.5f, depth: BD - Wz + 0.8f);
        }

        // Faldón de techo como caja fina inclinada entre dos puntos (eje-lo, eje-hi) en
        // el plano correspondiente. axis 'x' → inclina en X (cumbrera N-S), depth va en Z.
        // axis 'z' → inclina en Z (cumbrera E-W), "zc"/"depth" son el centro/ancho en X.
        static void AddSlope(List<CombineInstance> ci, Mesh cube, char axis,
                             float t0, float y0, float t1, float y1, float zc, float depth)
        {
            Vector2 lo = new Vector2(t0, y0), hi = new Vector2(t1, y1);
            Vector2 mid = (lo + hi) * 0.5f;
            float len = Vector2.Distance(lo, hi);
            float ang = Mathf.Atan2(hi.y - lo.y, hi.x - lo.x) * Mathf.Rad2Deg;
            const float thick = 0.14f;
            if (axis == 'x')
                ci.Add(CI(cube, new Vector3(mid.x, mid.y, zc), Quaternion.Euler(0f, 0f, ang),
                          new Vector3(len, thick, depth)));
            else // 'z': inclina en Z; el faldón se extiende en X (centro zc, ancho depth)
                ci.Add(CI(cube, new Vector3(zc, mid.y, mid.x), Quaternion.Euler(-ang, 0f, 0f),
                          new Vector3(depth, thick, len)));
        }

        // Hastial: prisma triangular (relleno de revoque bajo el dos aguas) como malla.
        // plane 'x' → triángulo en el plano X-Y a z=fixedC, extruido en Z por 'thick'.
        // plane 'z' → triángulo en el plano Z-Y a x=fixedC, extruido en X por 'thick'.
        static void AddGable(List<CombineInstance> ci, char plane, float fixedC,
                             float a, float b, float baseY, float apexT, float apexY)
        {
            const float thick = T;
            float h = thick * 0.5f;
            Vector3 P(float t, float y, float off) =>
                plane == 'x' ? new Vector3(t, y, fixedC + off) : new Vector3(fixedC + off, y, t);
            var v = new[] {
                P(a, baseY, -h), P(b, baseY, -h), P(apexT, apexY, -h),   // 0,1,2 cara frontal
                P(a, baseY,  h), P(b, baseY,  h), P(apexT, apexY,  h),   // 3,4,5 cara trasera
            };
            var uv = new Vector2[6];
            for (int i = 0; i < 6; i++) uv[i] = plane == 'x' ? new Vector2(v[i].x, v[i].y)
                                                              : new Vector2(v[i].z, v[i].y);
            var tris = new List<int>();
            void Tri(int i0, int i1, int i2) { tris.Add(i0); tris.Add(i1); tris.Add(i2);
                                               tris.Add(i0); tris.Add(i2); tris.Add(i1); } // doble cara
            void Quad(int i0, int i1, int i2, int i3) { Tri(i0, i1, i2); Tri(i0, i2, i3); }
            Tri(0, 1, 2); Tri(3, 4, 5);       // triángulos de las dos caras
            Quad(0, 1, 4, 3);                 // base
            Quad(1, 2, 5, 4); Quad(2, 0, 3, 5); // los dos faldones
            var m = new Mesh { vertices = v, uv = uv, triangles = tris.ToArray() };
            m.RecalculateNormals();
            ci.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
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

        // Caja genérica (para chimenea/columnas/vigas): along axis con y0..y1.
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

        // ── Muebles low-poly (Kenney Furniture Kit, CC0) ─────────────────────
        // El kit NO trae textura: cada submalla usa un material de color plano
        // (wood/metal/carpet/…) definido en el FBX. En URP esos materiales
        // importados salen ROSA, así que remapeo cada submaterial por NOMBRE a un
        // material URP propio con el color del kit (paleta de 15). Un material por
        // color → buen batching y nada de rosa.
        const string KFurnDir = "Assets/ExternalAssets/HouseFurniture_Kenney/";
        // PS1 Kitchen Pack (Dazed Crow Games, licencia FREE con atribución — NO CC0;
        // no subir los .fbx/.png a un repo público). Un atlas 256² compartido.
        const string KFurnPS1Dir = "Assets/ExternalAssets/HouseFurniture_PS1/";
        // House Interior Pack (nappin.dev, lowpoly texturizado). Prefabs con materiales
        // built-in (Standard) → hay que convertirlos a URP al instanciar o salen magenta.
        const string NappinDir = "Assets/nappin/HouseInteriorPack/Prefabs/(Prb)";

        // ── Casa de la vieja: modelo ALP (Country house, Aleksey8310) en vez de la
        //    cáscara procedural. Trae interior completo + colliders; materiales Standard
        //    → se convierten a URP al colocar. La casa procedural queda como respaldo
        //    (UseAlpHouse=false para volver a ella).
        const bool UseAlpHouse = true;
        const string AlpHousePrefab = "Assets/ALP_Assets/country house01/Prefabs/House_Prefab.prefab";
        // giro: entrada mirando hacia el CAMPAMENTO. Con la vieja en su posición nueva
        // (235,388) tras el intercambio con Campo de Caza, el campamento (410,442) queda
        // al este-noreste → yaw = atan2(dx,dz) ≈ 72°. (Antes era 180°, cuando el
        // campamento quedaba casi derecho al sur desde la posición vieja de la casa.)
        const float AlpHouseYaw = 72f;
        const float AlpHouseScale = 1.35f; // agrandada: puertas/techos por encima del jugador (2.4m)
        const bool UseNappinFurniture = false; // muebles nappin OFF (el owner usará un pack de muebles viejos)
        const float AlpHouseDropY = 0f;    // ajuste fino de altura (si flota/se hunde)

        // Convierte los materiales built-in del pack nappin a URP (una vez por material,
        // cacheado): crea un URP/Lit copiando la textura del gradiente y el color/emisión.
        static readonly Dictionary<Material, Material> _napMatCache = new Dictionary<Material, Material>();
        static Material NappinUrp(Material src)
        {
            if (src == null) return BuilderUtils.Mat("nap_null", new Color(0.6f, 0.6f, 0.6f), 0f);
            if (src.shader != null && src.shader.name.Contains("Universal")) return src;
            if (_napMatCache.TryGetValue(src, out var cached)) return cached;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { name = "nap_" + src.name };
            Texture tex = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            if (tex != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            Color col = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tex != null ? Color.white : col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
            // normal map (la casa ALP trae detalle en normales que si no se perdería)
            Texture bump = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
            if (bump != null && m.HasProperty("_BumpMap"))
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", bump);
            }
            // materiales de luz del pack (EmissiveWarm) → emisión cálida
            if (src.name.Contains("Emissive") || src.IsKeywordEnabled("_EMISSION"))
            {
                m.EnableKeyword("_EMISSION");
                Color em = src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor")
                                                             : new Color(1f, 0.85f, 0.55f);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", em);
            }
            _napMatCache[src] = m;
            return m;
        }

        static readonly Dictionary<string, Color> KPalette = new Dictionary<string, Color>
        {
            { "wood",         new Color(0.896f, 0.602f, 0.393f) },
            { "woodDark",     new Color(0.678f, 0.456f, 0.299f) },
            { "metal",        new Color(0.741f, 0.823f, 0.840f) },
            { "metalDark",    new Color(0.306f, 0.388f, 0.388f) },
            { "metalMedium",  new Color(0.369f, 0.467f, 0.467f) },
            { "metalLight",   new Color(0.937f, 0.980f, 0.957f) },
            { "carpet",       new Color(0.943f, 0.367f, 0.343f) },
            { "carpetWhite",  new Color(0.900f, 0.905f, 0.880f) },
            { "carpetBlue",   new Color(0.356f, 0.517f, 0.868f) },
            { "carpetDarker", new Color(0.608f, 0.298f, 0.285f) },
            { "glass",        new Color(0.698f, 0.827f, 0.769f) },
            { "lamp",         new Color(1.000f, 0.914f, 0.588f) },
            { "plant",        new Color(0.182f, 0.700f, 0.400f) },
            { "fur",          new Color(0.647f, 0.459f, 0.298f) },
            { "_defaultMat",  new Color(0.780f, 0.780f, 0.780f) },
        };

        // Tabla de muebles (modelo, x local, z local, yaw, alturaObjetivo[m], baseY[m]).
        // Coords locales (planta en L): x 0..16 (O→E), z 0..14 (S→N), piso y=0.
        // baseY>0 = colgado de la pared (alacenas altas, campana, espejo).
        // El ÍNDICE de cada fila da el nombre "Furn_##_modelo" del objeto en la escena.
        // (La persistencia de muebles se sacó: los muebles se colocan siempre por código;
        //  las ediciones a mano ya no sobreviven a un regenerate.)
        // ⚠ Posiciones/rotaciones/alturas son 1er pase estimado → ajustar acá en la tabla
        //   (el "facing" nativo de los modelos varía).
        public static readonly (string m, float x, float z, float ry, float h, float by)[] FurnitureItems =
        {
                // Muebles del pack nappin (NAP_*) donde hay equivalente; los que nappin no
                // tiene (alfombra, radio, TV vintage, bañera, banco) siguen Kenney/PS1.
                // ── DORMITORIO PRINCIPAL (x0..8, z0..5) ──
                ("NAP_DoubleBed",       4.0f,  1.5f,   0f, 0.85f, 0f),
                ("NAP_BedsideTable",    1.1f,  0.5f,   0f, 0.45f, 0f),
                ("NAP_BedsideTable",    6.9f,  0.5f,   0f, 0.45f, 0f),
                ("NAP_Wardrobe",        7.3f,  3.9f, -90f, 1.80f, 0f),
                ("NAP_Dresser",         0.7f,  3.9f,  90f, 0.75f, 0f),
                // ── LIVING (x0..8, z5..10; chimenea O, entrada E) ──
                ("rugRectangle",        4.0f,  7.6f,   0f, 0.03f, 0f),
                ("NAP_Sofa",            6.1f,  7.6f,  90f, 0.75f, 0f),
                ("NAP_Chair1",          2.4f,  9.0f, 200f, 0.75f, 0f),
                ("NAP_CoffeTable",      4.2f,  7.6f,   0f, 0.40f, 0f),
                ("radio",               4.7f,  7.6f,   0f, 0.20f, 0.40f),
                ("NAP_Shelf1",          2.4f,  9.6f, 180f, 1.80f, 0f),
                ("NAP_Lamp",            7.4f,  9.4f,   0f, 1.50f, 0f),
                ("NAP_MediaConsole",    0.7f,  6.4f,  90f, 0.50f, 0f),
                ("televisionVintage",   0.85f, 6.4f,  90f, 0.45f, 0.50f),
                // ── COCINA-COMEDOR (ala este, x8..16, z0..7) ──
                //    mesada/alacenas/mesa/sillas = PS1 Kitchen Pack (texturizado).
                //    bacha/cocina/campana/heladera = nappin.
                ("PS1_Cabinet_Base",    9.6f,  0.6f,   0f, 0.90f, 0f),
                ("NAP_KitchenSink",    11.0f,  0.6f,   0f, 0.90f, 0f),
                ("NAP_Stove",          12.4f,  0.6f,   0f, 0.90f, 0f),
                ("PS1_Cabinet_Base",   13.8f,  0.6f,   0f, 0.90f, 0f),
                ("PS1_Cabinet_Upper",   9.6f,  0.5f,   0f, 0.70f, 1.55f),
                ("PS1_Cabinet_Upper",  13.8f,  0.5f,   0f, 0.70f, 1.55f),
                ("NAP_SmokeVent",      12.4f,  0.45f,  0f, 0.55f, 1.55f),
                ("NAP_Fridge",         15.3f,  1.4f, -90f, 1.25f, 0f),
                ("PS1_Table",          11.5f,  4.4f,   0f, 0.75f, 0f),
                ("PS1_Chair",          10.4f,  4.4f,  90f, 0.90f, 0f),
                ("PS1_Chair",          12.6f,  4.4f, -90f, 0.90f, 0f),
                ("PS1_Chair",          11.5f,  3.4f,   0f, 0.90f, 0f),
                ("PS1_Chair",          11.5f,  5.4f, 180f, 0.90f, 0f),
                // ── DORMITORIO 2 (simple, x3.5..8, z10..14) ──
                ("NAP_SingleBed",       6.0f, 12.9f, 180f, 0.80f, 0f),
                ("NAP_BedsideTable",    4.3f, 13.4f,   0f, 0.45f, 0f),
                ("NAP_Storage1",        7.4f, 11.0f, -90f, 1.70f, 0f),
                // ── BAÑO (x0..3.5, z10..14) ──
                ("NAP_Toilet",          0.8f, 13.3f,  90f, 0.70f, 0f),
                ("NAP_BathroomSink",    0.8f, 11.2f,  90f, 0.80f, 0f),
                ("NAP_Mirror1",         0.35f,11.2f,  90f, 0.55f, 1.15f),
                ("bathtub",             2.8f, 12.4f, -90f, 0.55f, 0f),
                // ── GALERÍA (codo NE, x8..16, z7..14) ──
                ("bench",              14.6f,  8.2f, -90f, 0.50f, 0f),
                ("NAP_Chair2",         12.6f, 12.2f,  40f, 0.75f, 0f),
                ("NAP_LaunchTable",    13.7f, 11.4f,   0f, 0.55f, 0f),
                ("NAP_WaterGarden",    15.3f, 13.3f,   0f, 0.90f, 0f),
                ("NAP_CoatHanger",      8.7f,  7.6f,   0f, 1.70f, 0f),
        };

        static void BuildFurnitureKenney(Transform group, float floorWorldY)
        {
            for (int i = 0; i < FurnitureItems.Length; i++)
            {
                var it = FurnitureItems[i];
                PlaceFurniture(group, i, it.m, it.x, it.z, it.ry, it.h, it.by, floorWorldY);
            }
        }

        static void PlaceFurniture(Transform group, int id, string model, float lx, float lz,
                                   float rotY, float targetH, float baseY, float floorWorldY)
        {
            bool isPs1 = model.StartsWith("PS1_");
            bool isNap = model.StartsWith("NAP_");
            string path = isNap ? NappinDir + model.Substring(4) + ".prefab"
                        : (isPs1 ? KFurnPS1Dir : KFurnDir) + model + ".fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[HouseBuilder] falta mueble: " + model + " (" + path + ")"); return; }

            // El objeto "Furn_##_modelo" ES el FBX (no un holder vacío): así, al clickearlo
            // en la escena, Unity lo selecciona directo → lo que movés/rotás es lo que se
            // guarda. Se preserva la rotación/escala de eje propia del import (r0/s0) y
            // sólo se le compone el yaw, así el modelo queda parado igual que con holder.
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = $"Furn_{id:D2}_{model}";
            inst.transform.SetParent(group, false);
            Quaternion r0 = inst.transform.localRotation;   // rotación de eje del import
            Vector3    s0 = inst.transform.localScale;       // escala del import

            // Materiales: nappin = convertir sus materiales built-in a URP (conservando la
            // textura del gradiente); PS1 = atlas texturizado compartido; Kenney = remapeo
            // por NOMBRE a color plano de la paleta.
            Material ps1 = isPs1 ? Ps1Mat() : null;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                var src = r.sharedMaterials;
                var outMats = new Material[Mathf.Max(1, src.Length)];
                for (int i = 0; i < outMats.Length; i++)
                    outMats[i] = isNap ? NappinUrp(i < src.Length ? src[i] : null)
                        : isPs1 ? ps1
                        : KenneyMat(i < src.Length && src[i] != null ? src[i].name : null);
                r.sharedMaterials = outMats;
            }

            // Colocación procedural (1er pase): yaw (preservando r0), escala a la altura
            // objetivo (preservando s0), y asentar la base en el piso.
            inst.transform.localRotation = Quaternion.Euler(0f, rotY, 0f) * r0;
            Bounds b = FurnitureBounds(inst);
            // nappin ya viene a escala real (metros) → NO re-escalar por altura (si no
            // quedaban enanos). Kenney/PS1 sí se normalizan a la altura objetivo.
            if (!isNap && b.size.y > 0.001f) inst.transform.localScale = s0 * (targetH / b.size.y);

            inst.transform.localPosition = new Vector3(lx, 0f, lz);
            b = FurnitureBounds(inst);   // ya rotado/escalado
            inst.transform.localPosition += Vector3.up * (floorWorldY + baseY - b.min.y);
        }

        // Material único del PS1 Kitchen Pack: atlas 256² compartido, filtrado POINT +
        // sin mipmaps para el crunch retro PS1 (fuerza el import una vez).
        static Material _ps1Mat;
        static Material Ps1Mat()
        {
            if (_ps1Mat != null) return _ps1Mat;
            string texPath = KFurnPS1Dir + "stove_atlas.png";
            if (AssetImporter.GetAtPath(texPath) is TextureImporter imp &&
                (imp.filterMode != FilterMode.Point || imp.mipmapEnabled))
            {
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            _ps1Mat = atlas != null
                ? BuilderUtils.MatTextured("ps1_kitchen", atlas, Color.white, 0.05f)
                : BuilderUtils.Mat("ps1_kitchen_fallback", new Color(0.6f, 0.55f, 0.5f));
            return _ps1Mat;
        }

        // Mapea el nombre de material importado del FBX Kenney → material URP de la paleta.
        static Material KenneyMat(string rawName)
        {
            string key = "_defaultMat";
            if (!string.IsNullOrEmpty(rawName))
            {
                // el import puede venir como "wood", "wood (Instance)", "metalDark 1"…
                // → elegir la clave de paleta MÁS LARGA contenida en el nombre.
                string n = rawName.ToLowerInvariant();
                int best = -1;
                foreach (var kv in KPalette)
                {
                    string k = kv.Key.ToLowerInvariant();
                    if (n.Contains(k) && k.Length > best) { key = kv.Key; best = k.Length; }
                }
            }
            return BuilderUtils.Mat("kfurn_" + key, KPalette[key], key == "lamp" ? 0.5f : 0f);
        }

        static Bounds FurnitureBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
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
