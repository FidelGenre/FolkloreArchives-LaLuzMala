// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  PsxFurnitureBuilder.cs — owner: "añadime estos muebles a la casa de la vieja".
//  Asset: "PSX Furniture Pack" by Akneeee (itch.io, name-your-price) — ver
//  ASSET_CREDITS.md. GLB único (furniture.glb) con todas las piezas (mesita, silla,
//  ropero, mesa ratona, sofá, mesa, maceta, alfombra, cuadro) YA dispuestas como una
//  habitación; texturas embebidas → Unity lo importa a URP solo.
//
//  El pack viene ~2.3× grande (silla 2.1 m, ropero 4.6 m) → se escala a tamaño real.
//  Se instancia ENTERO (conserva la disposición de habitación del autor), centrado en
//  la planta de la casa ALP y apoyado en el piso. Bajo FOLKLORE_MAP con nombre único
//  "MueblesVieja" → guardable con Save Map Layout: el owner acomoda el grupo o cada
//  pieza a mano y persiste al regenerar. Se llama desde HouseBuilder.BuildAlpHouse
//  (antes de ApplySavedLayout).
// ============================================================
using UnityEditor;
using UnityEngine;

namespace FolkloreArchives.MapGen
{
    public static class PsxFurnitureBuilder
    {
        const string Glb   = "Assets/ExternalAssets/PSXFurniture/furniture.glb";
        const float  Scale = 0.43f;   // el pack viene ~2.3× grande → a tamaño real (ajustable)

        public static void Build(Transform mapRoot, Bounds houseBounds)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Glb);
            if (prefab == null)
            {
                Debug.LogWarning("[Muebles] falta " + Glb + " (¿Unity terminó de importar el GLB?). No pongo los muebles.");
                return;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "MueblesVieja";
            inst.transform.SetParent(mapRoot, true);          // bajo el mapa (escala 1), NO bajo la casa (evita doble escala)
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * Scale;

            // centrar en la planta de la casa (XZ) y apoyar la base en el piso
            float floorY = houseBounds.min.y + 0.05f;
            Bounds b = PropBounds(inst);
            inst.transform.position += new Vector3(
                houseBounds.center.x - b.center.x,
                floorY - b.min.y,
                houseBounds.center.z - b.center.z);

            // colliders por pieza (que el jugador no atraviese los muebles)
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;

            Debug.Log("<color=lime>[Muebles] PSX Furniture Pack (Akneeee) dentro de la casa de la vieja. " +
                      "Acomodá el grupo o cada pieza a mano y Tools ▸ Folklore Archives ▸ Save Map Layout.</color>");
        }

        static Bounds PropBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
