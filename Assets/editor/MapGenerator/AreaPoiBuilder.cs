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
        const string SedanObj = "Assets/ExternalAssets/PSXCars/Sedan/Car5.obj";

        // modelos DESCARGADOS (Sketchfab) — el owner los pone en estas carpetas. El código
        // los carga con fallback a lo procedural si todavía no están (busca cualquier
        // .fbx/.glb/.gltf/.obj dentro de la carpeta, sin importar el nombre interno).
        const string DirWindmill = "Assets/ExternalAssets/Windmill";
        const string DirTower    = "Assets/ExternalAssets/RadioTower";
        const string DirDock     = "Assets/ExternalAssets/Dock";
        const string DirDeadTree = "Assets/ExternalAssets/DeadTree";
        const string DirBarn     = "Assets/ExternalAssets/BarnShed";
        const string DirFence    = "Assets/ExternalAssets/ChainFence";
        const string DirGasProps = "Assets/ExternalAssets/GasStationProps";
        const string DirChurch   = "Assets/ExternalAssets/Church";

        // materiales (compartidos, cacheados como asset por BuilderUtils.Mat)
        static Material Rust, MetalDark, Wood, ShrineRed, FlagRed, Bottle, Bone, Reed,
                        Rope, Ash, Burnt, DarkWater, Candle, RedLight, StoneGrey;

        // Cantidad de objetos que se registran para persistencia manual (IDs
        // 0..PersistCount-1). Debe coincidir con la cantidad de Reg(...) en Build,
        // en su orden de creación. Si reordenás/agregás/sacás, re-guardá el layout.
        public const int PersistCount = 13;
        static Transform Reg(Transform g) => ManualLayoutPersistence.Register("AreaPois", g);

        public static void Build(Transform parent, Terrain t)
        {
            ManualLayoutPersistence.Begin("AreaPois");   // carga overrides manuales guardados (si hay)
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

            Reg(Estepa(root, t));
            Reg(Mallin(root, t));
            Reg(BurntForestArea(root, t));
            Reg(LakeShoreDock(root, t));
            Reg(DifuntaCorrea(root, t));
            Reg(GauchitoGil(root, t));
            Reg(HangedTree(root, t));
            Reg(Antenna(root, t));
            Reg(Corrales(root, t));
            Reg(YpfStation(root, t));
            Reg(Estancia(root, t));
            Reg(Capilla(root, t));

            // set-dressing fijo → static batching (menos draw calls). Excepto luces.
            BuilderUtils.MarkStaticRecursive(root);
        }

        [MenuItem("Tools/Folklore Archives/Save Area POIs Layout")]
        public static void SaveAreaPoisLayout() => ManualLayoutPersistence.Save("AreaPois", "AreasAndPOIs", PersistCount);

        [MenuItem("Tools/Folklore Archives/Clear Area POIs Layout")]
        public static void ClearAreaPoisLayout() => ManualLayoutPersistence.Clear("AreaPois");

        // ---------------- ESTEPA + MOLINO ----------------
        static Transform Estepa(Transform parent, Terrain t)
        {
            var g = BuilderUtils.Group(parent, "Estepa", BuilderUtils.Ground(t, MapLayout.EstepaCenter));
            BuilderUtils.Label(g, "ESTEPA", g.position + Vector3.up * 7f);

            // molino australiano oxidado: torre reticulada + cabezal + aspas + veleta
            var mp = BuilderUtils.Ground(t, MapLayout.Molino);
            var mill = BuilderUtils.Group(g, "MolinoOxidado", mp);
            BuilderUtils.Label(mill, "MOLINO", mp + Vector3.up * 10f);
            // molino real descargado (Windmill/) — si no está, molino procedural
            if (SpawnModel(DirWindmill, mill, mp, 8f, 0f, true, "MolinoModelo") == null)
            {
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
            } // fin fallback procedural del molino

            // alambrado (2 tiradas) + huesos de oveja
            Fence(g, t, MapLayout.EstepaCenter + new Vector2(-18, -6), MapLayout.EstepaCenter + new Vector2(20, 2), 6f);
            Fence(g, t, MapLayout.EstepaCenter + new Vector2(8, 18), MapLayout.EstepaCenter + new Vector2(14, -20), 6f);
            SheepBones(g, t, MapLayout.EstepaCenter + new Vector2(6, -4));
            SheepBones(g, t, MapLayout.EstepaCenter + new Vector2(-10, 8));
            return g;
        }

        // ---------------- MALLÍN (pantano) ----------------
        static Transform Mallin(Transform parent, Terrain t)
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
            return g;
        }

        // ---------------- BOSQUE QUEMADO ----------------
        static Transform BurntForestArea(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.BurntForest);
            var g = BuilderUtils.Group(parent, "BosqueQuemado", p);
            BuilderUtils.Label(g, "BOSQUE QUEMADO", p + Vector3.up * 8f);

            // árbol muerto real (DeadTree/) esparcido, o troncos negros procedurales
            var deadSrc = FindModelInFolder(DirDeadTree);
            for (int i = 0; i < 22; i++)
            {
                Vector2 o = Random.insideUnitCircle * 16f;
                Vector3 rp = BuilderUtils.Ground(t, MapLayout.BurntForest.x + o.x, MapLayout.BurntForest.y + o.y);
                float h = Random.Range(3.5f, 6.5f);
                if (deadSrc != null)
                {
                    SpawnModelFrom(deadSrc, g, rp, h, Random.Range(0f, 360f), true, "ArbolMuerto" + i);
                    continue;
                }
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
            return g;
        }

        // ---------------- ORILLA DEL LAGO + MUELLE ----------------
        static Transform LakeShoreDock(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.LakeShore);
            var g = BuilderUtils.Group(parent, "OrillaLago", p);
            BuilderUtils.Label(g, "ORILLA DEL LAGO", p + Vector3.up * 7f);

            // muelle: modelo real (Dock/) apuntando al lago, o tablones procedurales
            Vector2 toLake = (MapLayout.CentralLakeCenter - MapLayout.LakeShore).normalized;
            float deckY = MapLayout.CentralLakeLevel + 0.6f;
            float dockYaw = Mathf.Atan2(toLake.x, toLake.y) * Mathf.Rad2Deg;
            var dockPos = new Vector3(MapLayout.LakeShore.x, deckY, MapLayout.LakeShore.y);
            // muelle más corto (12->5) para la laguna chica -- con el tamaño viejo
            // llegaba casi al centro de una laguna de solo 9m de radio.
            if (SpawnModel(DirDock, g, dockPos, 5f, dockYaw, false, "MuelleModelo") == null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 plankXZ = MapLayout.LakeShore + toLake * (i * 1.2f + 1f);
                    Vector3 pk = new Vector3(plankXZ.x, deckY, plankXZ.y);
                    BuilderUtils.Prim(PrimitiveType.Cube, "Tabla" + i, g, pk, new Vector3(2.0f, 0.12f, 1.6f), Wood,
                        new Vector3(0f, Random.Range(-4f, 4f), 0f));
                    if (i % 2 == 0) // pilotes
                        BuilderUtils.Prim(PrimitiveType.Cylinder, "Pilote" + i, g,
                            new Vector3(plankXZ.x, MapLayout.CentralLakeBed + 1f, plankXZ.y),
                            new Vector3(0.18f, (deckY - MapLayout.CentralLakeBed) * 0.5f + 0.5f, 0.18f), Wood);
                }
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
            return g;
        }

        // ---------------- DIFUNTA CORREA ----------------
        static Transform DifuntaCorrea(Transform parent, Terrain t)
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
            return g;
        }

        // ---------------- GAUCHITO GIL ----------------
        static Transform GauchitoGil(Transform parent, Terrain t)
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
            return g;
        }

        // ---------------- ÁRBOL DEL AHORCADO + CEMENTERIO ----------------
        static Transform HangedTree(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.HangedTree);
            var g = BuilderUtils.Group(parent, "ArbolDelAhorcado", p);
            BuilderUtils.Label(g, "ARBOL DEL AHORCADO", p + Vector3.up * 8f);

            // árbol solitario: modelo real (DeadTree/) o tronco+rama procedural
            Vector3 branch = p + new Vector3(0f, 4.6f, 0f);
            if (SpawnModel(DirDeadTree, g, p, 6.5f, Random.Range(0f, 360f), true, "ArbolAhorcado") == null)
            {
                BuilderUtils.Prim(PrimitiveType.Cylinder, "Tronco", g, p + Vector3.up * 2.6f, new Vector3(0.5f, 2.6f, 0.5f), Wood);
                BuilderUtils.Prim(PrimitiveType.Cylinder, "Rama", g, branch, new Vector3(0.22f, 1.6f, 0.22f), Wood, new Vector3(0f, 0f, 90f));
            }
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
            return g;
        }

        // ---------------- ANTENA / REPETIDORA ----------------
        static Transform Antenna(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.Antenna);
            var g = BuilderUtils.Group(parent, "Antena", p);
            BuilderUtils.Label(g, "ANTENA", p + Vector3.up * 32f);

            float h = 28f;
            // torre real descargada (RadioTower/) — venía acostada; -90 X la para derecha
            // (el auto-stand la ponía de cabeza). Si no está, torre reticulada procedural.
            if (SpawnModel(DirTower, g, p, h, 0f, true, "TorreAntena", new Vector3(-90f, 0f, 0f)) == null)
            {
                for (int i = 0; i < 4; i++)
                {
                    float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
                    Vector3 baseP = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 2.2f;
                    Vector3 topP  = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.4f + Vector3.up * h;
                    Beam(g, baseP, topP, 0.14f, MetalDark);
                }
                for (float y = 3f; y < h; y += 4f)
                    RingRungs(g, p, Mathf.Lerp(2.0f, 0.5f, y / h), y, MetalDark);
            }
            // baliza roja (siempre, arriba de la torre real o procedural)
            var beacon = BuilderUtils.Prim(PrimitiveType.Sphere, "Baliza", g, p + Vector3.up * (h + 0.6f), Vector3.one * 0.6f, RedLight);
            DestroyCol(beacon);
            var l = new GameObject("BalizaLuz").AddComponent<Light>();
            l.transform.SetParent(g); l.transform.position = p + Vector3.up * (h + 0.6f);
            l.type = LightType.Point; l.color = new Color(1f, 0.1f, 0.1f); l.range = 30f; l.intensity = 2.5f; l.shadows = LightShadows.None;
            return g;
        }

        // ---------------- CORRALES / BAÑADERO ----------------
        static Transform Corrales(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.Corrales);
            var g = BuilderUtils.Group(parent, "Corrales", p);
            BuilderUtils.Label(g, "CORRALES", p + Vector3.up * 6f);

            // corral cuadrado de postes + alambre
            Vector2 c = MapLayout.Corrales; float s = 9f;
            var corners = new[] { c + new Vector2(-s, -s), c + new Vector2(s, -s), c + new Vector2(s, s), c + new Vector2(-s, s) };
            var fenceSrc = FindModelInFolder(DirFence);   // cerco de cadena real (ChainFence/) o postes procedurales
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                if (fenceSrc != null) FenceLineModel(fenceSrc, g, t, a, b);
                else Fence(g, t, a, b, 3f);
            }
            // bañadero (pileta larga angosta)
            BuilderUtils.Prim(PrimitiveType.Cube, "Banadero", g, p + Vector3.up * 0.3f, new Vector3(4f, 0.6f, 1.0f), StoneGrey);
            BuilderUtils.Prim(PrimitiveType.Cube, "Agua", g, p + Vector3.up * 0.45f, new Vector3(3.7f, 0.3f, 0.75f), DarkWater);
            SheepBones(g, t, c + new Vector2(3, -2));
            return g;
        }

        // ---------------- ESTACIÓN YPF ----------------
        static Transform YpfStation(Transform parent, Terrain t)
        {
            // Posición y tamaño del lote SIEMPRE derivados de YpfPadNearZ/FarZ (única fuente
            // de verdad, la misma que usan TerrainBuilder/ForestBuilder para despejar/limpiar
            // el lote) — así el modelo, el mesh del piso y la zona despejada SIEMPRE coinciden,
            // y el borde cercano queda BIEN pasado el hombro de asfalto de la ruta (no pisa la ruta).
            float roadZHere = MapLayout.PavedRouteZAt(MapLayout.YpfStation.x);
            float nearZ = roadZHere + MapLayout.YpfPadNearZ, farZ = roadZHere + MapLayout.YpfPadFarZ;
            float centerZ = (nearZ + farZ) * 0.5f;
            var p = BuilderUtils.Ground(t, MapLayout.YpfStation.x, centerZ);   // centro del lote
            var g = BuilderUtils.Group(parent, "EstacionYPF", p);
            BuilderUtils.Label(g, "ESTACION YPF", p + Vector3.up * 8f);

            // PLAYÓN de ASFALTO (mesh plano) — garantiza pavimento plano bajo la estación
            // SIN depender del rebuild del terreno. La estación y el Falcon se apoyan encima.
            var asphalt = BuilderUtils.Mat("ypf_asphalt", new Color(0.55f, 0.55f, 0.57f)); // gris concreto claro (piso de estación)
            if (asphalt.HasProperty("_Smoothness")) asphalt.SetFloat("_Smoothness", 0f);          // mate, no plástico
            if (asphalt.HasProperty("_SpecularHighlights")) asphalt.SetFloat("_SpecularHighlights", 0f);
            // Playón FINO apoyado en la altura del CENTRO del lote (no en los extremos: un
            // lote de 36x32m puede pisar una loma real del terreno en una punta, e intentar
            // "cubrirla" con un bloque grueso termina siendo un cubo gigante flotando). Para
            // que quede perfectamente parejo sin ningún borde asomando hace falta aplanar el
            // TERRENO de verdad una vez: Tools > Rebuild Terrain (forzar) — el código de
            // HeightAt() ya aplana este lote a la altura de la ruta, solo falta ese paso.
            float halfX = MapLayout.YpfPadHalfX - 2f, halfZ = (farZ - nearZ) * 0.5f - 1f;
            float padTop = p.y + 0.12f;
            BuilderUtils.Prim(PrimitiveType.Cube, "PlayonAsfalto", g,
                new Vector3(p.x, padTop - 0.15f, p.z),
                new Vector3(halfX * 2f, 0.3f, halfZ * 2f), asphalt);
            p.y = padTop;   // todo lo de la estación se apoya sobre el playón

            // La estación ENTERA es el modelo descargado: GasStationProps trae TIENDA +
            // TECHO + SURTIDORES + CARTEL, todo junto. Se escala a ~24m (el conjunto es
            // ancho) y mira a la ruta (yaw 180). Si el modelo no está, se arma procedural.
            var st = SpawnModel(DirGasProps, g, p, 24f, 180f, false, "EstacionModelo", new Vector3(-90f, 0f, 0f));
            if (st != null) HideCatalogClutter(st);   // oculta la fila de cajones/productos sueltos del exhibidor
            if (st == null)
            {
                // --- fallback procedural (solo si NO está el modelo) ---
                BuilderUtils.Prim(PrimitiveType.Cube, "Techo", g, p + Vector3.up * 4.2f, new Vector3(9f, 0.4f, 6f), MetalDark);
                BuilderUtils.Prim(PrimitiveType.Cube, "ColA", g, p + new Vector3(-3.5f, 2f, -2f), new Vector3(0.4f, 4f, 0.4f), MetalDark);
                BuilderUtils.Prim(PrimitiveType.Cube, "ColB", g, p + new Vector3(3.5f, 2f, 2f), new Vector3(0.4f, 4f, 0.4f), MetalDark);
                Vector3 tp = p + new Vector3(-8f, 0f, 3.5f);
                BuilderUtils.Prim(PrimitiveType.Cube, "Tienda", g, tp + Vector3.up * 1.5f, new Vector3(5f, 3f, 4f), Rust);
                BuilderUtils.Prim(PrimitiveType.Cube, "TiendaTecho", g, tp + Vector3.up * 3.15f, new Vector3(5.5f, 0.3f, 4.5f), MetalDark);
                BuilderUtils.Prim(PrimitiveType.Cube, "TiendaPuerta", g, tp + new Vector3(1.4f, 1f, 2.02f), new Vector3(1.1f, 2f, 0.1f), MetalDark);
                var tv = BuilderUtils.Prim(PrimitiveType.Cube, "TiendaVidriera", g, tp + new Vector3(-1.2f, 1.6f, 2.02f), new Vector3(2.2f, 1.3f, 0.1f), Bottle);
                DestroyCol(tv);
                for (int i = 0; i < 2; i++)
                {
                    Vector3 sp = p + new Vector3((i - 0.5f) * 3.5f, 0.9f, 0f);
                    BuilderUtils.Prim(PrimitiveType.Cube, "Surtidor" + i, g, sp, new Vector3(0.7f, 1.8f, 0.9f), Rust);
                    BuilderUtils.Prim(PrimitiveType.Cube, "Display" + i, g, sp + new Vector3(0f, 0.4f, 0.48f), new Vector3(0.5f, 0.4f, 0.06f), MetalDark);
                }
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
            return g;
        }

        // ---------------- ESTANCIA + GALPÓN ----------------
        static Transform Estancia(Transform parent, Terrain t)
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
            // galpón real (BarnShed/) o nave procedural
            if (SpawnModel(DirBarn, barn, bp, 13f, 90f, false, "GalponModelo") == null)
            {
                BuilderUtils.Prim(PrimitiveType.Cube, "Paredes", barn, bp + Vector3.up * 2f, new Vector3(9f, 4f, 12f), Rust);
                BuilderUtils.Prim(PrimitiveType.Cube, "TechoA", barn, bp + Vector3.up * 4.4f, new Vector3(6f, 0.2f, 12.5f), MetalDark, new Vector3(0f, 0f, 28f));
                BuilderUtils.Prim(PrimitiveType.Cube, "TechoB", barn, bp + Vector3.up * 4.4f, new Vector3(6f, 0.2f, 12.5f), MetalDark, new Vector3(0f, 0f, -28f));
                BuilderUtils.Prim(PrimitiveType.Cube, "Porton", barn, bp + new Vector3(0f, 1.8f, 6.1f), new Vector3(3.5f, 3.6f, 0.2f), MetalDark);
            }
            // "cadenas" colgando (marcador de El Familiar) — siempre
            for (int i = 0; i < 3; i++)
                BuilderUtils.Prim(PrimitiveType.Cylinder, "Cadena" + i, barn, bp + new Vector3(-1f + i, 2.6f, 6.3f), new Vector3(0.05f, 0.7f, 0.05f), MetalDark);
            return g;
        }

        // ---------------- CAPILLA ANEGADA (modelo descargado) ----------------
        static Transform Capilla(Transform parent, Terrain t)
        {
            Vector2 xz = MapLayout.Capilla;
            float groundY = t.SampleHeight(new Vector3(xz.x, 0f, xz.y));
            var g = BuilderUtils.Group(parent, "CapillaAnegada", new Vector3(xz.x, groundY, xz.y));
            BuilderUtils.Label(g, "CAPILLA ANEGADA", new Vector3(xz.x, groundY + 9f, xz.y));

            // medio HUNDIDA: apoyo el fondo ~2.5m bajo el nivel del suelo del río → el
            // campanario/techo asoma del agua.
            var sunk = new Vector3(xz.x, groundY - 2.5f, xz.y);
            if (SpawnModel(DirChurch, g, sunk, 11f, Random.Range(0f, 360f), false, "CapillaModelo") == null)
            {
                // placeholder procedural: nave + campanario + cruz asomando
                BuilderUtils.Prim(PrimitiveType.Cube, "Nave", g, new Vector3(xz.x, groundY + 0.5f, xz.y), new Vector3(6f, 4f, 9f), StoneGrey);
                BuilderUtils.Prim(PrimitiveType.Cube, "Campanario", g, new Vector3(xz.x, groundY + 3f, xz.y - 4f), new Vector3(2.2f, 5f, 2.2f), StoneGrey);
                BuilderUtils.Prim(PrimitiveType.Cube, "CruzV", g, new Vector3(xz.x, groundY + 6.5f, xz.y - 4f), new Vector3(0.2f, 1.3f, 0.2f), Wood);
                BuilderUtils.Prim(PrimitiveType.Cube, "CruzH", g, new Vector3(xz.x, groundY + 6.2f, xz.y - 4f), new Vector3(0.8f, 0.2f, 0.2f), Wood);
            }
            return g;
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

        // tilea el modelo de cerco entre a y b (repite el segmento a lo largo de la línea).
        static void FenceLineModel(GameObject src, Transform parent, Terrain t, Vector2 a, Vector2 b)
        {
            // largo nativo del segmento (lado mayor XZ)
            var probe = (GameObject)Object.Instantiate(src);
            probe.transform.localScale = Vector3.one;
            var pb = ModelBounds(probe);
            float segLen = Mathf.Max(pb.size.x, pb.size.z);
            Object.DestroyImmediate(probe);
            if (segLen < 0.3f) segLen = 2f;

            float total = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(total / segLen));
            float yaw = Mathf.Atan2((b - a).x, (b - a).y) * Mathf.Rad2Deg;
            for (int i = 0; i < n; i++)
            {
                Vector2 xz = Vector2.Lerp(a, b, (i + 0.5f) / n);
                Vector3 gp = BuilderUtils.Ground(t, xz.x, xz.y);
                SpawnModelFrom(src, parent, gp, segLen, yaw, false, "Cerco" + i); // escala ~nativa
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

        // Productos/cajones del exhibidor del modelo de la YPF que quedan "tirados" afuera.
        // Se ocultan por nombre para dejar la estación limpia (estructura + surtidores +
        // cartel quedan). Si oculta de más/menos, se ajusta la lista.
        // Por pedido del owner: dejar TODO el catálogo (cajones, góndolas, heladera suelta,
        // etc.) visible AFUERA como venía del modelo — lo reacomoda él a mano después.
        // Solo se oculta el piso propio del modelo bajo el techo ("Sidewalk"/"Sidewalk_01"),
        // que duplicaba nuestro playón gris.
        static readonly System.Collections.Generic.HashSet<string> YpfClutter = new System.Collections.Generic.HashSet<string>(
            new[] { "Sidewalk", "Sidewalk_01" }, System.StringComparer.OrdinalIgnoreCase);
        static void HideCatalogClutter(GameObject inst)
        {
            int hid = 0;
            foreach (var tr in inst.GetComponentsInChildren<Transform>(true))
            {
                if (YpfClutter.Contains(tr.name)) { tr.gameObject.SetActive(false); hid++; }
            }
            Debug.Log($"<color=cyan>[YPF] {hid} objetos sueltos/piso propio ocultados del modelo.</color>");
        }

        // ---- carga de MODELOS DESCARGADOS ----
        // busca el primer modelo (GameObject) dentro de una carpeta; null si no está.
        static GameObject FindModelInFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
            foreach (var gu in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(gu);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) return go;
            }
            return null;
        }

        // bounds (world) combinados de todos los renderers de una instancia.
        static Bounds ModelBounds(GameObject inst)
        {
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(inst.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        // instancia el modelo de `folder`, lo escala para que su lado mayor (XZ) o su ALTO
        // sea ~targetSize, lo APOYA en el piso en `pos` (con yaw). Devuelve null si el
        // modelo todavía no está descargado (→ el caller arma la versión procedural).
        static GameObject SpawnModel(string folder, Transform parent, Vector3 pos, float targetSize, float yaw, bool byHeight = false, string name = null, Vector3? tilt = null)
        {
            var src = FindModelInFolder(folder);
            if (src == null) return null;
            return SpawnModelFrom(src, parent, pos, targetSize, yaw, byHeight, name, tilt);
        }

        // igual que SpawnModel pero con el modelo ya encontrado (para instanciar en loop
        // sin re-buscar en la carpeta cada vez). `tilt` = rotación previa para parar modelos
        // que vienen acostados (ej. torres exportadas con eje Z arriba).
        static GameObject SpawnModelFrom(GameObject src, Transform parent, Vector3 pos, float targetSize, float yaw, bool byHeight = false, string name = null, Vector3? tilt = null)
        {
            var inst = (GameObject)Object.Instantiate(src, parent);
            if (name != null) inst.name = name;
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * (tilt.HasValue ? Quaternion.Euler(tilt.Value) : Quaternion.identity);
            inst.transform.localScale = Vector3.one;
            var b = ModelBounds(inst);
            // PARAR modelos que vinieron ACOSTADOS: si se pide por ALTO pero el eje
            // vertical (Y) no es el más largo, roto para que el eje más largo quede vertical
            // (ej. torres exportadas de costado). Solo si no se dio un tilt manual.
            if (byHeight && !tilt.HasValue)
            {
                if (b.size.z > b.size.y * 1.3f && b.size.z >= b.size.x)
                { inst.transform.rotation = Quaternion.Euler(90f, 0f, 0f) * inst.transform.rotation; b = ModelBounds(inst); }
                else if (b.size.x > b.size.y * 1.3f && b.size.x >= b.size.z)
                { inst.transform.rotation = Quaternion.Euler(0f, 0f, 90f) * inst.transform.rotation; b = ModelBounds(inst); }
            }
            float dim = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            if (dim > 0.001f)
            {
                inst.transform.localScale = Vector3.one * (targetSize / dim);
                b = ModelBounds(inst);
            }
            inst.transform.position += new Vector3(0f, pos.y - b.min.y, 0f); // apoyar el fondo en el piso
            return inst;
        }
    }
}
