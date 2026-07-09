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
        const int    Count      = 30;    // cuántas montañas en el anillo (menos = más FPS)
        const float  RingMargin = 160f;  // metros más allá del borde del mapa
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
            int backdropLayer = LayerMask.NameToLayer("Backdrop");
            if (backdropLayer < 0) backdropLayer = 6;

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

                // Ríos y RUTA: corredor/boca abiertos → salteo cualquiera cerca (no las
                // quiero pegadas a la carretera).
                if (BuilderUtils.DistToRivers(pos) < 100f) continue;
                if (BuilderUtils.DistToPolyline(pos, MapLayout.PavedRoute) < 380f) continue;
                // Lago: EMPUJO la montaña hacia afuera hasta despejar (queda DETRÁS del
                // lago, orilla lejana, no encima).
                int guard = 0;
                while (Vector2.Distance(pos, MapLayout.CentralLakeCenter) < lakeClear && guard++ < 8)
                {
                    rr += 0.14f;
                    x = cx + Mathf.Cos(ang) * rx * rr;
                    z = cz + Mathf.Sin(ang) * rz * rr;
                    pos = new Vector2(x, z);
                }
                if (Vector2.Distance(pos, MapLayout.CentralLakeCenter) < lakeClear) continue;
                if (BuilderUtils.DistToPolyline(pos, MapLayout.PavedRoute) < 380f) continue; // por si el empuje la acercó

                var pf = prefabs[Random.Range(0, prefabs.Count)];
                var m = (GameObject)PrefabUtility.InstantiatePrefab(pf, group.transform);
                m.transform.position = new Vector3(x, BaseY, z);
                float yaw = Mathf.Atan2(cx - x, cz - z) * Mathf.Rad2Deg + Random.Range(-25f, 25f); // mirando al centro + variación
                m.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                float s = Random.Range(ScaleMin, ScaleMax);
                m.transform.localScale = new Vector3(s, s * Random.Range(HeightMin, HeightMax), s); // Y más alto = picos altos
                m.isStatic = true;
                // sin sombras (decorado lejano) → ahorra en Scene view / si entran en vista
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                SetLayerRecursive(m, backdropLayer); // capa "Backdrop" → la dibuja la cámara de fondo
                placed++;
            }
            Debug.Log("MountainRing: " + placed + " montañas low-poly alrededor del mapa (con exclusiones ruta/lago/río).");
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
