// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarBuilder.cs — Auto manejable. Modelo PSX importado (GGBot Car
//  05 sedán, CC0) para la CARROCERÍA + INTERIOR procedural (piso,
//  tablero, volante, radio, 4 asientos, paneles de puerta) para que
//  adentro se vea una cabina de verdad y no transparente. Puerta de
//  conductor con pivote que se abre al subir. Rigidbody + CarController
//  (manejo arcade). Spawn en la ruta pasando el túnel.
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class CarBuilder
    {
        const string SedanObj = "Assets/ExternalAssets/PSXCars/Sedan/Car5.obj";
        const string SedanTex = "Assets/ExternalAssets/PSXCars/Sedan/car5.png";
        // El OBJ mide ~7.37 de largo → 0.72 lo lleva a ~5.3m (grande, para acompañar
        // al jugador de 2.4m). Subí/bajá este número para agrandar/achicar el auto.
        const float ModelScale = 0.72f;
        // Escala GLOBAL extra encima de ModelScale (agranda TODO el auto junto).
        const float CarSize = 1.35f;

        // Colores del interior (PS1 apagado).
        static readonly Color SeatColor  = new Color(0.16f, 0.13f, 0.12f);
        static readonly Color DashColor  = new Color(0.09f, 0.09f, 0.10f);
        static readonly Color WheelColor = new Color(0.04f, 0.04f, 0.05f);
        static readonly Color PanelColor = new Color(0.13f, 0.12f, 0.12f);
        static readonly Color RadioGlow  = new Color(1.0f, 0.6f, 0.15f);

        public static GameObject Build(Transform parent, Terrain terrain)
        {
            float carX = MapLayout.TunnelEntranceX + 25f;
            float carZ = MapLayout.PavedRouteZAt(carX);
            var pos = new Vector3(carX, MapLayout.RoadSurfaceHeight, carZ);
            float dz = MapLayout.PavedRouteZAt(carX + 6f) - MapLayout.PavedRouteZAt(carX - 6f);
            float yaw = Mathf.Atan2(12f, dz) * Mathf.Rad2Deg;

            var car = new GameObject("Renault12");
            car.transform.SetParent(parent);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SedanObj);
            if (model != null)
            {
                var shell = (GameObject)Object.Instantiate(model, car.transform);
                shell.name = "Body";
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localScale = Vector3.one * ModelScale;
                shell.transform.localRotation = Quaternion.identity; // frente = +Z
                ApplyTexture(shell);
            }
            else
            {
                Debug.LogWarning("[CarBuilder] No encuentro " + SedanObj + " — hacé clic en Unity para importar y regenerá.");
            }

            BuildInterior(car.transform);
            var door = BuildDriverDoor(car.transform);

            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.60f, 0.03f);
            col.size   = new Vector3(1.90f, 1.25f, 5.20f);

            car.AddComponent<Rigidbody>();
            var ctrl = car.AddComponent<FolkloreArchives.CarController>();
            ctrl.driverDoor = door;
            ctrl.driverSeat     = Seat(car.transform, "Seat_Driver",   new Vector3(-0.42f, 1.08f,  0.18f));
            ctrl.frontPassenger = Seat(car.transform, "Seat_FrontPax", new Vector3( 0.42f, 1.08f,  0.18f));
            ctrl.rearLeft       = Seat(car.transform, "Seat_RearL",    new Vector3(-0.42f, 1.08f, -0.90f));
            ctrl.rearRight      = Seat(car.transform, "Seat_RearR",    new Vector3( 0.42f, 1.08f, -0.90f));

            car.transform.position = pos + Vector3.up * 0.05f;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            // Escala GLOBAL del auto (carrocería + interior + asientos + collider juntos).
            // Subí CarSize para agrandarlo más. 1.35 → ~7.2m (bien grande, para el jugador de 2.4m).
            car.transform.localScale = Vector3.one * CarSize;
            return car;
        }

        // ---------- carrocería exterior (textura) ----------
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

        // ---------- INTERIOR procedural ----------
        static void BuildInterior(Transform car)
        {
            var seat = Mat("car_int_seat", SeatColor);
            var dash = Mat("car_int_dash", DashColor);
            var wheel = Mat("car_int_wheel", WheelColor);
            var panel = Mat("car_int_panel", PanelColor);
            var glow = Mat("car_int_radio", RadioGlow, 1.6f);

            // piso + techo interior (tapan el "transparente" arriba/abajo)
            IBox(car, "Int_Floor", new Vector3(0f, 0.42f, -0.35f), new Vector3(1.62f, 0.06f, 2.70f), panel);
            IBox(car, "Int_Roof",  new Vector3(0f, 1.30f, -0.35f), new Vector3(1.55f, 0.05f, 2.30f), panel);
            // paneles laterales (der + trasero-izq; el frente-izq lo tapa la PUERTA)
            IBox(car, "Int_PanelR",   new Vector3( 0.85f, 0.70f, -0.30f), new Vector3(0.05f, 0.55f, 1.95f), panel);
            IBox(car, "Int_PanelLrear",new Vector3(-0.85f, 0.70f, -0.95f), new Vector3(0.05f, 0.55f, 0.95f), panel);
            IBox(car, "Int_Rear",     new Vector3(0f, 0.85f, -1.55f), new Vector3(1.55f, 0.55f, 0.10f), panel);

            // tablero + guantera
            IBox(car, "Dash", new Vector3(0f, 0.96f, 0.82f), new Vector3(1.55f, 0.30f, 0.26f), dash);
            // radio + display encendido
            IBox(car, "Radio",        new Vector3(0f, 0.90f, 0.70f), new Vector3(0.24f, 0.15f, 0.10f), dash);
            IBox(car, "RadioDisplay", new Vector3(0f, 0.92f, 0.76f), new Vector3(0.16f, 0.05f, 0.02f), glow);

            // volante (columna + aro) frente al conductor
            IBox(car, "SteerColumn", new Vector3(-0.42f, 0.90f, 0.60f), new Vector3(0.06f, 0.06f, 0.34f), wheel, new Vector3(58f, 0f, 0f));
            ICyl(car, "SteerWheel",  new Vector3(-0.42f, 0.94f, 0.46f), new Vector3(0.36f, 0.03f, 0.36f), wheel, new Vector3(58f, 0f, 0f));

            // asientos (base + respaldo) x4
            FrontSeat(car, "SeatV_Driver", -0.42f,  0.10f, seat);
            FrontSeat(car, "SeatV_Pax",     0.42f,  0.10f, seat);
            FrontSeat(car, "SeatV_RearL",  -0.42f, -0.95f, seat);
            FrontSeat(car, "SeatV_RearR",   0.42f, -0.95f, seat);
        }

        static void FrontSeat(Transform car, string n, float x, float z, Material m)
        {
            IBox(car, n + "_base", new Vector3(x, 0.56f, z),          new Vector3(0.52f, 0.14f, 0.52f), m);
            IBox(car, n + "_back", new Vector3(x, 0.90f, z - 0.28f),  new Vector3(0.52f, 0.60f, 0.14f), m);
        }

        // ---------- PUERTA del conductor (pivote que gira) ----------
        static Transform BuildDriverDoor(Transform car)
        {
            var pivot = new GameObject("DriverDoorPivot");
            pivot.transform.SetParent(car);
            pivot.transform.localPosition = new Vector3(-0.88f, 0.70f, 0.55f); // bisagra al frente de la puerta
            pivot.transform.localRotation = Quaternion.identity;
            // hoja de la puerta (se extiende hacia atrás desde la bisagra)
            var door = Mat("car_int_door", PanelColor);
            IBox(pivot.transform, "DriverDoor", new Vector3(0f, 0f, -0.46f), new Vector3(0.06f, 0.60f, 0.92f), door);
            return pivot.transform;
        }

        // ---------- helpers ----------
        static Transform Seat(Transform car, string name, Vector3 lpos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(car);
            g.transform.localPosition = lpos;
            g.transform.localRotation = Quaternion.identity;
            return g.transform;
        }

        static Material Mat(string name, Color c, float emission = 0f) => BuilderUtils.Mat(name, c, emission);

        // caja interior (sin collider, para no interferir con la física del auto)
        static GameObject IBox(Transform parent, string n, Vector3 lpos, Vector3 size, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            g.transform.SetParent(parent);
            g.transform.localPosition = lpos;
            g.transform.localScale = size;
            g.transform.localRotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.identity;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject ICyl(Transform parent, string n, Vector3 lpos, Vector3 size, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = n;
            g.transform.SetParent(parent);
            g.transform.localPosition = lpos;
            g.transform.localScale = size;
            g.transform.localRotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.identity;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }
    }
}
