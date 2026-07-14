// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarBuilder.cs — Auto manejable. Usa el modelo PSX importado
//  (GGBot "PSX Style Cars", Car 05 sedán, CC0) escalado a medidas
//  reales y ubicado en la ruta pasando el túnel. Si el OBJ todavía
//  no está importado, cae a un placeholder simple para no romper.
//  El manejo / entrar-salir / radio se agregan en las fases siguientes.
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class CarBuilder
    {
        const string SedanObj = "Assets/ExternalAssets/PSXCars/Sedan/Car5.obj";
        const string SedanTex = "Assets/ExternalAssets/PSXCars/Sedan/car5.png";
        // El OBJ mide ~7.37 de largo → 0.59 lo lleva a ~4.35m (largo real de un R12).
        const float ModelScale = 0.59f;

        public static GameObject Build(Transform parent, Terrain terrain)
        {
            float carX = MapLayout.TunnelEntranceX + 25f;          // en la ruta, pasando el túnel
            float carZ = MapLayout.PavedRouteZAt(carX);
            var pos = new Vector3(carX, MapLayout.RoadSurfaceHeight, carZ);
            float dz = MapLayout.PavedRouteZAt(carX + 6f) - MapLayout.PavedRouteZAt(carX - 6f);
            float yaw = Mathf.Atan2(12f, dz) * Mathf.Rad2Deg;      // mirando al este (afuera)

            var car = new GameObject("Renault12");
            car.transform.SetParent(parent);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SedanObj);
            if (model != null)
            {
                var shell = (GameObject)Object.Instantiate(model, car.transform);
                shell.name = "Body";
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localScale = Vector3.one * ModelScale;
                // El frente del modelo ya es +Z (adelante) → sin giro extra.
                shell.transform.localRotation = Quaternion.identity;
                ApplyTexture(shell);
            }
            else
            {
                Debug.LogWarning("[CarBuilder] No encuentro " + SedanObj +
                    " — hacé clic en Unity para que importe el OBJ y regenerá. Pongo un placeholder.");
                Placeholder(car.transform);
            }

            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.65f, 0f);
            col.size   = new Vector3(1.65f, 1.20f, 4.35f);

            car.transform.position = pos + Vector3.up * 0.02f;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return car;
        }

        // Material URP con la textura PSX del auto (baja gloss para que no brille plástico).
        static void ApplyTexture(GameObject go)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SedanTex);
            if (tex == null) return;
            var mat = BuilderUtils.MatTextured("car5", tex, Color.white, 0.08f);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        // Fallback mínimo (una caja) si el OBJ no está importado todavía.
        static void Placeholder(Transform car)
        {
            BuilderUtils.Prim(PrimitiveType.Cube, "Placeholder", car, Vector3.zero,
                new Vector3(1.6f, 1.2f, 4.3f), BuilderUtils.Mat("car_body", new Color(0.86f, 0.86f, 0.83f)));
        }
    }
}
