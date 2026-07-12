// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  LuzMalaBuilder.cs — coloca La Luz Mala en el mapa (de noche
//  aparece solita). v1: una, en el bosque al norte del campamento,
//  en el camino hacia el rancho de la vieja. El comportamiento vive
//  en LuzMala.cs (runtime).
// ============================================================
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class LuzMalaBuilder
    {
        // dónde aparece (x,z world). Cerca del rancho de la vieja (404,625), en tierra
        // firme y seca (la posición anterior 412,492 caía sobre el agua).
        public static readonly Vector2 SpawnXZ = new Vector2(404, 608);

        public static void Build(Transform parent, Terrain terrain)
        {
            var go = new GameObject("LuzMala");
            go.transform.SetParent(parent);
            float gy = terrain != null
                ? terrain.SampleHeight(new Vector3(SpawnXZ.x, 0f, SpawnXZ.y)) + terrain.transform.position.y
                : 20f;
            go.transform.position = new Vector3(SpawnXZ.x, gy + 1.6f, SpawnXZ.y);
            go.AddComponent<FolkloreArchives.LuzMala>();
            Debug.Log("<color=orange>La Luz Mala colocada (aparece de noche).</color>");
        }
    }
}
