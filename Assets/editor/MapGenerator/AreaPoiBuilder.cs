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
using System.Collections.Generic;
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
        // "broken_wooden_dock_ps1.glb" DESCARTADO (owner, después de 3 rondas de bugs
        // esta sesión -- la última: una tabla suelta adentro del propio modelo
        // apuntando derecho al cielo, "Cube_Material.002" a -38° en X, glitch del
        // asset en sí no de nuestro código). Reemplazado por "The Wharf" de Sketchfab
        // (CC Attribution, Mehdi Shahsavan, 572 tris) -- malla ÚNICA ("Cloner_2" en el
        // FBX, sin piezas sueltas con transform propio), mucho más simple/confiable.
        const string DirDock     = "Assets/ExternalAssets/DockWharf";
        const string DirDockTex  = "Assets/ExternalAssets/DockWharf/textures/01_DefaultMaterial_BaseColor.png";
        // rancho del pescador real (owner: "me gusta esta", Sketchfab "PSX Abandoned
        // House", CC-BY) -- reemplaza las primitivas procedurales de antes.
        const string DirHouseAbandoned = "Assets/ExternalAssets/AbandonedHouse";
        // torre/mirador de caza (owner: "Campo de Caza", referencia Sketchfab "Watch
        // tower (remastered, wide)").
        const string DirHuntingTower = "Assets/ExternalAssets/HuntingTower";
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
            Reg(HuntingFieldArea(root, t));

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
            // DESACTIVADO (owner: "saca ese bosque quemado", viendo la laguna con los
            // pinos de fondo nuevos -- los troncos negros quedaban justo detrás del
            // agua, tapando el bosque). Grupo vacío (mismo patrón que Estancia) para
            // no tocar el conteo de Reg()/PersistCount. El área queda libre para que
            // ForestBuilder la llene de árboles normales (ver el "quemado: solo
            // troncos negros" en ForestBuilder, también sacado).
            return BuilderUtils.Group(parent, "BosqueQuemado", Vector3.zero);
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
            var dockInst = SpawnModel(DirDock, g, dockPos, 5f, dockYaw, false, "MuelleModelo");
            if (dockInst != null) FixDockMaterial(dockInst);
            else
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

            // ---- rancho de pescador abandonado + bote + redes (owner: referencia con
            // laguna+muelle+casita chica, "que decis? que le agregarias?" -> bote a
            // remo + redes de pesca) -- todo alrededor del muelle, del lado de tierra.
            // OJO: BuilderUtils.Prim fija pos/euler en espacio MUNDO (no local al padre),
            // igual que las "Tabla"/"Pilote" del muelle de más arriba -- por eso acá
            // TODO se arma sumando toLake/perpToLake directo (no rotando el Transform
            // del grupo después, que no tendría ningún efecto visual sobre los hijos).
            Vector2 perpToLake = new Vector2(-toLake.y, toLake.x);

            // rancho: una sola pieza, chico, a un costado del muelle y un poco tierra
            // adentro (no tapa el agua ni el muelle). Modelo real (AbandonedHouse/,
            // PSX, Sketchfab) si está descargado, si no la versión procedural de antes.
            Vector2 shackXZ = MapLayout.LakeShore + perpToLake * 5f - toLake * 2f;
            Vector3 shackP = BuilderUtils.Ground(t, shackXZ.x, shackXZ.y);
            var shackInst = SpawnModel(DirHouseAbandoned, g, shackP, 4.5f, dockYaw, false, "RanchoPescador");
            if (shackInst != null)
            {
                FixHouseMaterial(shackInst);
                BuilderUtils.Label(shackInst.transform, "RANCHO DEL PESCADOR", shackP + Vector3.up * 4.5f);
            }
            else
            {
                var shack = BuilderUtils.Group(g, "RanchoPescador", shackP);
                BuilderUtils.Label(shack, "RANCHO DEL PESCADOR", shackP + Vector3.up * 4.5f);
                Vector3 shackFwd = new Vector3(toLake.x, 0f, toLake.y);
                Vector3 shackRight = new Vector3(perpToLake.x, 0f, perpToLake.y);
                BuilderUtils.Prim(PrimitiveType.Cube, "Paredes", shack, shackP + Vector3.up * 1.1f,
                    new Vector3(2.4f, 2.2f, 2.1f), Wood, new Vector3(0f, dockYaw, 0f));
                BuilderUtils.Prim(PrimitiveType.Cube, "TechoA", shack, shackP + Vector3.up * 2.35f,
                    new Vector3(1.5f, 0.15f, 2.4f), MetalDark, new Vector3(0f, dockYaw, 22f));
                BuilderUtils.Prim(PrimitiveType.Cube, "TechoB", shack, shackP + Vector3.up * 2.35f,
                    new Vector3(1.5f, 0.15f, 2.4f), MetalDark, new Vector3(0f, dockYaw, -22f));
                // chimenea torcida (abandonado, no sale humo) -- esquina del techo, en la
                // base (fwd,right) del rancho para que quede pegada al techo sea cual sea dockYaw.
                Vector3 chimP = shackP + shackRight * 0.7f + shackFwd * 0.6f + Vector3.up * 2.7f;
                BuilderUtils.Prim(PrimitiveType.Cube, "Chimenea", shack, chimP,
                    new Vector3(0.28f, 0.9f, 0.28f), StoneGrey, new Vector3(6f, dockYaw + 4f, 0f));
                // puerta entornada (más oscura, sin marco -- nivel de detalle del resto del archivo)
                Vector3 doorP = shackP + shackRight * 0.5f + shackFwd * 1.06f + Vector3.up * 0.75f;
                BuilderUtils.Prim(PrimitiveType.Cube, "Puerta", shack, doorP,
                    new Vector3(0.7f, 1.5f, 0.06f), MetalDark, new Vector3(0f, dockYaw + 18f, 0f));
            }

            // bote a remo volcado/varado en la orilla, cerca del muelle
            Vector2 boatXZ = MapLayout.LakeShore + perpToLake * 2.5f + toLake * 1f;
            Vector3 boatP = BuilderUtils.Ground(t, boatXZ.x, boatXZ.y);
            var boat = BuilderUtils.Group(g, "BoteVarado", boatP);
            float boatYaw = dockYaw + 25f; // no mirando derecho al muelle, un poco de costado
            float boatYawRad = boatYaw * Mathf.Deg2Rad;
            Vector3 boatFwd = new Vector3(Mathf.Sin(boatYawRad), 0f, Mathf.Cos(boatYawRad));
            Vector3 boatRight = new Vector3(boatFwd.z, 0f, -boatFwd.x);
            BuilderUtils.Prim(PrimitiveType.Cube, "Casco", boat, boatP + Vector3.up * 0.28f,
                new Vector3(0.8f, 0.35f, 2.3f), Wood, new Vector3(8f, boatYaw, 0f)); // apenas volcado de costado
            BuilderUtils.Prim(PrimitiveType.Cube, "Proa", boat, boatP + boatFwd * 1.15f + Vector3.up * 0.3f,
                new Vector3(0.55f, 0.32f, 0.5f), Wood, new Vector3(0f, boatYaw + 45f, 0f)); // punta angosta
            for (int i = 0; i < 2; i++)
            {
                Vector3 oarP = boatP + boatRight * (-0.3f + i * 0.6f) + boatFwd * (-0.4f + i * 0.3f) + Vector3.up * 0.42f;
                BuilderUtils.Prim(PrimitiveType.Cylinder, "Remo" + i, boat, oarP,
                    new Vector3(0.05f, 1.1f, 0.05f), Wood, new Vector3(78f, boatYaw + 20f + i * 30f, 0f));
            }

            // redes de pesca: pila enredada en el piso al lado del rancho
            Vector2 netXZ = MapLayout.LakeShore + perpToLake * 6.5f - toLake * 3.5f;
            Vector3 netP = BuilderUtils.Ground(t, netXZ.x, netXZ.y);
            var nets = BuilderUtils.Group(g, "RedesDePesca", netP);
            for (int i = 0; i < 10; i++)
            {
                Vector2 o = Random.insideUnitCircle * 0.9f;
                var strand = BuilderUtils.Prim(PrimitiveType.Cylinder, "Hebra" + i, nets,
                    netP + new Vector3(o.x, 0.05f + i * 0.01f, o.y), new Vector3(0.025f, 0.9f, 0.025f), Rope,
                    new Vector3(85f + Random.Range(-6f, 6f), Random.Range(0f, 360f), 0f));
                DestroyCol(strand);
            }
            // un par de flotadores (boyas) arriba de la pila
            for (int i = 0; i < 3; i++)
            {
                var buoy = BuilderUtils.Prim(PrimitiveType.Sphere, "Boya" + i, nets,
                    netP + new Vector3(Random.Range(-0.6f, 0.6f), 0.22f, Random.Range(-0.6f, 0.6f)),
                    Vector3.one * 0.22f, FlagRed);
                DestroyCol(buoy);
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
            // ESTANCIA DESACTIVADA (decisión del owner). El galpón real ahora va en la
            // casa de la vieja (granja), horneado en HouseBuilder.BuildBarn. Antes esto
            // construía el "casco" (House.fbx → salía MAGENTA por shader built-in) + el
            // GalponModelo, que además duplicaba el galpón. Dejo el grupo VACÍO y
            // registrado para NO correr los índices de persistencia de los demás POIs.
            var p = BuilderUtils.Ground(t, MapLayout.Estancia);
            return BuilderUtils.Group(parent, "Estancia", p);
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

        // ---------------- CAMPO DE CAZA (torre/mirador) ----------------
        // owner: "que deberia haber... torre como hito visual + 1-2 detalles
        // inquietantes cerca" -- por ahora solo la torre (el owner todavía no eligió
        // los detalles). Campo ABIERTO: no llenar de objetos, solo el hito central.
        static Transform HuntingFieldArea(Transform parent, Terrain t)
        {
            var p = BuilderUtils.Ground(t, MapLayout.HuntingField);
            var g = BuilderUtils.Group(parent, "CampoDeCaza", p);
            BuilderUtils.Label(g, "CAMPO DE CAZA", p + Vector3.up * 9f);

            // torre real (HuntingTower/, Sketchfab "Watch tower remastered wide") o,
            // si todavía no está descargada, una torre procedural simple (mismo
            // criterio que el molino de la Estepa: 4 patas + cruces + plataforma).
            var towerInst = SpawnModel(DirHuntingTower, g, p, 7f, Random.Range(0f, 360f), true, "TorreDeCaza");
            if (towerInst != null) FixTowerMaterial(towerInst);
            else
            {
                float towerH = 6f;
                for (int i = 0; i < 4; i++)
                {
                    float ang = i * 90f * Mathf.Deg2Rad + 45f * Mathf.Deg2Rad;
                    Vector3 baseP = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.8f;
                    Vector3 topP  = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.4f + Vector3.up * towerH;
                    Beam(g, baseP, topP, 0.10f, Wood);
                }
                RingRungs(g, p, 1.6f, 2.2f, Wood);
                BuilderUtils.Prim(PrimitiveType.Cube, "Plataforma", g, p + Vector3.up * (towerH + 0.15f),
                    new Vector3(2.4f, 0.15f, 2.4f), Wood);
                BuilderUtils.Prim(PrimitiveType.Cube, "Baranda1", g, p + Vector3.up * (towerH + 0.9f) + new Vector3(1.2f, 0f, 0f),
                    new Vector3(0.08f, 0.8f, 2.4f), Wood);
                BuilderUtils.Prim(PrimitiveType.Cube, "Baranda2", g, p + Vector3.up * (towerH + 0.9f) + new Vector3(-1.2f, 0f, 0f),
                    new Vector3(0.08f, 0.8f, 2.4f), Wood);
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

        // El rancho de Sketchfab (Blender, malla única "House" con 6 slots de
        // material: Wood/Stone/Brick/Roof/Wood2/DarkWood) -- Standard/PBR, no URP.
        // Empareja por NOMBRE de material (no por índice de slot -- más robusto,
        // no depende del orden en que Unity importó los slots). IMPORTANTE: los
        // nombres más largos van ANTES en la lista ("DarkWood"/"Wood2" antes que
        // "Wood") porque el match es por substring -- si no, "DarkWood" matchearía
        // "Wood" primero y quedaría con la textura equivocada.
        static readonly string[] HouseMatNames = { "DarkWood", "Wood2", "Wood", "Stone", "Brick", "Roof" };
        static Dictionary<string, Material> _houseMats;
        static void FixHouseMaterial(GameObject inst)
        {
            if (_houseMats == null)
            {
                _houseMats = new Dictionary<string, Material>();
                foreach (var n in HouseMatNames)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirHouseAbandoned + "/textures/" + n + ".jpg");
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                    string matPath = "Assets/Settings/House_" + n + ".mat";
                    AssetDatabase.DeleteAsset(matPath);
                    AssetDatabase.CreateAsset(mat, matPath);
                    _houseMats[n] = mat;
                }
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var src = r.sharedMaterials;
                var outM = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string baseName = src[i] != null ? src[i].name : "";
                    Material match = null;
                    foreach (var n in HouseMatNames)
                        if (baseName.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0) { match = _houseMats[n]; break; }
                    outM[i] = match != null ? match : src[i];
                }
                r.sharedMaterials = outM;
            }
        }

        // La torre de caza (Sketchfab, malla única) -- mismo criterio simple que el
        // wharf: solo BaseColor, sin mapear normal/AO.
        static Material _towerMat;
        static void FixTowerMaterial(GameObject inst)
        {
            if (_towerMat == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirHuntingTower + "/textures/Watch_tower_Base_color.png");
                _towerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null && _towerMat.HasProperty("_BaseMap")) _towerMat.SetTexture("_BaseMap", tex);
                if (_towerMat.HasProperty("_Smoothness")) _towerMat.SetFloat("_Smoothness", 0.1f);
                string matPath = "Assets/Settings/HuntingTower.mat";
                AssetDatabase.DeleteAsset(matPath);
                AssetDatabase.CreateAsset(_towerMat, matPath);
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = _towerMat;
                r.sharedMaterials = arr;
            }
        }

        // El wharf de Sketchfab (Cinema 4D, malla única) trae un material
        // Standard/PBR -- sin pasarlo a URP se ve magenta. Solo el BaseColor (mismo
        // criterio simple que la valla de madera: sin mapear roughness/normal).
        static Material _dockMat;
        static void FixDockMaterial(GameObject inst)
        {
            if (_dockMat == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirDockTex);
                _dockMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null && _dockMat.HasProperty("_BaseMap")) _dockMat.SetTexture("_BaseMap", tex);
                if (_dockMat.HasProperty("_Smoothness")) _dockMat.SetFloat("_Smoothness", 0.15f);
                string matPath = "Assets/Settings/DockWharf.mat";
                AssetDatabase.DeleteAsset(matPath);
                AssetDatabase.CreateAsset(_dockMat, matPath);
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = _dockMat;
                r.sharedMaterials = arr;
            }
        }

        // ---- carga de MODELOS DESCARGADOS ----
        // busca el primer modelo (GameObject) dentro de una carpeta; null si no está.
        static GameObject FindModelInFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            // ANTES filtraba "t:GameObject" -- el muelle (Dock/broken_wooden_dock_ps1.glb)
            // se importa bien (mismo importer glTFast que otros .glb del proyecto que sí
            // andan, sin errores en el reporte) pero igual no aparecía con ese filtro, así
            // que el código caía siempre al fallback procedural (los 8 tablones que dan el
            // efecto "abanico" al rotarlos). Sin filtro de tipo: traigo TODOS los assets de
            // la carpeta y pruebo cargar cada uno como GameObject -- más lento pero no
            // depende de cómo el índice de búsqueda de Unity clasifique cada importer.
            var guids = AssetDatabase.FindAssets("", new[] { folder });
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
