// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarBuilder.cs — Auto manejable. Usa el modelo COMPLETO de
//  scailman (CC-BY): carrocería + interior real + ventanas de
//  verdad (sin transparencia) + puertas/ruedas/luces separadas.
//  Auto-escalado por bounding box (no adivina el tamaño), apoyado
//  en el piso, sobre la ruta pasando el túnel. Cámara del conductor
//  auto-alineada al volante (Steerwheel). Tinte oscuro/PSX de terror.
//  CRÉDITO: "Low-Poly Sedan car" by scailman (CC Attribution).
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class CarBuilder
    {
        const string CarFbx = "Assets/ExternalAssets/SedanDonor/Model/Mesh/Car_Sedan.FBX";
        const float  TargetLength = 6.0f;    // largo objetivo del auto (m) — subí/bajá para el tamaño
        const float  ModelYawOffset = 0f;    // giro extra si el modelo mira al lado equivocado

        public static GameObject Build(Transform parent, Terrain terrain)
        {
            float carX = MapLayout.TunnelEntranceX + 25f;
            float carZ = MapLayout.PavedRouteZAt(carX);
            var pos = new Vector3(carX, MapLayout.RoadSurfaceHeight, carZ);
            float dz = MapLayout.PavedRouteZAt(carX + 6f) - MapLayout.PavedRouteZAt(carX - 6f);
            float yaw = Mathf.Atan2(12f, dz) * Mathf.Rad2Deg;

            var car = new GameObject("Renault12");
            car.transform.SetParent(parent);
            car.transform.position = Vector3.zero;
            car.transform.rotation = Quaternion.identity;

            var donor = AssetDatabase.LoadAssetAtPath<GameObject>(CarFbx);
            Transform steer = null;
            Transform[] carDoors = new Transform[0];
            if (donor != null)
            {
                var inst = (GameObject)Object.Instantiate(donor, car.transform);
                inst.name = "Car";
                inst.transform.localRotation = Quaternion.Euler(0f, ModelYawOffset, 0f);
                inst.transform.localScale = Vector3.one;

                // AUTO-ESCALADO: medir el modelo y llevarlo a TargetLength (el lado más largo).
                Bounds b = WorldBounds(inst);
                float longest = Mathf.Max(b.size.x, b.size.z);
                float scale = longest > 0.001f ? TargetLength / longest : 1f;
                inst.transform.localScale = Vector3.one * scale;

                // recentrar en X/Z y apoyar el fondo en y=0
                b = WorldBounds(inst);
                inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

                TintMoody(inst);

                var doorList = new System.Collections.Generic.List<Transform>();
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLower();
                    if (steer == null && n.Contains("steer")) steer = t;
                    if (n.Contains("door")) doorList.Add(t);
                }
                carDoors = doorList.ToArray();

                Debug.Log($"<color=cyan>[CarBuilder] scailman completo. escala {scale:0.000}, largo {TargetLength}m. Volante {(steer!=null?"OK":"NO")}, puertas {carDoors.Length}.</color>");
            }
            else
            {
                Debug.LogWarning("[CarBuilder] Falta importar " + CarFbx + " — hacé FOCO en Unity para que importe el FBX y regenerá.");
            }

            // Física + manejo.
            var col = car.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, TargetLength * 0.13f, 0f);
            col.size   = new Vector3(TargetLength * 0.42f, TargetLength * 0.26f, TargetLength * 0.98f);
            car.AddComponent<Rigidbody>();
            var ctrl = car.AddComponent<FolkloreArchives.CarController>();
            ctrl.driverDoor = null;
            ctrl.doors = carDoors;

            // Asiento del conductor: detrás y arriba del volante (auto-alineado).
            Vector3 dSeat = new Vector3(-0.42f, 1.0f, 0.2f);
            if (steer != null)
                dSeat = car.transform.InverseTransformPoint(steer.position) + new Vector3(0f, 0.42f, -0.30f);
            ctrl.driverSeat     = Seat(car.transform, "Seat_Driver",   dSeat);
            ctrl.frontPassenger = Seat(car.transform, "Seat_FrontPax", dSeat + new Vector3(0.84f, 0f, 0f));
            ctrl.rearLeft       = Seat(car.transform, "Seat_RearL",    dSeat + new Vector3(0f, 0f, -1.55f));
            ctrl.rearRight      = Seat(car.transform, "Seat_RearR",    dSeat + new Vector3(0.84f, 0f, -1.55f));

            // Colliders + marcadores para la MIRA (raycast): puertas y asientos.
            AddInteractColliders(ctrl);

            car.transform.position = pos + Vector3.up * 0.05f;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return car;
        }

        // AABB de todos los renderers (en mundo; con el auto en el origen = tamaño real del modelo).
        static Bounds WorldBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // Tinte oscuro/sucio + poco brillo (Falcon viejo de terror).
        static void TintMoody(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * 0.6f);
                    else if (m.HasProperty("_Color")) m.color = m.color * 0.6f;
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
                    else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
        }

        // Colliders-trigger + CarInteractable en cada puerta (sobre su malla) y cada asiento.
        static void AddInteractColliders(FolkloreArchives.CarController ctrl)
        {
            if (ctrl.doors != null)
                foreach (var door in ctrl.doors)
                {
                    if (door == null) continue;
                    var mf = door.GetComponentInChildren<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var host = mf.gameObject;
                    var bc = host.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size * 1.05f;
                    bc.isTrigger = true;
                    var ci = host.AddComponent<FolkloreArchives.CarInteractable>();
                    ci.car = ctrl; ci.part = door; ci.isSeat = false;
                }
            SeatCollider(ctrl.driverSeat, ctrl);
            SeatCollider(ctrl.frontPassenger, ctrl);
            SeatCollider(ctrl.rearLeft, ctrl);
            SeatCollider(ctrl.rearRight, ctrl);
        }

        static void SeatCollider(Transform seat, FolkloreArchives.CarController ctrl)
        {
            if (seat == null) return;
            var bc = seat.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, -0.35f, 0f);   // baja al asiento (el ancla está a la altura del ojo)
            bc.size = new Vector3(0.85f, 1.25f, 0.85f); // grande, fácil de apuntar
            bc.isTrigger = true;
            var ci = seat.gameObject.AddComponent<FolkloreArchives.CarInteractable>();
            ci.car = ctrl; ci.part = seat; ci.isSeat = true;
        }

        static Transform Seat(Transform car, string name, Vector3 lpos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(car);
            g.transform.localPosition = lpos;
            g.transform.localRotation = Quaternion.identity;
            return g.transform;
        }
    }
}
