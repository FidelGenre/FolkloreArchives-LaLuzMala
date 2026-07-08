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
        const float  RingMargin = 90f;   // metros más allá del borde del mapa
        const float  BaseY      = -8f;   // base (un poco hundida para que no flote)
        const float  ScaleMin   = 3.0f;  // ← subí/bajá esto si quedan chicas/grandes
        const float  ScaleMax   = 5.5f;

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

            for (int i = 0; i < Count; i++)
            {
                float ang = (i / (float)Count) * Mathf.PI * 2f + Random.Range(-0.04f, 0.04f);
                float rr  = Random.Range(0.97f, 1.15f); // jitter radial → no un óvalo perfecto
                float x = cx + Mathf.Cos(ang) * rx * rr;
                float z = cz + Mathf.Sin(ang) * rz * rr;

                var pf = prefabs[Random.Range(0, prefabs.Count)];
                var m = (GameObject)PrefabUtility.InstantiatePrefab(pf, group.transform);
                m.transform.position = new Vector3(x, BaseY, z);
                float yaw = Mathf.Atan2(cx - x, cz - z) * Mathf.Rad2Deg + Random.Range(-25f, 25f); // mirando al centro + variación
                m.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                float s = Random.Range(ScaleMin, ScaleMax);
                m.transform.localScale = new Vector3(s, s * Random.Range(0.85f, 1.35f), s);
                m.isStatic = true;
            }
            Debug.Log("MountainRing: " + Count + " montañas low-poly alrededor del mapa.");
        }
    }
}
