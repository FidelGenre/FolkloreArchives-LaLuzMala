// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  TerrainSurfaceDetector.cs — owner: "pisadas... segun de que este
//  pisando" -- mapea la textura del Terrain bajo los pies a una de las
//  4 categorías que trae el pack de sonido (WASDSound.WASDEnumMaterial:
//  Wood/Dirt/Stone/Grass). No usa raycast+Material (así viene el
//  WASDRaycast del pack): este terreno es UN SOLO Terrain con capas
//  mezcladas (splat), no objetos separados por superficie, así que se
//  samplea directo el alphamap en la posición del jugador.
//  Orden de capas = TerrainBuilder.PaintTextures (layers[0..8]) — si
//  ese orden cambia, actualizar LayerMaterial acá también.
// ============================================================
using UnityEngine;
using WASDSound;

namespace FolkloreArchives
{
    public static class TerrainSurfaceDetector
    {
        // 0 pasto, 1 barro base, 2 asfalto, 3 pasto seco, 4 sendero/barro,
        // 5 arena, 6 nieve, 7 ceniza, 8 roca -- el pack no tiene categorías
        // para nieve/ceniza/arena, van a Dirt como aproximación razonable.
        static readonly WASDEnumMaterial[] LayerMaterial =
        {
            WASDEnumMaterial.Grass, // 0 pasto
            WASDEnumMaterial.Dirt,  // 1 barro base
            WASDEnumMaterial.Stone, // 2 asfalto
            WASDEnumMaterial.Grass, // 3 pasto seco
            WASDEnumMaterial.Dirt,  // 4 sendero/barro
            WASDEnumMaterial.Dirt,  // 5 arena
            WASDEnumMaterial.Dirt,  // 6 nieve
            WASDEnumMaterial.Dirt,  // 7 ceniza
            WASDEnumMaterial.Stone, // 8 roca
        };

        public static WASDEnumMaterial At(Vector3 worldPos, Transform ignore = null)
        {
            // owner: "usá el sonido de asfalto/piedra para la ruta y la gasolinera". La ruta
            // y el lote de la YPF son MESHES aparte, no pintura del Terrain -- antes se leía
            // el splatmap del terreno DEBAJO (pasto/tierra) y sonaba mal. Primero un raycast
            // corto hacia abajo: si estás parado sobre un mesh de asfalto/cemento (ruta,
            // lote/surtidores YPF) -> Stone. Si es el Terrain (u otra cosa), se sigue como
            // antes leyendo la capa pintada.
            var hits = Physics.RaycastAll(worldPos + Vector3.up * 0.6f, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue; // ignorar al propio jugador
                if (h.collider is TerrainCollider) break;                                // terreno -> splatmap de abajo
                var tp = h.collider.transform;
                string n = (tp.name + " " + (tp.parent != null ? tp.parent.name : "")).ToLowerInvariant();
                if (n.Contains("pavedroad") || n.Contains("asphalt") || n.Contains("asfalt") ||
                    n.Contains("estacion")  || n.Contains("ypf")     || n.Contains("pavement") ||
                    n.Contains("pump")      || n.Contains("surtidor")|| n.Contains("cement") ||
                    n.Contains("concret")   || n.Contains("playon")  || n.Contains("carpet") ||
                    n.Contains("lote")      || n.Contains("_pad")    ||
                    // interior YPF + baño (piso de baldosa = suena a piedra)
                    n.Contains("floor")     || n.Contains("piso")    || n.Contains("suelo") ||
                    n.Contains("tile")      || n.Contains("baldosa") || n.Contains("toilet") ||
                    n.Contains("bath")      || n.Contains("interior"))
                    return WASDEnumMaterial.Stone;
                break; // primer mesh (no terreno) no reconocido -> caer al splatmap
            }

            var terrain = Terrain.activeTerrain;
            if (terrain == null) return WASDEnumMaterial.Stone;
            var td = terrain.terrainData;

            Vector3 local = worldPos - terrain.transform.position;
            float nx = Mathf.Clamp01(local.x / td.size.x);
            float nz = Mathf.Clamp01(local.z / td.size.z);
            int mx = Mathf.Clamp(Mathf.RoundToInt(nx * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1);
            int mz = Mathf.Clamp(Mathf.RoundToInt(nz * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);

            float[,,] map = td.GetAlphamaps(mx, mz, 1, 1);
            int layerCount = map.GetLength(2);
            int best = 0; float bestWeight = -1f;
            for (int i = 0; i < layerCount; i++)
            {
                float w = map[0, 0, i];
                if (w > bestWeight) { bestWeight = w; best = i; }
            }
            return best < LayerMaterial.Length ? LayerMaterial[best] : WASDEnumMaterial.Stone;
        }
    }
}
