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
        // torre/mirador de caza (Sketchfab "Watch tower remastered wide") -- ya no es
        // parte del Campo de Caza (reemplazado por el Cementerio), reubicada como
        // hito aparte pasando el puente (owner: "quitaste el mirador que puse
        // volvelo a poner pasando el puente").
        const string DirHuntingTower = "Assets/ExternalAssets/HuntingTower";
        // Cerco de alambre modular (PSX Modular Chain-Link Fence, DanglingBat, itch.io). GLB
        // convertidos a FBX (Blender). Recto = 2×2 m, pivote en la BASE-CENTRO. Materiales:
        // "chain_link" (malla con alfa recortado) + "galvanized_steel" (postes/marco, opaco).
        const string DirChainLinkFence = "Assets/ExternalAssets/ChainLinkFence/Models";
        // CEMENTERIO (owner: "quiero un solo cementerio" -- reemplaza lo que iba a ser
        // el Campo de Caza, mismo punto/trigger de Acto2 sin renombrar por abajo).
        // Referencia: "Stylized Graveyard Model Guide" (Sketchfab) -- reja + capillita +
        // sendero + lápidas dispersas + árboles pelados. Replicado a nivel de
        // COMPOSICIÓN (no una copia 1:1, no tenemos esos assets puntuales), con lo que
        // el owner ya bajó + lo que ya había en el proyecto (DeadTree, ChainFence).
        const string DirTombstone1    = "Assets/ExternalAssets/Cemetery/Tombstone1"; // owner: CC0-Tombstone (Sketchfab)
        const string DirCemeteryFence = "Assets/ExternalAssets/Cemetery/Fence";      // reja de hierro -- todavía no bajada
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
        public const int PersistCount = 14;
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
            // Cementerio/Mirador envueltos en try-catch (owner: "Mirador no existe para
            // nada" sin ningún error visible en Console -- si una excepción silenciosa
            // en Cementerio cortaba acá, el Mirador ni siquiera llegaba a intentarse.
            // Así, si alguno tira una excepción real, queda un error CLARO en la
            // Console con su nombre en vez de un "no aparece" mudo, y el otro POI
            // igual se construye).
            try { Reg(CemeteryArea(root, t)); }
            catch (System.Exception ex) { Debug.LogError("[AreaPoiBuilder] CemeteryArea explotó: " + ex); }
            try { Reg(BridgeLookout(root, t)); }
            catch (System.Exception ex) { Debug.LogError("[AreaPoiBuilder] BridgeLookout explotó: " + ex); }

            // set-dressing fijo → static batching (menos draw calls). Excepto luces.
            BuilderUtils.MarkStaticRecursive(root);
        }

        // (Los menús "Save/Clear Area POIs Layout" se eliminaron: mover/guardar POIs ahora
        //  es parte de "Save Map Layout", unificado con el resto del mapa. El registro
        //  interno (Begin/Reg) sigue para colocar los POIs; Save Map Layout lo pisa al final.)

        // ---------------- ESTEPA + MOLINO ----------------
        static Transform Estepa(Transform parent, Terrain t)
        {
            // ESTEPA DESACTIVADA (decisión del owner): se sacó el molino oxidado, el
            // alambrado (postes/vigas) y los huesos de oveja. Dejo el grupo VACÍO y
            // registrado para NO correr los índices de persistencia de los demás POIs.
            return BuilderUtils.Group(parent, "Estepa", BuilderUtils.Ground(t, MapLayout.EstepaCenter));
        }

        // ---------------- MALLÍN (pantano) ----------------
        static Transform Mallin(Transform parent, Terrain t)
        {
            // DESACTIVADO (owner: "quita el mallin"). Grupo vacío (mismo patrón que
            // Estancia/BurntForestArea) para no tocar el conteo de Reg()/PersistCount.
            // El área queda libre para que ForestBuilder/TerrainBuilder la llenen de
            // bosque y pasto normal (ver los "pantano (mallín)" en esos archivos,
            // también sacados).
            return BuilderUtils.Group(parent, "Mallin", Vector3.zero);
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
            // Piso de CEMENTO: textura de concreto gris liso (512px, seamless) tileada sobre
            // el playón, con un leve tinte claro. Si falta la textura, queda el gris plano de
            // antes. (Cambio horneado en el builder — se re-aplica en cada Generate.)
            var asphalt = BuilderUtils.Mat("ypf_asphalt", new Color(0.72f, 0.72f, 0.73f)); // tinte cemento claro
            var cementTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/AbandonedFarm/Textures/concrete.png");
            if (cementTex != null)
            {
                asphalt.mainTexture = cementTex;
                if (asphalt.HasProperty("_BaseMap")) asphalt.SetTexture("_BaseMap", cementTex);
                var tiling = new Vector2(MapLayout.YpfPadHalfX * 2f / 6f, (farZ - nearZ) / 6f); // ~1 repetición cada 6m
                asphalt.mainTextureScale = tiling;
                if (asphalt.HasProperty("_BaseMap")) asphalt.SetTextureScale("_BaseMap", tiling);
            }
            if (asphalt.HasProperty("_Smoothness")) asphalt.SetFloat("_Smoothness", 0f);          // mate, no plástico
            if (asphalt.HasProperty("_SpecularHighlights")) asphalt.SetFloat("_SpecularHighlights", 0f);
            // Playón FINO apoyado en la altura del CENTRO del lote (no en los extremos: un
            // lote de 36x32m puede pisar una loma real del terreno en una punta, e intentar
            // "cubrirla" con un bloque grueso termina siendo un cubo gigante flotando). Para
            // que quede perfectamente parejo sin ningún borde asomando hace falta aplanar el
            // TERRENO de verdad una vez: Tools > Rebuild Terrain (forzar) — el código de
            // HeightAt() ya aplana este lote a la altura de la ruta, solo falta ese paso.
            float halfX = MapLayout.YpfPadHalfX - 2f, halfZ = (farZ - nearZ) * 0.5f - 1f;
            // owner: "añadile colisión al piso de cemento". El playón AHORA es sólido (se
            // conserva el BoxCollider que trae CreatePrimitive) para que el jugador se pare
            // ENCIMA del cemento y no se hunda al terreno de abajo.
            //
            // Historia del auto trabado: antes este cubo asomaba +0.12m sobre el terreno →
            // su BoxCollider formaba un cordón de ~12cm justo en la entrada y frenaba al
            // auto. Ahora bajamos el playón CASI AL RAS del terreno (borde de solo ~0.02m):
            // sigue tapando el suelo y dando piso sólido, pero el escalón es tan bajo que el
            // auto lo cruza sin trabarse. El terreno de abajo ya está aplanado a la altura de
            // la ruta (HeightAt()), así que el cemento queda parejo.
            float padTop = p.y + 0.02f;
            var playon = BuilderUtils.Prim(PrimitiveType.Cube, "PlayonAsfalto", g,
                new Vector3(p.x, padTop - 0.15f, p.z),
                new Vector3(halfX * 2f, 0.3f, halfZ * 2f), asphalt);
            // (collider de caja conservado a propósito → piso de cemento sólido)
            ParkingLines(g, p, padTop);   // líneas de estacionamiento (blancas + amarillas) — el owner las reacomoda
            p.y = padTop;   // todo lo de la estación se apoya sobre el playón

            // La estación ENTERA es el modelo descargado: GasStationProps trae TIENDA +
            // TECHO + SURTIDORES + CARTEL, todo junto. Se escala a ~24m (el conjunto es
            // ancho) y mira a la ruta (yaw 180). Si el modelo no está, se arma procedural.
            var st = SpawnModel(DirGasProps, g, p, 24f, 180f, false, "EstacionModelo", new Vector3(-90f, 0f, 0f));
            if (st != null)
            {
                HideCatalogClutter(st);   // oculta la fila de cajones/productos sueltos del exhibidor
                AddMeshColliders(st, "estación YPF");     // owner: "colisiones a toda la estación" (tienda/techo/columnas/surtidores)
                StyleYpfStation(st);      // re-brand: logo "6twelve" → YPF, banda de colores → azul navy YPF
            }
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

            // owner: "poné vallas de alambre alrededor de la YPF". Cerco modular (asset PSX
            // chain-link de DanglingBat) en 3 lados: NORTE, OESTE y SUR. El lado ESTE queda
            // ABIERTO a propósito = entrada: el auto de la secuencia ingresa desde el sureste
            // (CarBuilder) cruzando ese lado; si lo cerráramos, chocaría el cerco.
            FenceYpf(g, p, MapLayout.YpfPadHalfX - 1f, (farZ - nearZ) * 0.5f, t);

            // owner: "añadí el PC noventoso y la silla, dejalos cerca de la YPF que yo los acomodo".
            PlaceYpfComputer(g, p, t);
            return g;
        }

        // ---------------- LÍNEAS DE ESTACIONAMIENTO (YPF) ----------------
        // owner: "añadí líneas blancas o amarillas de estacionamiento, yo después las
        // posiciono". Se hornean sobre el playón (cara superior = padTop), como cubos
        // finitos apenas levantados (padTop + 0.03) para no pelear el z-fighting con el
        // asfalto. Cada línea es un objeto suelto bajo el grupo "LineasEstacionamiento"
        // (hijo de EstacionYPF): mové el grupo entero para trasladar todo, o cada línea
        // por separado para acomodar las bahías. Sin colisión (puramente visual). Como se
        // hornea en el builder, se re-crea igual en cada Generate; si querés fijar posiciones
        // que moviste a mano, después de acomodarlas usá "Save Map Layout".
        static void ParkingLines(Transform stationGroup, Vector3 padCenter, float padTop)
        {
            var group = BuilderUtils.Group(stationGroup, "LineasEstacionamiento",
                new Vector3(padCenter.x, padTop, padCenter.z));

            var white = BuilderUtils.Mat("ypf_line_white", new Color(0.90f, 0.90f, 0.87f));
            if (white.HasProperty("_Smoothness")) white.SetFloat("_Smoothness", 0f);
            if (white.HasProperty("_SpecularHighlights")) white.SetFloat("_SpecularHighlights", 0f);
            var yellow = BuilderUtils.Mat("ypf_line_yellow", new Color(0.93f, 0.78f, 0.10f));
            if (yellow.HasProperty("_Smoothness")) yellow.SetFloat("_Smoothness", 0f);
            if (yellow.HasProperty("_SpecularHighlights")) yellow.SetFloat("_SpecularHighlights", 0f);

            const float lineW = 0.15f;   // ancho de la raya
            const float lineH = 0.05f;   // espesor (bien finito, apenas sobresale)
            const float bayDepth = 5f;   // largo de la bahía (profundidad del auto)
            const float bayWidth = 2.7f; // ancho de cada lugar
            const int   dividers = 6;    // 6 rayas → 5 lugares
            float y = padTop + 0.03f;    // apenas por encima del asfalto

            float bankWidth = (dividers - 1) * bayWidth;
            float x0 = padCenter.x - bankWidth * 0.5f;
            float zAnchor = padCenter.z - 6f; // banco corrido hacia la ruta (lo reacomodás)

            // BLANCAS: divisorias de bahía (corren en Z, la profundidad del auto)
            for (int i = 0; i < dividers; i++)
            {
                float x = x0 + i * bayWidth;
                var l = BuilderUtils.Prim(PrimitiveType.Cube, "LineaBahia_" + i, group,
                    new Vector3(x, y, zAnchor), new Vector3(lineW, lineH, bayDepth), white);
                DestroyCol(l);
            }
            // BLANCA: línea de tope al fondo de las bahías (corre en X)
            var stop = BuilderUtils.Prim(PrimitiveType.Cube, "LineaTope", group,
                new Vector3(padCenter.x, y, zAnchor + bayDepth * 0.5f),
                new Vector3(bankWidth + lineW, lineH, lineW), white);
            DestroyCol(stop);

            // AMARILLAS de ejemplo (cordón/no estacionar) — el owner elige si las usa
            var yA = BuilderUtils.Prim(PrimitiveType.Cube, "LineaAmarilla_0", group,
                new Vector3(x0 - 1.5f, y, zAnchor), new Vector3(lineW, lineH, bayDepth + 2f), yellow);
            DestroyCol(yA);
            var yB = BuilderUtils.Prim(PrimitiveType.Cube, "LineaAmarilla_1", group,
                new Vector3(x0 - 1.8f, y, zAnchor), new Vector3(lineW, lineH, bayDepth + 2f), yellow);
            DestroyCol(yB);
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

        // ---------------- CAPILLA ANEGADA (ELIMINADA) ----------------
        // owner: sacar la Capilla Anegada. No alcanzaba con ocultarla + Save Map Layout
        // porque el prefijo "ML_###" que le pone ManualLayoutPersistence.Register se CORRE
        // entre generates (depende del orden de los Reg), así que la marca de "borrado"
        // quedaba atada a un nombre que ya no coincidía → volvía a aparecer. Se VACÍA a un
        // grupo vacío (mismo criterio que Estepa/Estancia) para NO tocar el conteo de
        // Reg()/PersistCount y no correr los POIs que vienen después (Cementerio, Lookout).
        static Transform Capilla(Transform parent, Terrain t)
        {
            Vector2 xz = MapLayout.Capilla;
            float groundY = t.SampleHeight(new Vector3(xz.x, 0f, xz.y));
            return BuilderUtils.Group(parent, "CapillaAnegada", new Vector3(xz.x, groundY, xz.y));
        }

        // ---------------- CEMENTERIO ----------------
        // owner: "quiero un solo cementerio" (consolida lo que iba a ser el Campo de
        // Caza en el mismo lugar -- sigue usando MapLayout.HuntingField/su trigger de
        // Acto2 por abajo, sin renombrar, mismo criterio que LakeMountain). Referencia:
        // "Stylized Graveyard Model Guide" (Sketchfab) -- reja + capillita + lápidas
        // dispersas + árboles pelados. Réplica a nivel de COMPOSICIÓN (no una copia
        // 1:1, esos assets puntuales no los tenemos) con lo ya descargado
        // (Tombstone1) + lo que ya había en el proyecto (DeadTree, ChainFence).
        static Transform CemeteryArea(Transform parent, Terrain t)
        {
            Vector2 c = MapLayout.HuntingField;
            var p = BuilderUtils.Ground(t, c);
            var g = BuilderUtils.Group(parent, "Cementerio", p);
            BuilderUtils.Label(g, "CEMENTERIO", p + Vector3.up * 9f);

            // reja perimetral rectangular -- real (Cemetery/Fence/, todavía sin bajar)
            // si está, si no la del corral (ChainFence/) o postes+alambre procedural
            // (mismo criterio que Corrales). Deja un hueco de portón del lado SUR
            // (frente, por donde se llega).
            const float half = 13f;
            var corners = new[] {
                c + new Vector2(-half, -half), c + new Vector2(half, -half),
                c + new Vector2(half, half),   c + new Vector2(-half, half)
            };
            var cemFenceSrc = FindModelInFolder(DirCemeteryFence) ?? FindModelInFolder(DirFence);
            for (int i = 1; i < 4; i++) // lados 1,2,3 -- el 0 (sur) queda para el portón
            {
                Vector2 a = corners[i], b = corners[(i + 1) % 4];
                if (cemFenceSrc != null) FenceLineModel(cemFenceSrc, g, t, a, b);
                else Fence(g, t, a, b, 3f);
            }
            Vector2 gateMidL = Vector2.Lerp(corners[0], corners[1], 0.35f);
            Vector2 gateMidR = Vector2.Lerp(corners[0], corners[1], 0.65f);
            if (cemFenceSrc != null)
            {
                FenceLineModel(cemFenceSrc, g, t, corners[0], gateMidL);
                FenceLineModel(cemFenceSrc, g, t, gateMidR, corners[1]);
            }
            else
            {
                Fence(g, t, corners[0], gateMidL, 3f);
                Fence(g, t, gateMidR, corners[1], 3f);
            }
            // portalada simple sobre el hueco
            Vector3 postL = BuilderUtils.Ground(t, gateMidL.x, gateMidL.y);
            Vector3 postR = BuilderUtils.Ground(t, gateMidR.x, gateMidR.y);
            BuilderUtils.Prim(PrimitiveType.Cube, "PortonPosteL", g, postL + Vector3.up * 1.1f, new Vector3(0.25f, 2.2f, 0.25f), Wood);
            BuilderUtils.Prim(PrimitiveType.Cube, "PortonPosteR", g, postR + Vector3.up * 1.1f, new Vector3(0.25f, 2.2f, 0.25f), Wood);
            Beam(g, postL + Vector3.up * 2.1f, postR + Vector3.up * 2.1f, 0.12f, Wood);

            // capillita chica al fondo (placeholder reducido, mismo criterio que la
            // Capilla Anegada: nave + campanario + cruz)
            Vector3 chapelP = BuilderUtils.Ground(t, c.x - half + 3.5f, c.y - half + 3.5f);
            BuilderUtils.Prim(PrimitiveType.Cube, "Capilla_Nave", g, chapelP + Vector3.up * 1.2f, new Vector3(3.6f, 2.4f, 4.5f), StoneGrey);
            BuilderUtils.Prim(PrimitiveType.Cube, "Capilla_Campanario", g, chapelP + new Vector3(0f, 3.1f, -2.2f), new Vector3(1.3f, 3.2f, 1.3f), StoneGrey);
            BuilderUtils.Prim(PrimitiveType.Cube, "Capilla_CruzV", g, chapelP + new Vector3(0f, 4.9f, -2.2f), new Vector3(0.12f, 0.7f, 0.12f), Wood);
            BuilderUtils.Prim(PrimitiveType.Cube, "Capilla_CruzH", g, chapelP + new Vector3(0f, 4.7f, -2.2f), new Vector3(0.45f, 0.12f, 0.12f), Wood);

            // lápidas dispersas: modelo real (Tombstone1/) mezclado con cruces simples
            // procedurales -- owner: "necesito mas asi no son todas iguales", así que
            // no repite el mismo modelo para todas.
            var tombSrc = FindModelInFolder(DirTombstone1);
            for (int i = 0; i < 16; i++)
            {
                Vector2 o = Random.insideUnitCircle * (half - 2.5f);
                Vector2 xz = c + o;
                if (Vector2.Distance(xz, new Vector2(chapelP.x, chapelP.z)) < 4f) continue; // no encima de la capilla
                Vector3 tp = BuilderUtils.Ground(t, xz.x, xz.y);
                float yaw = Random.Range(0f, 360f);
                if (tombSrc != null && Random.value < 0.55f)
                {
                    var inst = SpawnModelFrom(tombSrc, g, tp, Random.Range(0.9f, 1.3f), yaw, true, "Lapida" + i);
                    FixTombstoneMaterial(inst);
                }
                else
                {
                    // cruz simple de madera, apenas ladeada (como hundida) -- variante
                    // procedural para que no todas las tumbas sean la misma lápida.
                    float leanX = Random.Range(-8f, 8f), leanZ = Random.Range(-8f, 8f);
                    BuilderUtils.Prim(PrimitiveType.Cube, "CruzV" + i, g, tp + Vector3.up * 0.45f,
                        new Vector3(0.09f, 0.9f, 0.09f), Wood, new Vector3(leanX, yaw, leanZ));
                    BuilderUtils.Prim(PrimitiveType.Cube, "CruzH" + i, g, tp + Vector3.up * 0.7f,
                        new Vector3(0.5f, 0.09f, 0.09f), Wood, new Vector3(leanX, yaw, leanZ));
                }
            }

            // árboles pelados pegados al perímetro (reusa el modelo de DeadTree ya
            // usado en el árbol del ahorcado -- mismo clima que la referencia)
            var deadSrc = FindModelInFolder(DirDeadTree);
            for (int i = 0; i < 6; i++)
            {
                Vector2 o = Random.insideUnitCircle;
                Vector2 xz = c + o.normalized * (half - Random.Range(0.5f, 2.5f));
                Vector3 tp = BuilderUtils.Ground(t, xz.x, xz.y);
                float h = Random.Range(4f, 6.5f);
                if (deadSrc != null) SpawnModelFrom(deadSrc, g, tp, h, Random.Range(0f, 360f), true, "ArbolPelado" + i);
                else
                    BuilderUtils.Prim(PrimitiveType.Cylinder, "TroncoPelado" + i, g,
                        tp + Vector3.up * h * 0.5f, new Vector3(Random.Range(0.2f, 0.35f), h * 0.5f, Random.Range(0.2f, 0.35f)),
                        Burnt, new Vector3(Random.Range(0f, 10f), Random.Range(0f, 360f), Random.Range(0f, 10f)));
            }
            return g;
        }

        // ---------------- MIRADOR (torre, pasando el puente) ----------------
        // owner: "quitaste el mirador que puse volvelo a poner pasando el puente" --
        // la torre de Sketchfab (antes del Campo de Caza, ahora Cementerio) se
        // reubica del lado este del puente (BridgeBuilder: CenterX 315, Span 120 ->
        // extremo este en x=375), como hito aparte junto a la ruta.
        static Transform BridgeLookout(Transform parent, Terrain t)
        {
            float x = 400f; // ~25m pasado el extremo este del puente
            var p = RoadShoulder(t, new Vector2(x, MapLayout.PavedRouteZAt(x)), -20f); // lado B (sur, hacia el lago)
            var g = BuilderUtils.Group(parent, "Mirador", p);
            BuilderUtils.Label(g, "MIRADOR", p + Vector3.up * 9f);

            // torre real (HuntingTower/, Sketchfab "Watch tower remastered wide") o,
            // si todavía no está descargada, una torre procedural simple (mismo
            // criterio que el molino de la Estepa: 4 patas + cruces + plataforma).
            var towerInst = SpawnModel(DirHuntingTower, g, p, 7f, Random.Range(0f, 360f), true, "TorreMirador");
            if (towerInst != null)
            {
                FixTowerMaterial(towerInst);
                // owner: "añadile colisiones a este objeto". MeshCollider no-convexo por cada
                // mesh (la geometría real: patas, escalera, plataforma, barandas) para poder
                // caminar/chocar la torre. Horneado en el builder → se re-aplica cada Generate.
                AddMeshColliders(towerInst, "torre mirador");
            }
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

        // El mirador de Sketchfab (malla única) -- mismo criterio simple que el
        // wharf: solo BaseColor, sin mapear normal/AO.
        static Material _towerMat;
        static void FixTowerMaterial(GameObject inst)
        {
            // Reintentar si el primer Generate corrió antes de que Unity terminara de
            // importar la textura (mismo motivo que FixHouseMaterial).
            if (_towerMat == null || _towerMat.mainTexture == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirHuntingTower + "/textures/Watch_tower_Base_color.png");
                _towerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null && _towerMat.HasProperty("_BaseMap")) _towerMat.SetTexture("_BaseMap", tex);
                if (_towerMat.HasProperty("_Smoothness")) _towerMat.SetFloat("_Smoothness", 0.1f);
                string matPath = "Assets/Settings/HuntingTower.mat";
                _towerMat = BuilderUtils.SaveMaterialStable(_towerMat, matPath); // GUID estable → sin conflictos al regenerar
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = _towerMat;
                r.sharedMaterials = arr;
            }
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
            // largo nativo del segmento (lado mayor XZ) -- se mide SIN tocar la
            // rotación del probe (queda con la rotación propia del prefab, ver
            // comentario en SpawnModelFrom sobre por qué eso importa).
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

        // ---------------- CERCO DE ALAMBRE (YPF) ----------------
        static Material _fenceChainMat, _fenceSteelMat;
        static (Material chain, Material steel) FenceMaterials()
        {
            const string TexDir = "Assets/ExternalAssets/ChainLinkFence/Textures/";
            if (_fenceChainMat == null)
            {
                // MALLA: cutout (alfa) + DOBLE CARA (se ve el tejido de los dos lados).
                var chainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "chainlink_diffuse_128x128_png_chainlink_alpha_128x128.png");
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (chainTex != null) m.SetTexture("_BaseMap", chainTex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Surface", 0f); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                m.SetFloat("_Smoothness", 0.1f);
                _fenceChainMat = BuilderUtils.SaveMaterialStable(m, "Assets/Settings/ChainLinkFence_Chain.mat");
            }
            if (_fenceSteelMat == null)
            {
                var steelTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "chain_link_fence_01_diffuse_256x256.png");
                var s = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (steelTex != null) s.SetTexture("_BaseMap", steelTex);
                s.SetColor("_BaseColor", Color.white);
                s.SetFloat("_Smoothness", 0.2f); s.SetFloat("_Metallic", 0.4f);
                _fenceSteelMat = BuilderUtils.SaveMaterialStable(s, "Assets/Settings/ChainLinkFence_Steel.mat");
            }
            return (_fenceChainMat, _fenceSteelMat);
        }

        // Cerco de alambre alrededor del lote (centro c, medio-ancho halfX en X, medio-fondo halfZ
        // en Z). Cierra NORTE, OESTE y SUR con paneles rectos de 2 m; deja el ESTE abierto (entrada).
        static void FenceYpf(Transform parent, Vector3 c, float halfX, float halfZ, Terrain t)
        {
            var segModel = AssetDatabase.LoadAssetAtPath<GameObject>(DirChainLinkFence + "/chain_link_fence_01.fbx");
            if (segModel == null) { Debug.LogWarning("[YPF] falta " + DirChainLinkFence + "/chain_link_fence_01.fbx — no se arma el cerco. Hacé foco en Unity para que importe el FBX y regenerá."); return; }
            var mats = FenceMaterials();
            var group = BuilderUtils.Group(parent, "CercoYPF", c);
            const float seg = 2f;
            int nx = Mathf.RoundToInt(halfX * 2f / seg); // paneles por lado N/S
            int nz = Mathf.RoundToInt(halfZ * 2f / seg); // por lado O/E

            // NORTE (z = c.z+halfZ) y SUR (z = c.z-halfZ): paneles a lo largo de X (sin rotar).
            for (int i = 0; i < nx; i++)
            {
                float x = c.x - halfX + (i + 0.5f) * seg;
                PlaceFenceSeg(group, segModel, mats.chain, mats.steel, t, x, c.z + halfZ, 0f);
                PlaceFenceSeg(group, segModel, mats.chain, mats.steel, t, x, c.z - halfZ, 0f);
            }
            // OESTE (x = c.x-halfX): paneles a lo largo de Z (rotados 90°). ESTE queda abierto.
            for (int i = 0; i < nz; i++)
            {
                float z = c.z - halfZ + (i + 0.5f) * seg;
                PlaceFenceSeg(group, segModel, mats.chain, mats.steel, t, c.x - halfX, z, 90f);
            }
            Debug.Log($"<color=cyan>[YPF] Cerco de alambre: {nx * 2 + nz} paneles (N/O/S). Lado ESTE abierto = entrada del auto.</color>");
        }

        static void PlaceFenceSeg(Transform parent, GameObject model, Material chain, Material steel, Terrain t, float x, float z, float yaw)
        {
            float y = BuilderUtils.Ground(t, x, z).y;
            var inst = (GameObject)Object.Instantiate(model, parent);
            inst.name = "Valla";
            inst.transform.position = new Vector3(x, y, z);
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            {
                var src = r.sharedMaterials;
                var arr = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string on = src[i] != null ? src[i].name.ToLowerInvariant() : "";
                    arr[i] = (on.Contains("steel") || on.Contains("galv")) ? steel : chain; // por nombre, no por posición
                }
                r.sharedMaterials = arr;
            }
            // Colisión fina del panel para que el jugador no lo atraviese (pivote base-centro → centro y=1).
            var col = inst.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(2f, 2f, 0.15f);
        }

        // PC noventoso ("90s Desktop PC - PSX" by visualdiscette, CC-BY) + silla de oficina (GLB).
        // Se dejan CERCA de la estación, escalados a tamaño real y apoyados en el piso; el owner los
        // acomoda a mano (nombres únicos → el layout guarda su posición/escala al mover/Save Map Layout).
        static void PlaceYpfComputer(Transform parent, Vector3 p, Terrain t)
        {
            var pcModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/DesktopPC/desktop_pc.glb");
            var chairModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ExternalAssets/DesktopPC/office_chair.glb");
            if (pcModel == null && chairModel == null)
            {
                Debug.LogWarning("[YPF] faltan los GLB del PC/silla en Assets/ExternalAssets/DesktopPC/ — hacé foco en Unity para que importen y regenerá.");
                return;
            }
            if (pcModel != null)
                SpawnModelFrom(pcModel, parent, BuilderUtils.Ground(t, p.x - 6f, p.z + 6f), 0.65f, 0f, false, "DesktopPC_YPF");   // ~0.65 m de ancho
            if (chairModel != null)
                // owner: "hay un solo playero y quiero que esté en la silla" -- el playero
                // Richard va SENTADO en esta silla, pero se sienta DESPUÉS de ApplySavedLayout
                // (YpfNpcBuilder.SeatYpfPlayero, llamado desde MapGenerator), cuando la silla
                // ya está en su transform final -- si no, hereda la inclinación del GLB y queda
                // tumbado, y encima el layout la mueve después dejándolo desfasado.
                SpawnModelFrom(chairModel, parent, BuilderUtils.Ground(t, p.x - 6f, p.z + 7.5f), 1.15f, 180f, true, "OfficeChair_YPF"); // ~1.15 m de alto
            Debug.Log("<color=cyan>[YPF] PC noventoso + silla agregados cerca de la estación (acomodalos a mano). Crédito: '90s Desktop PC - PSX' by visualdiscette (CC-BY).</color>");
        }

        // Paneles del cerco YPF que el owner NO quiere. Posición LOCAL (x,z) respecto de CercoYPF
        // (la que muestra el Inspector). owner: "borrá esta valla que se genera acá" — destildar no
        // alcanzaba porque había un panel del builder + un duplicado a mano superpuestos. Esto corre
        // DESPUÉS del layout (MapGenerator) y borra CUALQUIER panel (builder o clon) en esa posición.
        // Para sacar más paneles, agregá su (x,z) local a esta lista.
        static readonly Vector2[] FenceRemoveLocal = { new Vector2(-2f, 24f) };
        public static void RemoveUnwantedFencePanels(Transform mapRoot)
        {
            if (mapRoot == null || FenceRemoveLocal.Length == 0) return;
            Transform cerco = null;
            foreach (var tr in mapRoot.GetComponentsInChildren<Transform>(true))
                if (tr.name == "CercoYPF") { cerco = tr; break; }
            if (cerco == null) return;

            var kids = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in cerco) kids.Add(c);
            int removed = 0;
            foreach (var k in kids)
            {
                if (k == null || !k.name.StartsWith("Valla")) continue;
                Vector3 lp = k.localPosition;
                foreach (var s in FenceRemoveLocal)
                    if (Mathf.Abs(lp.x - s.x) < 1.5f && Mathf.Abs(lp.z - s.y) < 1.5f)
                    { Object.DestroyImmediate(k.gameObject); removed++; break; }
            }
            if (removed > 0) Debug.Log($"<color=cyan>[YPF] {removed} panel(es) de cerco removido(s) por posición (owner).</color>");
        }

        // ---------------- MARCA YPF (re-brand del modelo de la estación) ----------------
        // owner: "cambiá el logo '6twelve' por el de YPF y los colores de alrededor por los del
        // logo, como en la foto". El modelo trae la marca embebida: material "6twelve.001" = el
        // logo del techo; material "Sign" (Image_1, rayas rosa/cyan) = la banda de la marquesina.
        // Se sobreescriben tras instanciar: banda → azul navy YPF; logo → textura YPF generada
        // (ypf_logo.png). El logo va emisivo para que se lea de día y de noche (cartel iluminado).
        static Material _ypfLogoMat, _ypfBandMat, _ypfTotemMat;
        static void StyleYpfStation(GameObject st)
        {
            if (st == null) return;
            if (_ypfLogoMat == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/GasStationProps/ypf_logo.png");
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null) m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0f);   // mate: sin brillo especular del sol
                // owner: "brilla mucho... tiene un borde blanco" — era la EMISIÓN (bloom = halo blanco
                // alrededor + tapaba las sombras). Se saca: material lit normal → recibe/proyecta sombras
                // y no genera halo. (El cartel se ve por la luz de la estación; si de noche queda muy
                // oscuro, subir una emisión SUAVE acá.)
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                // El GLB trae la V invertida → la textura del logo sale AL REVÉS (upside down).
                // Se da vuelta desde el material (scale.y = -1) para que quede derecha con cualquier
                // imagen que se ponga en ypf_logo.png (no hay que editar el archivo).
                m.SetTextureScale("_BaseMap", new Vector2(1f, -1f)); m.SetTextureOffset("_BaseMap", new Vector2(0f, 1f));
                _ypfLogoMat = BuilderUtils.SaveMaterialStable(m, "Assets/Settings/YPF_Logo.mat");
            }
            if (_ypfBandMat == null)
            {
                // owner: "los colores no coinciden: son franjas GRISES con una AZUL central" (como la
                // foto). Textura ypf_band.png = franjas horizontales gris/gris-oscuro/AZUL/.../ (simétrica,
                // a prueba de flip) mapeada sobre la marquesina (misma UV que la banda "Sign" original).
                var bandTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/GasStationProps/ypf_band.png");
                var b = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (bandTex != null) b.SetTexture("_BaseMap", bandTex);
                b.SetColor("_BaseColor", Color.white);
                b.SetFloat("_Smoothness", 0.2f);
                _ypfBandMat = BuilderUtils.SaveMaterialStable(b, "Assets/Settings/YPF_Band.mat");
            }
            if (_ypfTotemMat == null)
            {
                // Tótem de precios (material "6twelve_Sign", Image_53 base + Image_54 emisión). Se
                // reemplaza por ypf_totem_base/emis: logo "6twelve" → caja YPF, labels → SUPER/INFINIA/
                // DIESEL (pintados sobre las originales, en las mismas posiciones, espejados como la UV).
                // Emisión enmascarada (el negro del mapa no brilla) → solo el YPF y los precios se iluminan.
                var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/GasStationProps/ypf_totem_base.png");
                var emisTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ExternalAssets/GasStationProps/ypf_totem_emis.png");
                var s = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (baseTex != null) s.SetTexture("_BaseMap", baseTex);
                s.SetColor("_BaseColor", Color.white);
                s.SetFloat("_Smoothness", 0.2f);
                if (emisTex != null)
                {
                    s.EnableKeyword("_EMISSION");
                    s.SetTexture("_EmissionMap", emisTex);
                    s.SetColor("_EmissionColor", Color.white * 0.6f);
                    s.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                _ypfTotemMat = BuilderUtils.SaveMaterialStable(s, "Assets/Settings/YPF_Totem.mat");
            }

            int logo = 0, band = 0, totem = 0;
            foreach (var r in st.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool isLogo = r.gameObject.name.ToLowerInvariant().Contains("logo");
                var src = r.sharedMaterials;
                var arr = (Material[])src.Clone();
                bool changed = false;
                for (int i = 0; i < src.Length; i++)
                {
                    string n = src[i] != null ? src[i].name : "";
                    if (isLogo) { arr[i] = _ypfLogoMat; changed = true; logo++; }
                    else if (n == "Sign") { arr[i] = _ypfBandMat; changed = true; band++; } // banda a rayas (Image_1)
                    else if (n == "6twelve_Sign") { arr[i] = _ypfTotemMat; changed = true; totem++; } // tótem de precios
                }
                if (changed) r.sharedMaterials = arr;
                if (isLogo) FixLogoRimUVs(r);
            }
            Debug.Log($"<color=cyan>[YPF] Re-brand YPF: {logo} logo + {band} banda + {totem} tótem → YPF.</color>");
        }

        // El panel del cartel es una CAJA con grosor: sus caras LATERALES (el canto) muestrean la
        // textura donde caen las letras → se veía un "borde blanco" al costado. Se clona la malla (no
        // se toca el asset del GLB) y se manda la UV de las caras del canto a una esquina AZUL de la
        // textura. Así el logo puede ocupar casi todo el frente sin que reaparezca el borde. El canto
        // se detecta por el eje MÁS FINO del panel (su normal apunta ahí en el frente/atrás; el canto
        // no), independiente del sistema de ejes del GLB.
        static void FixLogoRimUVs(MeshRenderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var src = mf.sharedMesh;
            if (src.name.EndsWith("_YPFrim")) return; // ya arreglada
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + "_YPFrim";
            var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv;
            if (uvs == null || uvs.Length != verts.Length) return;
            Vector3 sz = mesh.bounds.size;
            int thin = (sz.x <= sz.y && sz.x <= sz.z) ? 0 : (sz.y <= sz.z ? 1 : 2); // eje del grosor
            var tris = mesh.triangles;
            var blue = new Vector2(0.03f, 0.03f); // esquina azul de la textura (margen)
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                Vector3 fn = (norms != null && norms.Length == verts.Length)
                    ? (norms[a] + norms[b] + norms[c]).normalized
                    : Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]).normalized;
                float comp = thin == 0 ? fn.x : (thin == 1 ? fn.y : fn.z);
                if (Mathf.Abs(comp) < 0.6f) { uvs[a] = blue; uvs[b] = blue; uvs[c] = blue; } // canto → azul
            }
            mesh.uv = uvs;
            mf.sharedMesh = mesh;
        }

        // Colisiones a TODO un modelo: una MeshCollider no-convexa por cada mesh ACTIVO.
        // Pensado para props estáticos (la estación YPF): el jugador choca contra tienda,
        // techo, columnas, surtidores y cartel — la geometría real, no una caja. Saltea los
        // meshes inactivos (los cajones/productos que oculta HideCatalogClutter) y los que
        // ya tengan collider. Horneado en el builder → se re-aplica en cada Generate.
        static void AddMeshColliders(GameObject root, string label = "modelo")
        {
            if (root == null) return;
            int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false)) // false = solo activos
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                n++;
            }
            Debug.Log($"<color=cyan>[AreaPoi] {n} colliders (MeshCollider) agregados a {label}.</color>");
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
            // Reintentar si el primer Generate corrió ANTES de que Unity terminara de
            // importar las texturas recién copiadas (owner: "no la colocaste con
            // texturas" -- quedaba cacheado en blanco para el resto de la sesión de
            // Editor). Si falta alguna textura todavía, se reconstruye.
            bool missing = _houseMats == null;
            if (!missing) foreach (var m in _houseMats.Values) if (m == null || m.mainTexture == null) { missing = true; break; }
            if (missing)
            {
                _houseMats = new Dictionary<string, Material>();
                foreach (var n in HouseMatNames)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirHouseAbandoned + "/textures/" + n + ".jpg");
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (tex != null && mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                    string matPath = "Assets/Settings/House_" + n + ".mat";
                    mat = BuilderUtils.SaveMaterialStable(mat, matPath); // GUID estable → sin conflictos al regenerar
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

        // La lápida de Sketchfab (CC0, malla única) -- mismo criterio simple que el
        // wharf: solo Albedo, sin mapear normal/rough/metal/AO.
        static Material _tombstoneMat;
        static void FixTombstoneMaterial(GameObject inst)
        {
            if (inst == null) return;
            // Reintentar si el primer Generate corrió antes de que Unity terminara de
            // importar la textura (mismo motivo que FixHouseMaterial).
            if (_tombstoneMat == null || _tombstoneMat.mainTexture == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTombstone1 + "/textures/TomstoneAlbedo.png");
                _tombstoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null && _tombstoneMat.HasProperty("_BaseMap")) _tombstoneMat.SetTexture("_BaseMap", tex);
                if (_tombstoneMat.HasProperty("_Smoothness")) _tombstoneMat.SetFloat("_Smoothness", 0.1f);
                string matPath = "Assets/Settings/CemeteryTombstone1.mat";
                _tombstoneMat = BuilderUtils.SaveMaterialStable(_tombstoneMat, matPath); // GUID estable → sin conflictos al regenerar
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = _tombstoneMat;
                r.sharedMaterials = arr;
            }
        }

        // El wharf de Sketchfab (Cinema 4D, malla única) trae un material
        // Standard/PBR -- sin pasarlo a URP se ve magenta. Solo el BaseColor (mismo
        // criterio simple que la valla de madera: sin mapear roughness/normal).
        static Material _dockMat;
        static void FixDockMaterial(GameObject inst)
        {
            // Reintentar si el primer Generate corrió antes de que Unity terminara de
            // importar la textura (mismo motivo que FixHouseMaterial).
            if (_dockMat == null || _dockMat.mainTexture == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirDockTex);
                _dockMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null && _dockMat.HasProperty("_BaseMap")) _dockMat.SetTexture("_BaseMap", tex);
                if (_dockMat.HasProperty("_Smoothness")) _dockMat.SetFloat("_Smoothness", 0.15f);
                string matPath = "Assets/Settings/DockWharf.mat";
                _dockMat = BuilderUtils.SaveMaterialStable(_dockMat, matPath); // GUID estable → sin conflictos al regenerar
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
            // ALGUNOS .glb (ej. los exportados por Sketchfab desde software Z-up)
            // traen una rotación HORNEADA en el nodo raíz que corrige eso a Y-up --
            // la reja del cementerio/corral venía "acostada" porque esto la pisaba
            // sin querer (owner: "las rejas estan acostadas no paradas"). Se
            // preserva esa rotación propia del prefab y el yaw/tilt se componen
            // ENCIMA, no en reemplazo.
            Quaternion baked = inst.transform.rotation;
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * (tilt.HasValue ? Quaternion.Euler(tilt.Value) : Quaternion.identity) * baked;
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
