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

                StyleCar(inst);

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

        // Estiliza el auto: chapa con textura de ÓXIDO/mugre, vidrios oscuros, ruedas
        // negras, todo MATE (sin plástico). Las partes se distinguen por el nombre.
        static void StyleCar(GameObject inst)
        {
            var rust   = CarRustTex();
            var body   = CarMat("car_body_rust", Color.white,                   rust); // chapa oxidada
            var glass  = CarMat("car_glass2",    new Color(0.06f, 0.07f, 0.09f), null); // vidrio oscuro
            var rubber = CarMat("car_rubber",    new Color(0.05f, 0.05f, 0.05f), null); // gomas/ruedas
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLower();
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (n.Contains("window") || n.Contains("glass")) ms[i] = glass;
                    else if (n.Contains("wheel") || n.Contains("tire")) ms[i] = rubber;
                    else if (n.Contains("light")) ms[i] = MatteCopy(ms[i]);   // luces: dejo su color, solo mate
                    else ms[i] = body;                                        // chapa/interior: óxido
                }
                r.sharedMaterials = ms;
            }
        }

        // Textura procedural de ÓXIDO + mugre (colores cálidos apagados), point filter (PSX).
        static Texture2D CarRustTex()
        {
            string path = MapLayout.GeneratedFolder + "/tex_car_rust.asset";
            var ex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ex != null) return ex;
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var rnd = new System.Random(777);
            Color paint = new Color(0.36f, 0.33f, 0.29f); // pintura vieja apagada
            Color rustC = new Color(0.42f, 0.21f, 0.09f); // óxido
            Color dark  = new Color(0.11f, 0.10f, 0.09f); // manchas oscuras
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float patch = Mathf.PerlinNoise(x * 0.03f + 5f, y * 0.03f + 9f);       // parches de óxido
                    float grain = Mathf.PerlinNoise(x * 0.13f, y * 0.13f) * 0.6f + (float)rnd.NextDouble() * 0.4f;
                    Color c = Color.Lerp(paint, rustC, Mathf.SmoothStep(0.45f, 0.78f, patch));
                    c = Color.Lerp(c, dark, Mathf.Clamp01(grain - 0.7f) * 0.9f);           // manchas/mugre
                    c *= Mathf.Lerp(0.82f, 1.06f, grain);                                  // variación de brillo
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static Material CarMat(string name, Color c, Texture2D tex)
        {
            var m = BuilderUtils.Mat(name, c);
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", new Vector2(1.5f, 1.5f)); }
                else m.mainTexture = tex;
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            return m;
        }

        static Material MatteCopy(Material src)
        {
            if (src == null) return null;
            var m = new Material(src);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
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
