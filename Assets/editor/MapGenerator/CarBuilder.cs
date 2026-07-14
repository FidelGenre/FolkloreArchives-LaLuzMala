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

        // Interior DONANTE (scailman FBX): agarro solo Interior/InteriorPanel/Steerwheel
        // y lo meto adentro de la carcasa PSX. Estos 3 los calibro con capturas para que
        // encaje en la cabina.
        const string DonorFbx     = "Assets/ExternalAssets/SedanDonor/Model/Mesh/Car_Sedan.FBX";
        const float  InteriorScale = 0.13f;
        static readonly Vector3 InteriorOffset = new Vector3(0f, 0.20f, 0.0f);
        const float  InteriorYaw   = 0f;

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

            var steer = AddDonorInterior(car.transform);   // interior REAL del scailman

            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.60f, 0.03f);
            col.size   = new Vector3(1.90f, 1.25f, 5.20f);

            car.AddComponent<Rigidbody>();
            var ctrl = car.AddComponent<FolkloreArchives.CarController>();
            ctrl.driverDoor = null;

            // asiento del CONDUCTOR: detrás y arriba del volante (auto-alineado al Steerwheel).
            Vector3 dSeat = new Vector3(-0.42f, 1.08f, 0.18f); // fallback si no hay volante
            if (steer != null)
                dSeat = car.transform.InverseTransformPoint(steer.position) + new Vector3(0f, 0.45f, -0.30f);
            ctrl.driverSeat     = Seat(car.transform, "Seat_Driver",   dSeat);
            ctrl.frontPassenger = Seat(car.transform, "Seat_FrontPax", dSeat + new Vector3(0.84f, 0f, 0f));
            ctrl.rearLeft       = Seat(car.transform, "Seat_RearL",    dSeat + new Vector3(0f, 0f, -1.10f));
            ctrl.rearRight      = Seat(car.transform, "Seat_RearR",    dSeat + new Vector3(0.84f, 0f, -1.10f));

            car.transform.position = pos + Vector3.up * 0.05f;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            // Escala GLOBAL del auto (carrocería + interior + asientos + collider juntos).
            // Subí CarSize para agrandarlo más. 1.35 → ~7.2m (bien grande, para el jugador de 2.4m).
            car.transform.localScale = Vector3.one * CarSize;
            Debug.Log($"<color=cyan>[CarBuilder] Auto armado. Escala efectiva = {ModelScale * CarSize:0.00} (largo ~{7.37f * ModelScale * CarSize:0.0}m). Si NO ves este mensaje al regenerar, no se ejecutó el codigo nuevo.</color>");
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

        // ---------- INTERIOR DONANTE (scailman) ----------
        // Instancia el FBX del scailman, se queda SOLO con el interior + volante
        // (borra chasis/vidrios/puertas/ruedas/luces) y lo mete escalado en la carcasa.
        // Devuelve el transform del VOLANTE (Steerwheel) para alinear la cámara del asiento.
        static Transform AddDonorInterior(Transform car)
        {
            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(DonorFbx);
            if (donor == null)
            {
                Debug.LogWarning("[CarBuilder] Falta importar " + DonorFbx + " — hacé FOCO en Unity para que importe el FBX y regenerá.");
                return null;
            }
            var inst = (GameObject)Object.Instantiate(donor, car);
            inst.name = "Interior";

            // borrar todo lo que NO es interior (chasis, vidrios, puertas, ruedas, luces).
            // OJO: "Steerwheel" contiene "wheel" → lo excluyo del borrado.
            var kill = new System.Collections.Generic.List<GameObject>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t == inst.transform) continue;
                string n = t.name.ToLower();
                bool isWheel = n.Contains("wheel") && !n.Contains("steer");
                if (n.Contains("chassis") || n.Contains("window") || n.Contains("door") || n.Contains("light") || isWheel)
                    kill.Add(t.gameObject);
            }
            foreach (var g in kill) if (g != null) Object.DestroyImmediate(g);

            inst.transform.localScale = Vector3.one * InteriorScale;
            inst.transform.localPosition = InteriorOffset;
            inst.transform.localRotation = Quaternion.Euler(0f, InteriorYaw, 0f);

            // encontrar el VOLANTE para alinear la cámara del conductor
            Transform steer = null;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains("steer")) { steer = t; break; }

            Debug.Log($"<color=cyan>[CarBuilder] Interior donante (scailman) colocado. Escala {InteriorScale}, offset {InteriorOffset}. Volante {(steer!=null?"OK":"NO encontrado")}.</color>");
            return steer;
        }

        // ---------- INTERIOR procedural (MÍNIMO y SEGURO) — YA NO SE USA ----------
        // Solo tablero + volante + radio, ubicados relativos al asiento del conductor
        // (adelante y abajo de la vista) para que SIEMPRE queden adentro de la cabina y
        // en cámara, sin atravesar la carrocería. Nada de paneles/asientos anchos que
        // (al no conocer las medidas exactas del modelo) se salían para afuera.
        static readonly Vector3 DriverLocal = new Vector3(-0.42f, 1.08f, 0.18f);

        static void BuildInterior(Transform car)
        {
            var dash  = Mat("car_int_dash", DashColor);
            var wheel = Mat("car_int_wheel", WheelColor);
            var glow  = Mat("car_int_radio", RadioGlow, 1.6f);

            // tablero: angosto y bajo, delante de los asientos
            Vector3 dashC = DriverLocal + new Vector3(0.42f, -0.28f, 0.55f); // centrado en X, abajo-adelante
            IBox(car, "Dash",         dashC,                              new Vector3(1.05f, 0.20f, 0.20f), dash);
            IBox(car, "Radio",        dashC + new Vector3(0f, 0.01f, -0.10f), new Vector3(0.22f, 0.12f, 0.10f), dash);
            IBox(car, "RadioDisplay", dashC + new Vector3(0f, 0.03f, -0.05f), new Vector3(0.15f, 0.045f, 0.02f), glow);

            // volante frente al conductor (columna inclinada + aro)
            Vector3 wheelC = DriverLocal + new Vector3(0f, -0.26f, 0.34f);
            IBox(car, "SteerColumn", wheelC + new Vector3(0f, -0.02f, 0.10f), new Vector3(0.05f, 0.05f, 0.28f), wheel, new Vector3(58f, 0f, 0f));
            ICyl(car, "SteerWheel",  wheelC,                                  new Vector3(0.32f, 0.03f, 0.32f), wheel, new Vector3(58f, 0f, 0f));
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
