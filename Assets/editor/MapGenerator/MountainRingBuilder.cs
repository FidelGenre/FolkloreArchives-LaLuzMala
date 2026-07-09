// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  MountainRingBuilder.cs — anillo de montañas low-poly (HQP
//  "Rocks and Terrains Pack") alrededor de todo el mapa, como
//  telón/horizonte patagónico. No caminables (decorado lejano).
//  Ojo: con la niebla/farClip cortos de noche casi no se ven
//  desde el centro; se aprecian en Scene view o de día.
// ============================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class MountainRingBuilder
    {
        const string Dir = "Assets/HQP STUDIOS/Rocks and Terrains Pack - Low Poly/Prefabs/Terrains/Mountains/LOD/";
        const int    Count      = 48;    // cuántas montañas en el anillo
        const float  RingMargin = 350f;  // metros más allá del borde del mapa (alejadas del área jugable)
        const float  BaseY      = -12f;  // base (un poco hundida para que no flote)
        const float  ScaleMin   = 3.5f;  // ← ancho/base (subí/bajá si quedan chicas/grandes)
        const float  ScaleMax   = 6.0f;
        const float  HeightMin  = 1.9f;  // ← ALTURA (multiplica el ancho): más alto = picos más altos
        const float  HeightMax  = 2.8f;

        public static void Build(Transform parent, Terrain terrain)
        {
            var prefabs = new List<GameObject>();
            for (int i = 1; i <= 20; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Mountain_L_" + i.ToString("00") + "_LOD.prefab");
                if (p != null) prefabs.Add(p);
            }
            if (prefabs.Count == 0) { Debug.LogWarning("MountainRing: no encontré montañas HQP en " + Dir); return; }

            var group = new GameObject("MountainRing");
            group.transform.SetParent(parent);

            float cx = MapLayout.MapSizeX * 0.5f, cz = MapLayout.MapSize * 0.5f;
            float rx = MapLayout.MapSizeX * 0.5f + RingMargin;
            float rz = MapLayout.MapSize * 0.5f + RingMargin;

            float lakeClear = MapLayout.CentralLakeRadius + MapLayout.CentralLakeShore + 140f;
            int placed = 0;
            for (int i = 0; i < Count; i++)
            {
                float ang = (i / (float)Count) * Mathf.PI * 2f + Random.Range(-0.04f, 0.04f);
                float rr  = Random.Range(0.99f, 1.04f); // casi uniforme → pared pareja (todas a la misma lejanía)
                float x = cx + Mathf.Cos(ang) * rx * rr;
                float z = cz + Mathf.Sin(ang) * rz * rr;
                var pos = new Vector2(x, z);

                // Ríos: boca abierta (salteo cualquiera cerca del agua del río).
                if (BuilderUtils.DistToRivers(pos) < 100f) continue;
                // Ruta/túnel y lago: EMPUJO la montaña hacia afuera hasta despejar (así
                // quedan DETRÁS, no encima). La ruta necesita bastante margen.
                int guard = 0;
                while ((BuilderUtils.DistToPolyline(pos, MapLayout.PavedRoute) < 190f ||
                        Vector2.Distance(pos, MapLayout.CentralLakeCenter) < lakeClear) && guard++ < 8)
                {
                    rr += 0.14f;
                    x = cx + Mathf.Cos(ang) * rx * rr;
                    z = cz + Mathf.Sin(ang) * rz * rr;
                    pos = new Vector2(x, z);
                }
                // Si aun así sigue pegada a la ruta (ej. la boca oeste, donde la ruta se
                // va del mapa) o al lago → salteo (valle abierto).
                if (BuilderUtils.DistToPolyline(pos, MapLayout.PavedRoute) < 140f) continue;
                if (Vector2.Distance(pos, MapLayout.CentralLakeCenter) < lakeClear) continue;

                var pf = prefabs[Random.Range(0, prefabs.Count)];
                var m = (GameObject)PrefabUtility.InstantiatePrefab(pf, group.transform);
                m.transform.position = new Vector3(x, BaseY, z);
                float yaw = Mathf.Atan2(cx - x, cz - z) * Mathf.Rad2Deg + Random.Range(-25f, 25f); // mirando al centro + variación
                m.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                float s = Random.Range(ScaleMin, ScaleMax);
                m.transform.localScale = new Vector3(s, s * Random.Range(HeightMin, HeightMax), s); // Y más alto = picos altos
                m.isStatic = true;
                placed++;
            }
            Debug.Log("MountainRing: " + placed + " montañas low-poly alrededor del mapa (con exclusiones ruta/lago/río).");
        }
    }
}
