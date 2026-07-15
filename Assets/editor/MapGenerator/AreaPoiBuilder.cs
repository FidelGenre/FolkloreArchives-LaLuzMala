// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  AreaPoiBuilder.cs — construye las ZONAS y PUNTOS DE INTERÉS nuevos
//  del MapPlan (ideas): estepa+molino, mallín, roquedal, bosque quemado,
//  orilla+muelle, Difunta Correa, Gauchito Gil, árbol del ahorcado,
//  antena, corrales, YPF (con sedán reusado), estancia (galpón).
//  Todo PROCEDURAL (primitivas + materiales) o reusando assets que ya
//  están en el proyecto (rocas HQP, sedán PSXCars). Lo faltante (capilla,
//  El Familiar, etc.) se agrega después.
//  Cada lugar deja su NOMBRE flotando encima (BuilderUtils.Label).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class AreaPoiBuilder
    {
        // rocas low-poly que YA están en el proyecto (para el roquedal)
        const string RockDir = "Assets/HQP STUDIOS/Rocks and Terrains Pack - Low Poly/Models/Rocks/Block Rocks/Block Rocks/";
        const string SedanObj = "Assets/ExternalAssets/PSXCars/Sedan/Car5.obj";

        // materiales (compartidos, cacheados como asset por BuilderUtils.Mat)
        static Material Rust, MetalDark, Wood, ShrineRed, FlagRed, Bottle, Bone, Reed,
                        Rope, Ash, Burnt, DarkWater, Candle, RedLight, StoneGrey;

        public static void Build(Transform parent, Terrain t)
        {
            var root = BuilderUtils.Group(parent, "AreasAndPOIs", Vector3.zero);

            Rust      = BuilderUtils.Mat("rust",       new Color(0.42f, 0.28f, 0.20f));
            MetalDark = BuilderUtils.Mat("metaldark",  new Color(0.22f, 0.23f, 0.25f));
            Wood      = BuilderUtils.Mat("wood",       new Color(0.34f, 0.24f, 0.15f));
            ShrineRed = BuilderUtils.Mat("shrinered",  new Color(0.52f, 0.07f, 0.07f));
            FlagRed   = BuilderUtils.Mat("flagred",    new Color(0.60f, 0.09f, 0.09f));
            Bottle    = BuilderUtils.Mat("bottleglass",new Color(0.45f, 0.62f, 0.66f));
            Bone      = BuilderUtils.Mat("bone",       new Color(0.82f, 0.80f, 0.72f));
            Reed      = BuilderUtils.Mat("reed",       new Color(0.44f, 0.50f, 0.28f));
            Rope      = BuilderUtils.Mat("rope",       new Color(0.60f, 0.52f, 0.36f));
            Ash       = BuilderUtils.Mat("ash",        new Color(0.13f, 0.12f, 0.12f));
            Burnt     = BuilderUtils.Mat("burnttrunk", new Color(0.08f, 0.07f, 0.06f));
            DarkWater = BuilderUtils.Mat("darkwater",  new Color(0.05f, 0.08f, 0.09f));
            Candle    = BuilderUtils.Mat("candleflame",new Color(1f, 0.72f, 0.32f), 3f);
            RedLight  = BuilderUtils.Mat("redbeacon",  new Color(1f, 0.12f, 0.10f), 4f);
            StoneGrey = BuilderUtils.Mat("stonegrey",  new Color(0.42f, 0.42f, 0.44f));

            Estepa(root, t);
            Mallin(root, t);
            Roquedal(root, t);
            BurntForestArea(root, t);
            LakeShoreDock(root, t);
            DifuntaCorrea(root, t);
            GauchitoGil(root, t);
            HangedTree(root, t);
            Antenna(root, t);
            Corrales(root, t);
            YpfStation(root, t);
            Estancia(root, t);

            // set-dressing fijo → static batching (menos draw calls). Excepto luces.
            BuilderUtils.MarkStaticRecursive(root);
        }

        // ---------------- ESTEPA + MOLINO ----------------
        static void Estepa(Transform parent, Terrain t)
        {
            var g = BuilderUtils.Group(parent, "Estepa", BuilderUtils.Ground(t, MapLayout.EstepaCenter));
            BuilderUtils.Label(g, "ESTEPA", g.position + Vector3.up * 7f);

            // molino australiano oxidado: torre reticulada + cabezal + aspas + veleta
            var mp = BuilderUtils.Ground(t, MapLayout.Molino);
            var mill = BuilderUtils.Group(g, "MolinoOxidado", mp);
            BuilderUtils.Label(mill, "MOLINO", mp + Vector3.up * 10f);
            float towerH = 7.5f;
            // 4 patas que convergen (torre reticulada baja)
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                Vector3 baseP = mp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.6f;
                Vector3 topP  = mp + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.35f + Vector3.up * towerH;
                Beam(mill, baseP, topP, 0.10f, Rust);
            }
            // cruces (2 niveles)
            RingRungs(mill, mp, 1.2f, 2.6f, Rust);
            RingRungs(mill, mp, 0.7f, 5.2f, Rust);
            // cabezal
            Vector3 hub = mp + Vector3.up * (towerH + 0.2f);
            BuilderUtils.Prim(PrimitiveType.Cube, "Head", mill, hub, new Vector3(0.7f, 0.6f, 1.1f), Rust);
            // rueda de aspas (varias palas radiales sobre un disco)
            var wheel = BuilderUtils.Group(mill, "Aspas", hub + new Vector3(0f, 0f, 0.7f));
            for (int b = 0; b < 12; b++)
            {
                float a = b * 30f;
                var blade = BuilderUtils.Prim(PrimitiveType.Cube, "Blade" + b, wheel,
                    wheel.position, new Vector3(0.28f, 1.9f, 0.05f), MetalDark);
                blade.transform.RotateAround(wheel.position, Vector3.forward, a);
            }
            BuilderUtils.Prim(PrimitiveType.Cylinder, "Axle", mill, hub + new Vector3(0f, 0f, 0.45f),
                new Vector3(0.12f, 0.5f, 0.12f), MetalDark, new Vector3(90f, 0f, 0f));
            // veleta (cola)
            BuilderUtils.Prim(PrimitiveType.Cube, "Vane", mill, hub + new Vector3(0f, 0.1f, -1.2f),
                new Vector3(0.05f, 0.9f, 1.4f), Rust);

            // alambrado (2 tiradas) + huesos de oveja
            Fence(g, t, MapLayout.EstepaCenter + new Vector2(-18, -6), MapLayout.EstepaCenter + new Vector2(20, 2), 6f);
            Fence(g, t, MapLayout.EstepaCenter + new Vector2(8, 18), MapLayout.EstepaCenter + new Vector2(14, -20), 6f);
            SheepBones(g, t, MapLayout.EstepaCenter + new Vector2(6, -4));
            SheepBones(g, t, MapLayout.EstepaCenter + new Vector2(-10, 8));
        }

        // ---------------- MALLÍN (pantano) ----------------
        static void Mallin(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.Mallin);
            var g = BuilderUtils.Group(parent, "Mallin", p);
            BuilderUtils.Label(g, "MALLIN", p + Vector3.up * 7f);

            // charco de agua estancada oscura (quad plano casi a ras del piso)
            BuilderUtils.Prim(PrimitiveType.Cube, "AguaEstancada", g, p + Vector3.up * 0.03f,
                new Vector3(22f, 0.06f, 16f), DarkWater);
            // juncos / totora (matas de tiras verdes)
            for (int i = 0; i < 60; i++)
            {
                Vector2 o = Random.insideUnitCircle * 11f;
                Vector3 rp = BuilderUtils.Ground(t, MapLayout.Mallin.x + o.x, MapLayout.Mallin.y + o.y);
                float h = Random.Range(0.9f, 1.7f);
                var reed = BuilderUtils.Prim(PrimitiveType.Cube, "Junco", g, rp + Vector3.up * h * 0.5f,
                    new Vector3(0.06f, h, 0.06f), Reed, new Vector3(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)));
                DestroyCol(reed);
            }
            // troncos podridos + pasarela de tablones podridos
            for (int i = 0; i < 3; i++)
            {
                Vector2 o = Random.insideUnitCircle * 8f;
                Vector3 rp = BuilderUtils.Ground(t, MapLayout.Mallin.x + o.x, MapLayout.Mallin.y + o.y);
                BuilderUtils.Prim(PrimitiveType.Cylinder, "TroncoPodrido", g, rp + Vector3.up * 0.25f,
                    new Vector3(0.35f, 1.6f, 0.35f), Burnt, new Vector3(90f, Random.Range(0f, 360f), 0f));
            }
            Vector3 a = BuilderUtils.Ground(t, MapLayout.Mallin.x - 10f, MapLayout.Mallin.y);
            for (int i = 0; i < 8; i++)
            {
                Vector3 pk = a + new Vector3(i * 2.4f, 0.12f, (i % 2) * 0.15f);
                BuilderUtils.Prim(PrimitiveType.Cube, "Tabla" + i, g, pk, new Vector3(2.2f, 0.08f, 0.9f), Wood,
                    new Vector3(0f, Random.Range(-6f, 6f), 0f));
            }
        }

        // ---------------- ROQUEDAL (rocas reusadas HQP) ----------------
        static void Roquedal(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.Roquedal);
            var g = BuilderUtils.Group(parent, "Roquedal", p);
            BuilderUtils.Label(g, "ROQUEDAL", p + Vector3.up * 8f);

            var rocks = new System.Collections.Generic.List<GameObject>();
            for (int n = 1; n <= 20; n++)
            {
                var a = AssetDatabase.LoadAssetAtPath<GameObject>(RockDir + "Block_Rock_" + n + ".fbx");
                if (a != null) rocks.Add(a);
            }
            if (rocks.Count == 0)
            {
                // fallback: bloques de piedra procedural si el pack no está
                for (int i = 0; i < 14; i++)
                {
                    Vector2 o = Random.insideUnitCircle * 13f;
                    Vector3 rp = BuilderUtils.Ground(t, MapLayout.Roquedal.x + o.x, MapLayout.Roquedal.y + o.y);
                    float s = Random.Range(1.5f, 4.5f);
                    BuilderUtils.Prim(PrimitiveType.Cube, "Roca" + i, g, rp + Vector3.up * s * 0.35f,
                        new Vector3(s, s * 0.8f, s * Random.Range(0.7f, 1.3f)), StoneGrey,
                        new Vector3(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f)));
                }
                return;
            }
            for (int i = 0; i < 18; i++)
            {
                Vector2 o = Random.insideUnitCircle * 14f;
                Vector3 rp = BuilderUtils.Ground(t, MapLayout.Roquedal.x + o.x, MapLayout.Roquedal.y + o.y);
                var src = rocks[Random.Range(0, rocks.Count)];
                var inst = (GameObject)Object.Instantiate(src, g);
                inst.name = "Roca_" + i;
                inst.transform.position = rp;
                inst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                inst.transform.localScale = Vector3.one * Random.Range(1.6f, 4.0f);
            }
        }

        // ---------------- BOSQUE QUEMADO ----------------
        static void BurntForestArea(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.BurntForest);
            var g = BuilderUtils.Group(parent, "BosqueQuemado", p);
            BuilderUtils.Label(g, "BOSQUE QUEMADO", p + Vector3.up * 8f);

            for (int i = 0; i < 22; i++)
            {
                Vector2 o = Random.insideUnitCircle * 16f;
                Vector3 rp = BuilderUtils.Ground(t, MapLayout.BurntForest.x + o.x, MapLayout.BurntForest.y + o.y);
                float h = Random.Range(3.5f, 6.5f);
                float lean = Random.Range(0f, 14f);
                // tronco negro cónico (sin follaje) — cilindro fino y alto
                var trunk = BuilderUtils.Prim(PrimitiveType.Cylinder, "TroncoQuemado" + i, g,
                    rp + Vector3.up * h * 0.5f, new Vector3(Random.Range(0.2f, 0.4f), h * 0.5f, Random.Range(0.2f, 0.4f)),
                    Burnt, new Vector3(lean, Random.Range(0f, 360f), lean * 0.5f));
                // un par de ramas peladas
                if (Random.value > 0.5f)
                    BuilderUtils.Prim(PrimitiveType.Cylinder, "Rama", g, rp + Vector3.up * (h * 0.8f),
                        new Vector3(0.08f, Random.Range(0.6f, 1.2f), 0.08f), Burnt,
                        new Vector3(Random.Range(40f, 80f), Random.Range(0f, 360f), 0f));
            }
        }

        // ---------------- ORILLA DEL LAGO + MUELLE ----------------
        static void LakeShoreDock(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.LakeShore);
            var g = BuilderUtils.Group(parent, "OrillaLago", p);
            BuilderUtils.Label(g, "ORILLA DEL LAGO", p + Vector3.up * 7f);

            // muelle de tablones apuntando hacia el centro del lago
            Vector2 toLake = (MapLayout.CentralLakeCenter - MapLayout.LakeShore).normalized;
            float deckY = MapLayout.CentralLakeLevel + 0.6f;
            for (int i = 0; i < 8; i++)
            {
                Vector2 plankXZ = MapLayout.LakeShore + toLake * (i * 1.6f + 1f);
                Vector3 pk = new Vector3(plankXZ.x, deckY, plankXZ.y);
                BuilderUtils.Prim(PrimitiveType.Cube, "Tabla" + i, g, pk, new Vector3(2.0f, 0.12f, 1.6f), Wood,
                    new Vector3(0f, Random.Range(-4f, 4f), 0f));
                if (i % 2 == 0) // pilotes
                    BuilderUtils.Prim(PrimitiveType.Cylinder, "Pilote" + i, g,
                        new Vector3(plankXZ.x, MapLayout.CentralLakeBed + 1f, plankXZ.y),
                        new Vector3(0.18f, (deckY - MapLayout.CentralLakeBed) * 0.5f + 0.5f, 0.18f), Wood);
            }
            // unos juncos en el borde
            for (int i = 0; i < 12; i++)
            {
                Vector2 o = Random.insideUnitCircle * 6f;
                Vector3 rp = BuilderUtils.Ground(t, MapLayout.LakeShore.x + o.x, MapLayout.LakeShore.y + o.y);
                var reed = BuilderUtils.Prim(PrimitiveType.Cube, "Junco", g, rp + Vector3.up * 0.6f,
                    new Vector3(0.06f, 1.2f, 0.06f), Reed, new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)));
                DestroyCol(reed);
            }
        }

        // ---------------- DIFUNTA CORREA ----------------
        static void DifuntaCorrea(Transform parent, Terrain t)
        {
            var p = RoadShoulder(t, MapLayout.DifuntaCorrea, 8f);
            var g = BuilderUtils.Group(parent, "DifuntaCorrea", p);
            BuilderUtils.Label(g, "DIFUNTA CORREA", p + Vector3.up * 6f);

            // montaña de botellas de agua (pila cónica)
            for (int i = 0; i < 90; i++)
            {
                Vector2 o = Random.insideUnitCircle * (2.4f * (1f - i / 120f));
                float y = (i / 90f) * 1.6f;
                var b = BuilderUtils.Prim(PrimitiveType.Cylinder, "Botella", g,
                    p + new Vector3(o.x, y + 0.15f, o.y), new Vector3(0.12f, 0.16f, 0.12f), Bottle,
                    new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f)));
                DestroyCol(b);
            }
            // cruz + banderas rojas
            BuilderUtils.Prim(PrimitiveType.Cube, "CruzV", g, p + new Vector3(0f, 1.1f, 0f), new Vector3(0.12f, 2.2f, 0.12f), Wood);
            BuilderUtils.Prim(PrimitiveType.Cube, "CruzH", g, p + new Vector3(0f, 1.6f, 0f), new Vector3(0.8f, 0.12f, 0.12f), Wood);
            RedFlags(g, p, 5, 2.6f);
        }

        // ---------------- GAUCHITO GIL ----------------
        static void GauchitoGil(Transform parent, Terrain t)
        {
            var p = RoadShoulder(t, MapLayout.GauchitoGil, 9f);
            var g = BuilderUtils.Group(parent, "GauchitoGil", p);
            BuilderUtils.Label(g, "GAUCHITO GIL", p + Vector3.up * 6f);

            // ermita roja chica (cajón + techo a dos aguas)
            BuilderUtils.Prim(PrimitiveType.Cube, "Ermita", g, p + Vector3.up * 0.7f, new Vector3(1.3f, 1.4f, 1.1f), ShrineRed);
            BuilderUtils.Prim(PrimitiveType.Cube, "Techo", g, p + Vector3.up * 1.55f, new Vector3(1.5f, 0.18f, 1.3f), Wood, new Vector3(0f, 0f, 12f));
            BuilderUtils.Prim(PrimitiveType.Cube, "Techo2", g, p + Vector3.up * 1.55f, new Vector3(1.5f, 0.18f, 1.3f), Wood, new Vector3(0f, 0f, -12f));
            // vela (emisiva) + luz cálida tenue
            var flame = BuilderUtils.Prim(PrimitiveType.Cylinder, "Vela", g, p + new Vector3(0.3f, 1.5f, 0f), new Vector3(0.08f, 0.12f, 0.08f), Candle);
            DestroyCol(flame);
            WarmPoint(g, p + Vector3.up * 1.6f, 6f, 1.4f, new Color(1f, 0.5f, 0.2f));
            RedFlags(g, p, 6, 3f);
        }

        // ---------------- ÁRBOL DEL AHORCADO + CEMENTERIO ----------------
        static void HangedTree(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.HangedTree);
            var g = BuilderUtils.Group(parent, "ArbolDelAhorcado", p);
            BuilderUtils.Label(g, "ARBOL DEL AHORCADO", p + Vector3.up * 8f);

            // árbol solitario seco (tronco + rama gruesa horizontal)
            BuilderUtils.Prim(PrimitiveType.Cylinder, "Tronco", g, p + Vector3.up * 2.6f, new Vector3(0.5f, 2.6f, 0.5f), Wood);
            Vector3 branch = p + new Vector3(0f, 4.6f, 0f);
            BuilderUtils.Prim(PrimitiveType.Cylinder, "Rama", g, branch, new Vector3(0.22f, 1.6f, 0.22f), Wood, new Vector3(0f, 0f, 90f));
            // soga colgando
            Vector3 knot = branch + new Vector3(1.3f, 0f, 0f);
            BuilderUtils.Prim(PrimitiveType.Cylinder, "Soga", g, knot + Vector3.down * 0.9f, new Vector3(0.04f, 0.9f, 0.04f), Rope);
            // lazo (aro)
            var loop = BuilderUtils.Prim(PrimitiveType.Cylinder, "Lazo", g, knot + Vector3.down * 1.9f, new Vector3(0.28f, 0.02f, 0.28f), Rope, new Vector3(90f, 0f, 0f));
            DestroyCol(loop);
            // cementerio: cruces torcidas
            for (int i = 0; i < 5; i++)
            {
                Vector2 o = new Vector2(Random.Range(-6f, 6f), Random.Range(4f, 10f));
                Vector3 cp = BuilderUtils.Ground(t, MapLayout.HangedTree.x + o.x, MapLayout.HangedTree.y + o.y);
                float tilt = Random.Range(-16f, 16f);
                BuilderUtils.Prim(PrimitiveType.Cube, "CruzV" + i, g, cp + Vector3.up * 0.55f, new Vector3(0.1f, 1.1f, 0.1f), Wood, new Vector3(0f, Random.Range(0f, 60f), tilt));
                BuilderUtils.Prim(PrimitiveType.Cube, "CruzH" + i, g, cp + Vector3.up * 0.8f, new Vector3(0.55f, 0.1f, 0.1f), Wood, new Vector3(0f, Random.Range(0f, 60f), tilt));
            }
        }

        // ---------------- ANTENA / REPETIDORA ----------------
        static void Antenna(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.Antenna);
            var g = BuilderUtils.Group(parent, "Antena", p);
            BuilderUtils.Label(g, "ANTENA", p + Vector3.up * 32f);

            float h = 28f;
            for (int i = 0; i < 4; i++)
            {
                float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 baseP = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 2.2f;
                Vector3 topP  = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.4f + Vector3.up * h;
                Beam(g, baseP, topP, 0.14f, MetalDark);
            }
            for (float y = 3f; y < h; y += 4f)
                RingRungs(g, p, Mathf.Lerp(2.0f, 0.5f, y / h), y, MetalDark);
            // baliza roja
            var beacon = BuilderUtils.Prim(PrimitiveType.Sphere, "Baliza", g, p + Vector3.up * (h + 0.6f), Vector3.one * 0.6f, RedLight);
            DestroyCol(beacon);
            var l = new GameObject("BalizaLuz").AddComponent<Light>();
            l.transform.SetParent(g); l.transform.position = p + Vector3.up * (h + 0.6f);
            l.type = LightType.Point; l.color = new Color(1f, 0.1f, 0.1f); l.range = 30f; l.intensity = 2.5f; l.shadows = LightShadows.None;
        }

        // ---------------- CORRALES / BAÑADERO ----------------
        static void Corrales(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.Corrales);
            var g = BuilderUtils.Group(parent, "Corrales", p);
            BuilderUtils.Label(g, "CORRALES", p + Vector3.up * 6f);

            // corral cuadrado de postes + alambre
            Vector2 c = MapLayout.Corrales; float s = 9f;
            Fence(g, t, c + new Vector2(-s, -s), c + new Vector2(s, -s), 3f);
            Fence(g, t, c + new Vector2(s, -s), c + new Vector2(s, s), 3f);
            Fence(g, t, c + new Vector2(s, s), c + new Vector2(-s, s), 3f);
            Fence(g, t, c + new Vector2(-s, s), c + new Vector2(-s, -s), 3f);
            // bañadero (pileta larga angosta)
            BuilderUtils.Prim(PrimitiveType.Cube, "Banadero", g, p + Vector3.up * 0.3f, new Vector3(4f, 0.6f, 1.0f), StoneGrey);
            BuilderUtils.Prim(PrimitiveType.Cube, "Agua", g, p + Vector3.up * 0.45f, new Vector3(3.7f, 0.3f, 0.75f), DarkWater);
            SheepBones(g, t, c + new Vector2(3, -2));
        }

        // ---------------- ESTACIÓN YPF ----------------
        static void YpfStation(Transform parent, Terrain t)
        {
            var p = RoadShoulder(t, MapLayout.YpfStation, 11f);
            var g = BuilderUtils.Group(parent, "EstacionYPF", p);
            BuilderUtils.Label(g, "ESTACION YPF", p + Vector3.up * 8f);

            // techo (canopy) sobre 2 columnas
            BuilderUtils.Prim(PrimitiveType.Cube, "Techo", g, p + Vector3.up * 4.2f, new Vector3(9f, 0.4f, 6f), MetalDark);
            BuilderUtils.Prim(PrimitiveType.Cube, "ColA", g, p + new Vector3(-3.5f, 2f, -2f), new Vector3(0.4f, 4f, 0.4f), MetalDark);
            BuilderUtils.Prim(PrimitiveType.Cube, "ColB", g, p + new Vector3(3.5f, 2f, 2f), new Vector3(0.4f, 4f, 0.4f), MetalDark);
            // 2 surtidores
            for (int i = 0; i < 2; i++)
            {
                Vector3 sp = p + new Vector3((i - 0.5f) * 3.5f, 0.9f, 0f);
                BuilderUtils.Prim(PrimitiveType.Cube, "Surtidor" + i, g, sp, new Vector3(0.7f, 1.8f, 0.9f), Rust);
                BuilderUtils.Prim(PrimitiveType.Cube, "Display" + i, g, sp + new Vector3(0f, 0.4f, 0.48f), new Vector3(0.5f, 0.4f, 0.06f), MetalDark);
            }
            // tubo de luz parpadeante (por ahora fija, blanca fría)
            WarmPoint(g, p + Vector3.up * 3.9f, 12f, 1.6f, new Color(0.8f, 0.85f, 1f));
            // Falcon tirado (reusa el sedán PSXCars, ladeado)
            var sedan = AssetDatabase.LoadAssetAtPath<GameObject>(SedanObj);
            if (sedan != null)
            {
                var car = (GameObject)Object.Instantiate(sedan, g);
                car.name = "FalconAbandonado";
                car.transform.position = p + new Vector3(6f, 0.4f, -3f);
                car.transform.rotation = Quaternion.Euler(6f, 55f, 10f); // ladeado/abandonado
            }
        }

        // ---------------- ESTANCIA + GALPÓN ----------------
        static void Estancia(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.Estancia);
            var g = BuilderUtils.Group(parent, "Estancia", p);
            BuilderUtils.Label(g, "ESTANCIA", p + Vector3.up * 9f);

            // casco: reusa la casa rural si está; si no, un cajón
            var house = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ALP_Assets/country house01/Models/House.fbx");
            if (house != null)
            {
                var h = (GameObject)Object.Instantiate(house, g);
                h.name = "CascoEstancia";
                h.transform.position = p;
                h.transform.rotation = Quaternion.Euler(0f, 120f, 0f);
            }
            else
            {
                BuilderUtils.Prim(PrimitiveType.Cube, "Casco", g, p + Vector3.up * 1.6f, new Vector3(7f, 3.2f, 6f), Wood);
            }

            // galpón de esquila (procedural: nave grande + techo a dos aguas)
            Vector3 bp = p + new Vector3(-14f, 0f, 6f);
            var barn = BuilderUtils.Group(g, "GalponEsquila", bp);
            BuilderUtils.Label(barn, "GALPON (EL FAMILIAR)", bp + Vector3.up * 6f);
            BuilderUtils.Prim(PrimitiveType.Cube, "Paredes", barn, bp + Vector3.up * 2f, new Vector3(9f, 4f, 12f), Rust);
            BuilderUtils.Prim(PrimitiveType.Cube, "TechoA", barn, bp + Vector3.up * 4.4f, new Vector3(6f, 0.2f, 12.5f), MetalDark, new Vector3(0f, 0f, 28f));
            BuilderUtils.Prim(PrimitiveType.Cube, "TechoB", barn, bp + Vector3.up * 4.4f, new Vector3(6f, 0.2f, 12.5f), MetalDark, new Vector3(0f, 0f, -28f));
            // portón oscuro + "cadenas" colgando (marcador de El Familiar)
            BuilderUtils.Prim(PrimitiveType.Cube, "Porton", barn, bp + new Vector3(0f, 1.8f, 6.1f), new Vector3(3.5f, 3.6f, 0.2f), MetalDark);
            for (int i = 0; i < 3; i++)
                BuilderUtils.Prim(PrimitiveType.Cylinder, "Cadena" + i, barn, bp + new Vector3(-1f + i, 2.6f, 6.3f), new Vector3(0.05f, 0.7f, 0.05f), MetalDark);
        }

        // ================= helpers =================

        // punto sobre el hombro NORTE de la ruta (para POIs al borde del asfalto)
        static Vector3 RoadShoulder(Terrain t, Vector2 onRoad, float northOffset)
        {
            Vector2 shoulder = onRoad + new Vector2(0f, northOffset); // norte = z+ (lado del bosque; el sur es el lago)
            return BuilderUtils.Ground(t, shoulder);
        }

        // viga entre dos puntos (cubo estirado y orientado)
        static void Beam(Transform parent, Vector3 a, Vector3 b, float thick, Material m)
        {
            Vector3 mid = (a + b) * 0.5f;
            float len = Vector3.Distance(a, b);
            var go = BuilderUtils.Prim(PrimitiveType.Cube, "Beam", parent, mid, new Vector3(thick, len, thick), m);
            go.transform.up = (b - a).normalized;
        }

        // anillo de 4 barras horizontales a una altura (cruces de la torre)
        static void RingRungs(Transform parent, Vector3 baseP, float radius, float y, Material m)
        {
            for (int i = 0; i < 4; i++)
            {
                float a0 = i * 90f * Mathf.Deg2Rad, a1 = (i + 1) * 90f * Mathf.Deg2Rad;
                Vector3 p0 = baseP + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius + Vector3.up * y;
                Vector3 p1 = baseP + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius + Vector3.up * y;
                Beam(parent, p0, p1, 0.07f, m);
            }
        }

        // alambrado: postes cada `step` + 2 hilos de alambre
        static void Fence(Transform parent, Terrain t, Vector2 a, Vector2 b, float step)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / step));
            Vector3 pa = Vector3.zero;
            for (int i = 0; i <= n; i++)
            {
                Vector2 xz = Vector2.Lerp(a, b, i / (float)n);
                Vector3 gp = BuilderUtils.Ground(t, xz.x, xz.y);
                BuilderUtils.Prim(PrimitiveType.Cube, "Poste", parent, gp + Vector3.up * 0.6f, new Vector3(0.08f, 1.2f, 0.08f), Wood);
                if (i > 0)
                {
                    Beam(parent, pa + Vector3.up * 0.5f, gp + Vector3.up * 0.5f, 0.02f, MetalDark);
                    Beam(parent, pa + Vector3.up * 0.95f, gp + Vector3.up * 0.95f, 0.02f, MetalDark);
                }
                pa = gp;
            }
        }

        // banderas rojas descoloridas en palitos
        static void RedFlags(Transform parent, Vector3 center, int count, float radius)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Vector3 fp = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                BuilderUtils.Prim(PrimitiveType.Cube, "Palo" + i, parent, fp + Vector3.up * 0.9f, new Vector3(0.05f, 1.8f, 0.05f), Wood);
                var flag = BuilderUtils.Prim(PrimitiveType.Cube, "Bandera" + i, parent, fp + new Vector3(0.25f, 1.5f, 0f), new Vector3(0.5f, 0.35f, 0.02f), FlagRed);
                DestroyCol(flag);
            }
        }

        // huesos de oveja: unos pocos cilindros/esferas claros
        static void SheepBones(Transform parent, Terrain t, Vector2 at)
        {
            Vector3 c = BuilderUtils.Ground(t, at.x, at.y);
            for (int i = 0; i < 5; i++)
            {
                Vector3 o = new Vector3(Random.Range(-1.2f, 1.2f), 0.06f, Random.Range(-1.2f, 1.2f));
                var b = BuilderUtils.Prim(PrimitiveType.Cylinder, "Hueso" + i, parent, c + o, new Vector3(0.05f, 0.28f, 0.05f), Bone,
                    new Vector3(90f, Random.Range(0f, 360f), 0f));
                DestroyCol(b);
            }
            var skull = BuilderUtils.Prim(PrimitiveType.Sphere, "Craneo", parent, c + Vector3.up * 0.12f, Vector3.one * 0.22f, Bone);
            DestroyCol(skull);
        }

        static void WarmPoint(Transform parent, Vector3 pos, float range, float intensity, Color col)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(parent); go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = col; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        }

        static void DestroyCol(GameObject g)
        {
            var c = g.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }
    }
}
