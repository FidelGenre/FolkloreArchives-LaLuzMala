// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LandmarkBuilder.cs — every point of interest from the plan:
//  road start, campsite by the river, old lady's ranch, hunting
//  field, unmarked grave + Luz Mala spawn, main criminal camp,
//  hostage area, secondary camp. Includes item/spawn markers.
//  Paste into:  Assets/Editor/MapGenerator/LandmarkBuilder.cs
// ============================================================
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class LandmarkBuilder
    {
        public static void Build(Transform parent, Terrain t)
        {
            var poi = BuilderUtils.Group(parent, "PointsOfInterest", Vector3.zero);

            var carMat    = BuilderUtils.Mat("car",     new Color(0.55f, 0.10f, 0.08f));
            var blackMat  = BuilderUtils.Mat("black",   new Color(0.08f, 0.08f, 0.08f));
            var tentMat   = BuilderUtils.Mat("tent",    new Color(0.80f, 0.35f, 0.10f));
            var woodMat   = BuilderUtils.Mat("wood",    new Color(0.35f, 0.25f, 0.15f));
            var stoneMat  = BuilderUtils.Mat("stone",   new Color(0.40f, 0.38f, 0.36f));
            var metalMat  = BuilderUtils.Mat("metal",   new Color(0.30f, 0.31f, 0.33f));
            var clothMat  = BuilderUtils.Mat("cloth",   new Color(0.35f, 0.32f, 0.28f));
            var soilMat   = BuilderUtils.Mat("soil",    new Color(0.36f, 0.26f, 0.16f));
            var npcMat    = BuilderUtils.Mat("npc",     new Color(0.60f, 0.55f, 0.45f));
            var wispMat   = BuilderUtils.Mat("wisp",    new Color(1f, 1f, 0.95f), 4f);
            var trunkMat  = BuilderUtils.Mat("trunk",   new Color(0.36f, 0.27f, 0.17f));
            var dryMat    = BuilderUtils.Mat("drybranch", new Color(0.50f, 0.44f, 0.35f));

            // ---------- ROAD START (Act 1) ----------
            // Car spawns INSIDE the tunnel (30m east of the portal face) facing west.
            // The player drives out of the tunnel into the open night as the game begins.
            // Y is fixed at RoadSurfaceHeight (not terrain-sampled: x=1410 is past the terrain edge).
            // Car spawns INSIDE the west tunnel (30m west of the portal face) facing EAST.
            // Player drives east — out of the mountain tunnel onto the open night road.
            float startX = MapLayout.TunnelEntranceX - 30f;  // 30m inside (west of portal)
            float startZ = MapLayout.PavedRouteZAt(MapLayout.TunnelEntranceX);
            float startY = MapLayout.RoadSurfaceHeight + 0.5f; // fixed height: inside tunnel mesh
            BuilderUtils.Label(poi, "START - TUNNEL INTERIOR",
                new Vector3(startX, startY + 7f, startZ));
            var carSpawn = BuilderUtils.Empty(poi, "SPAWN_CAR_START",
                new Vector3(startX, startY, startZ));
            carSpawn.transform.rotation = Quaternion.LookRotation(Vector3.right); // facing east, out of tunnel
            BuilderUtils.Label(poi, "DIRT ROAD TURNOFF", BuilderUtils.Ground(t, MapLayout.DirtTurnoff) + Vector3.up * 7f);

            // ---------- CAMPSITE (Act 1, tutorial; Act 4 return) ----------
            var camp = BuilderUtils.Group(poi, "Campsite", BuilderUtils.Ground(t, MapLayout.Campsite));
            BuilderUtils.Label(camp, "CAMPSITE", camp.position + Vector3.up * 8f);

            var car = BuilderUtils.Group(camp, "Car", BuilderUtils.Ground(t, MapLayout.Campsite.x - 12f, MapLayout.Campsite.y - 16f));
            car.rotation = Quaternion.Euler(0f, 35f, 0f);
            BuilderUtils.Prim(PrimitiveType.Cube, "Body", car, car.position + Vector3.up * 0.85f,
                new Vector3(1.8f, 0.9f, 4.2f), carMat).transform.rotation = car.rotation;
            BuilderUtils.Prim(PrimitiveType.Cube, "Cabin", car, car.position + car.rotation * new Vector3(0f, 1.6f, -0.3f),
                new Vector3(1.6f, 0.7f, 2.0f), blackMat).transform.rotation = car.rotation;
            for (int i = 0; i < 4; i++)
            {
                var off = new Vector3(i % 2 == 0 ? 0.95f : -0.95f, 0.35f, i < 2 ? 1.4f : -1.4f);
                var wheel = BuilderUtils.Prim(PrimitiveType.Cylinder, "Wheel", car,
                    car.position + car.rotation * off, new Vector3(0.65f, 0.15f, 0.65f), blackMat);
                wheel.transform.rotation = car.rotation * Quaternion.Euler(0f, 0f, 90f);
            }

            // campfire (touching it = death, per the script)
            var campfire = BuilderUtils.Group(camp, "Campfire", BuilderUtils.Ground(t, MapLayout.Campsite.x + 3f, MapLayout.Campsite.y + 2f));
            for (int i = 0; i < 3; i++)
                BuilderUtils.Prim(PrimitiveType.Cylinder, "Log", campfire, campfire.position + Vector3.up * 0.2f,
                    new Vector3(0.12f, 0.8f, 0.12f), woodMat, new Vector3(90f, i * 60f, 0f));
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                BuilderUtils.Prim(PrimitiveType.Sphere, "Stone", campfire,
                    campfire.position + new Vector3(Mathf.Cos(ang), 0.12f, Mathf.Sin(ang)) * 1.1f,
                    Vector3.one * 0.35f, stoneMat);
            }
            var fireLight = new GameObject("CampfireLight").AddComponent<Light>();
            fireLight.transform.SetParent(campfire);
            fireLight.transform.position = campfire.position + Vector3.up * 0.8f;
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.2f);
            fireLight.intensity = 2.5f;
            fireLight.range = 14f;

            BuilderUtils.Prim(PrimitiveType.Cube, "Tent1", camp,
                BuilderUtils.Ground(t, MapLayout.Campsite.x - 4f, MapLayout.Campsite.y + 9f) + Vector3.up * 0.75f,
                new Vector3(2.6f, 1.5f, 2.6f), tentMat, new Vector3(0f, 20f, 0f));
            BuilderUtils.Prim(PrimitiveType.Cube, "Tent2", camp,
                BuilderUtils.Ground(t, MapLayout.Campsite.x + 6f, MapLayout.Campsite.y + 10f) + Vector3.up * 0.75f,
                new Vector3(2.6f, 1.5f, 2.6f), tentMat, new Vector3(0f, -35f, 0f));

            BuilderUtils.Empty(camp, "SPAWN_PLAYER1", BuilderUtils.Ground(t, MapLayout.Campsite.x - 2f, MapLayout.Campsite.y - 4f) + Vector3.up * 0.5f);
            BuilderUtils.Empty(camp, "SPAWN_RUFUS", BuilderUtils.Ground(t, MapLayout.Campsite.x + 1f, MapLayout.Campsite.y - 5f) + Vector3.up * 0.5f);
            BuilderUtils.Empty(camp, "WATER_BOTTLE_POINT", BuilderUtils.Ground(t, MapLayout.Campsite.x - 20f, MapLayout.Campsite.y - 5f) + Vector3.up * 0.5f);

            // ---------- OLD LADY'S RANCH (Act 2) ----------
            // El rancho placeholder (cubos Walls/Roof/Door + luz) se ELIMINÓ: la casa
            // real la construye HouseBuilder en el mismo punto (MapLayout.OldLadyRanch),
            // se superponían. Queda solo un marcador vacío + label de referencia.
            BuilderUtils.Label(poi, "OLD LADY'S RANCH",
                BuilderUtils.Ground(t, MapLayout.OldLadyRanch) + Vector3.up * 11f);

            // ---------- HUNTING FIELD (Act 2) ----------
            BuilderUtils.Label(poi, "HUNTING FIELD", BuilderUtils.Ground(t, MapLayout.HuntingField) + Vector3.up * 7f);

            // ---------- UNMARKED GRAVE (Act 2 — the Anchor) ----------
            var grave = BuilderUtils.Group(poi, "Grave", BuilderUtils.Ground(t, MapLayout.Grave));
            BuilderUtils.Label(grave, "UNMARKED GRAVE", grave.position + Vector3.up * 6f);
            BuilderUtils.Prim(PrimitiveType.Sphere, "Mound", grave, grave.position + Vector3.up * 0.1f, new Vector3(1.6f, 0.5f, 2.4f), soilMat);
            BuilderUtils.Prim(PrimitiveType.Cube, "CrossVertical", grave, grave.position + new Vector3(0f, 0.7f, 1.1f), new Vector3(0.12f, 1.4f, 0.12f), woodMat);
            BuilderUtils.Prim(PrimitiveType.Cube, "CrossHorizontal", grave, grave.position + new Vector3(0f, 1.0f, 1.1f), new Vector3(0.7f, 0.12f, 0.12f), woodMat);
            BuilderUtils.Empty(grave, "RUFUS_DIG_POINT", grave.position + Vector3.up * 0.3f);

            // ---------- LUZ MALA SPAWN ----------
            var wisp = BuilderUtils.Group(poi, "LuzMala", BuilderUtils.Ground(t, MapLayout.Grave.x + 18f, MapLayout.Grave.y - 15f) + Vector3.up * 2f);
            BuilderUtils.Label(wisp, "LUZ MALA (SPAWN)", wisp.position + Vector3.up * 4f);
            var sphere = BuilderUtils.Prim(PrimitiveType.Sphere, "Wisp", wisp, wisp.position, Vector3.one * 0.7f, wispMat);
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            var halo = new GameObject("Halo").AddComponent<Light>();
            halo.transform.SetParent(wisp);
            halo.transform.position = wisp.position;
            halo.type = LightType.Point;
            halo.color = Color.white;
            halo.intensity = 3f;
            halo.range = 25f;

            // ---------- MAIN CRIMINAL CAMP (Act 3) ----------
            var criminals = BuilderUtils.Group(poi, "MainCriminalCamp", BuilderUtils.Ground(t, MapLayout.MainCriminalCamp));
            BuilderUtils.Label(criminals, "MAIN CRIMINAL CAMP", criminals.position + Vector3.up * 9f);
            Shack(criminals, t, MapLayout.MainCriminalCamp.x - 8f, MapLayout.MainCriminalCamp.y + 2f, 15f, clothMat, metalMat);
            Shack(criminals, t, MapLayout.MainCriminalCamp.x + 6f, MapLayout.MainCriminalCamp.y - 5f, -40f, clothMat, metalMat);
            Shack(criminals, t, MapLayout.MainCriminalCamp.x + 2f, MapLayout.MainCriminalCamp.y + 9f, 80f, clothMat, metalMat);
            BuilderUtils.Empty(criminals, "SPAWN_CRIMINAL_1", BuilderUtils.Ground(t, MapLayout.MainCriminalCamp.x - 4f, MapLayout.MainCriminalCamp.y) + Vector3.up * 0.5f);
            BuilderUtils.Empty(criminals, "SPAWN_CRIMINAL_2", BuilderUtils.Ground(t, MapLayout.MainCriminalCamp.x + 4f, MapLayout.MainCriminalCamp.y + 4f) + Vector3.up * 0.5f);
            BuilderUtils.Empty(criminals, "SPAWN_CRIMINAL_3", BuilderUtils.Ground(t, MapLayout.MainCriminalCamp.x, MapLayout.MainCriminalCamp.y - 6f) + Vector3.up * 0.5f);
            BuilderUtils.Empty(criminals, "JOURNAL_POINT", BuilderUtils.Ground(t, MapLayout.MainCriminalCamp.x - 8f, MapLayout.MainCriminalCamp.y + 2f) + Vector3.up * 1f);
            WarmLight(criminals, BuilderUtils.Ground(t, MapLayout.MainCriminalCamp.x, MapLayout.MainCriminalCamp.y) + Vector3.up * 1.2f, 18f, 3.2f); // distant campfire glow

            // ---------- HOSTAGE AREA (Act 3 — failed rescue) ----------
            var hostages = BuilderUtils.Group(poi, "HostageArea", BuilderUtils.Ground(t, MapLayout.HostageArea));
            BuilderUtils.Label(hostages, "HOSTAGES (NPCs)", hostages.position + Vector3.up * 7f);
            for (int i = 0; i < 3; i++)
            {
                var pos = BuilderUtils.Ground(t, MapLayout.HostageArea.x + (i - 1) * 4f, MapLayout.HostageArea.y + (i % 2) * 3f);
                ForestBuilder.DryTree(hostages, pos + new Vector3(0.8f, 0f, 0.8f), trunkMat, dryMat);
                BuilderUtils.Prim(PrimitiveType.Capsule, "TiedNPC_" + (i + 1), hostages, pos + Vector3.up * 1f, new Vector3(0.7f, 0.9f, 0.7f), npcMat);
            }

            // ---------- SECONDARY CAMP (Act 4 — car parts under pressure) ----------
            var secondary = BuilderUtils.Group(poi, "SecondaryCamp", BuilderUtils.Ground(t, MapLayout.SecondaryCamp));
            BuilderUtils.Label(secondary, "SECONDARY CAMP", secondary.position + Vector3.up * 8f);
            Shack(secondary, t, MapLayout.SecondaryCamp.x - 5f, MapLayout.SecondaryCamp.y + 3f, 25f, clothMat, metalMat);
            Shack(secondary, t, MapLayout.SecondaryCamp.x + 6f, MapLayout.SecondaryCamp.y - 4f, -60f, clothMat, metalMat);
            BuilderUtils.Empty(secondary, "CAR_PART_POINT_1", BuilderUtils.Ground(t, MapLayout.SecondaryCamp.x + 4f, MapLayout.SecondaryCamp.y + 6f) + Vector3.up * 0.5f);
            BuilderUtils.Empty(secondary, "CAR_PART_POINT_2", BuilderUtils.Ground(t, MapLayout.SecondaryCamp.x - 8f, MapLayout.SecondaryCamp.y - 3f) + Vector3.up * 0.5f);
            BuilderUtils.Empty(secondary, "CAR_PART_POINT_3", BuilderUtils.Ground(t, MapLayout.SecondaryCamp.x + 2f, MapLayout.SecondaryCamp.y - 9f) + Vector3.up * 0.5f);
            WarmLight(secondary, BuilderUtils.Ground(t, MapLayout.SecondaryCamp.x, MapLayout.SecondaryCamp.y) + Vector3.up * 1.2f, 16f, 2.6f); // distant camp glow

            // Zonas nuevas del owner (editor de plano) - por ahora, marcadores.
            var lakeMt = BuilderUtils.Group(poi, "LakeMountain", BuilderUtils.Ground(t, MapLayout.LakeMountain));
            BuilderUtils.Label(lakeMt, "MONTAÑA Y LAGO", lakeMt.position + Vector3.up * 8f);
            var wrongTurn = BuilderUtils.Group(poi, "WrongTurnDeath", BuilderUtils.Ground(t, MapLayout.WrongTurnDeath));
            BuilderUtils.Label(wrongTurn, "MUERTE CAMINO EQUIVOCADO", wrongTurn.position + Vector3.up * 7f);
            var lookout = BuilderUtils.Group(poi, "LakeLookout", BuilderUtils.Ground(t, MapLayout.LakeLookout));
            BuilderUtils.Label(lookout, "MIRADOR DEL LAGO", lookout.position + Vector3.up * 7f);
            var cabin = BuilderUtils.Group(poi, "AbandonedCabin", BuilderUtils.Ground(t, MapLayout.AbandonedCabin));
            BuilderUtils.Label(cabin, "CABAÑA ABANDONADA", cabin.position + Vector3.up * 7f);
            Shack(cabin, t, MapLayout.AbandonedCabin.x, MapLayout.AbandonedCabin.y, 30f, clothMat, metalMat);

            // Zonas del plano de dos lados (lado ESTE + escape):
            var mirE = BuilderUtils.Group(poi, "LookoutEast", BuilderUtils.Ground(t, MapLayout.LookoutEast));
            BuilderUtils.Label(mirE, "MIRADOR ESTE", mirE.position + Vector3.up * 7f);
            var cabE = BuilderUtils.Group(poi, "CabinEast", BuilderUtils.Ground(t, MapLayout.CabinEast));
            BuilderUtils.Label(cabE, "CABAÑA ESTE", cabE.position + Vector3.up * 7f);
            Shack(cabE, t, MapLayout.CabinEast.x, MapLayout.CabinEast.y, 200f, clothMat, metalMat);
            var esc = BuilderUtils.Group(poi, "EscapePoint", BuilderUtils.Ground(t, MapLayout.EscapePoint));
            BuilderUtils.Label(esc, "ESCAPE", esc.position + Vector3.up * 8f);

            // PUENTE PEATONAL sobre el cruce del río (vieja/campamento ↔ mirador este),
            // así el cruce a pie no queda bloqueado por agua. Se apoya en la altura de
            // las orillas (sampleada) para no flotar. Cruce ~ (598, 590).
            {
                float bx = 598f, bz = 590f, halfLen = 42f;
                float wy = t.SampleHeight(new Vector3(bx - halfLen, 0f, bz));
                float ey = t.SampleHeight(new Vector3(bx + halfLen, 0f, bz));
                float deckY = Mathf.Max(wy, ey) + 0.15f;
                var fb = BuilderUtils.Group(poi, "FootBridge", new Vector3(bx, deckY, bz));
                BuilderUtils.Label(fb, "PUENTE PEATONAL", new Vector3(bx, deckY + 4f, bz));
                BuilderUtils.Prim(PrimitiveType.Cube, "Deck", fb, new Vector3(bx, deckY, bz),
                    new Vector3(halfLen * 2f, 0.4f, 4.5f), woodMat);
                BuilderUtils.Prim(PrimitiveType.Cube, "RailN", fb, new Vector3(bx, deckY + 0.65f, bz + 2.1f),
                    new Vector3(halfLen * 2f, 1.1f, 0.15f), woodMat);
                BuilderUtils.Prim(PrimitiveType.Cube, "RailS", fb, new Vector3(bx, deckY + 0.65f, bz - 2.1f),
                    new Vector3(halfLen * 2f, 1.1f, 0.15f), woodMat);
            }

            // a warm lantern at the player's campsite so it reads as a safe, lit haven
            WarmLight(camp, BuilderUtils.Ground(t, MapLayout.Campsite.x - 3f, MapLayout.Campsite.y + 6f) + Vector3.up * 1.6f, 10f, 2.2f);

            // Every prop here is fixed set-dressing (shacks, tents, campfires, the
            // parked car, grave) - marking the whole subtree static lets Unity's
            // automatic static batching merge these into fewer draw calls.
            BuilderUtils.MarkStaticRecursive(poi);
        }

        // A warm point light (campfire / lantern glow) - the FtF "warm light in the
        // dark" focal points. No shadows (cheap; there are several of these).
        static void WarmLight(Transform parent, Vector3 pos, float range, float intensity)
        {
            var go = new GameObject("WarmLight");
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.28f); // warm fire orange
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        static void Shack(Transform parent, Terrain t, float x, float z, float rotY, Material clothMat, Material metalMat)
        {
            var pos = BuilderUtils.Ground(t, x, z);
            var g = BuilderUtils.Group(parent, "Shack", pos);
            g.rotation = Quaternion.Euler(0f, rotY, 0f);
            BuilderUtils.Prim(PrimitiveType.Cube, "Walls", g, pos + Vector3.up * 1.15f,
                new Vector3(3.5f, 2.3f, 3.5f), clothMat).transform.rotation = g.rotation;
            BuilderUtils.Prim(PrimitiveType.Cube, "Roof", g, pos + Vector3.up * 2.45f,
                new Vector3(4.2f, 0.15f, 4.2f), metalMat, new Vector3(5f, rotY, 0f));
        }
    }
}
