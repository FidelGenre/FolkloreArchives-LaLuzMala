// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarBuilder.cs — Auto manejable. Usa "Retro Car with Interior" de
//  bricchi-games (CC-BY, itch.io) -- owner: "este auto esta mejor no?"
//  (estilo PSX real, interior modelado con volante, 4 puertas +
//  capot + baúl con pivots ya puestos para animar). Reemplaza al
//  sedán de scailman usado antes.
//  Auto-escalado por bounding box (no adivina el tamaño), apoyado
//  en el piso, sobre la ruta pasando el túnel. Cámara del conductor
//  auto-alineada al volante (Steering_wheel).
//  CRÉDITO: "Retro Car With Interior" by bricchi-games (CC Attribution).
// ============================================================
using UnityEngine;
using UnityEditor;

namespace FolkloreArchives.MapGen
{
    public static class CarBuilder
    {
        const string CarFbx = "Assets/ExternalAssets/RetroCar/models/car.fbx";
        const string CarTexDir = "Assets/ExternalAssets/RetroCar/textures";
        const float  TargetLength = 5.8f;    // owner: "necesito qeu sea bastante mas grande" (era 4.4, muy chico)
        const float  ModelYawOffset = 0f;    // giro extra si el modelo mira al lado equivocado

        public static GameObject Build(Transform parent, Terrain terrain)
        {
            // owner: "quiero que arranque por el otro lado el auto" -- antes cerca de
            // la entrada del túnel (oeste, inicio del mapa), ahora cerca del borde
            // este (mismo criterio de offset, del otro extremo), para no tener que
            // manejar todo el mapa para probar el cementerio/campamento/cabañas nuevos.
            float carX = MapLayout.MapSizeX - 30f;
            float carZ = MapLayout.PavedRouteZAt(carX);
            // owner: "esta spwaneado debajo de la tierra" -- RoadSurfaceHeight es la
            // altura NOMINAL de la ruta pavimentada, pero cerca del borde este del
            // mapa el terreno real puede quedar más alto que ese valor fijo (mismo
            // bug que ya se había arreglado en TestPlayerBuilder). Muestreo el
            // terreno real y uso el mayor de los dos.
            float terrainY = terrain != null
                ? terrain.SampleHeight(new Vector3(carX, 0f, carZ)) + terrain.transform.position.y
                : MapLayout.RoadSurfaceHeight;
            float groundY = Mathf.Max(MapLayout.RoadSurfaceHeight, terrainY);
            var pos = new Vector3(carX, groundY, carZ);
            float dz = MapLayout.PavedRouteZAt(carX + 6f) - MapLayout.PavedRouteZAt(carX - 6f);
            // owner: "el auto esta mirando en direccion contraria" -- la fórmula
            // original apuntaba siempre hacia +X (tenía sentido cerca del túnel,
            // "hacia adentro" del mapa); ahora que arranca del lado ESTE, +X es
            // "hacia afuera" del mapa. +180° para que mire hacia el mapa otra vez.
            float yaw = Mathf.Atan2(12f, dz) * Mathf.Rad2Deg + 180f;

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

                Debug.Log($"<color=cyan>[CarBuilder] Retro Car completo. escala {scale:0.000}, largo {TargetLength}m. Volante {(steer!=null?"OK":"NO")}, puertas {carDoors.Length}.</color>");
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
            // Separaciones entre asientos como proporción de TargetLength (no un
            // número fijo) para que escalen solas si el auto vuelve a cambiar de tamaño.
            float seatSpread = TargetLength * 0.1409f, seatDepth = TargetLength * -0.2591f;
            Vector3 dSeat = new Vector3(-0.31f, 1.0f, 0.15f);
            if (steer != null)
                dSeat = car.transform.InverseTransformPoint(steer.position) + new Vector3(0f, 0.42f, -0.30f);
            ctrl.driverSeat     = Seat(car.transform, "Seat_Driver",   dSeat);
            ctrl.frontPassenger = Seat(car.transform, "Seat_FrontPax", dSeat + new Vector3(seatSpread, 0f, 0f));
            ctrl.rearLeft       = Seat(car.transform, "Seat_RearL",    dSeat + new Vector3(0f, 0f, seatDepth));
            ctrl.rearRight      = Seat(car.transform, "Seat_RearR",    dSeat + new Vector3(seatSpread, 0f, seatDepth));

            // Colliders + marcadores para la MIRA (raycast): puertas y asientos.
            AddInteractColliders(ctrl);

            // Faros (owner: "usarse las del auto con la misma tecla que la normal" --
            // la linterna del jugador se apaga al subirse de conductor, y F pasa a
            // prender/apagar estos). Apagados por defecto (SetHeadlights los prende).
            ctrl.headlights = BuildHeadlights(car.transform);

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

        // Estiliza el auto: un único material URP con la textura NEGRA del pack (el
        // FBX de itch.io a veces trae la ruta de textura rota -- la asignamos a mano
        // en vez de confiar en lo que haya importado Unity, mismo criterio que el
        // resto de los assets de Sketchfab/itch.io de esta sesión) + el mapa emisivo
        // del pack (faros/luces traseras) para que brillen un poco incluso con los
        // faros de juego apagados.
        static Material _carMat;
        static void StyleCar(GameObject inst)
        {
            if (_carMat == null || _carMat.mainTexture == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CarTexDir + "/BlackCarTexture.png");
                var emissive = AssetDatabase.LoadAssetAtPath<Texture2D>(CarTexDir + "/EmissiveTexture.png");
                _carMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (tex != null && _carMat.HasProperty("_BaseMap")) _carMat.SetTexture("_BaseMap", tex);
                if (_carMat.HasProperty("_Smoothness")) _carMat.SetFloat("_Smoothness", 0.15f);
                if (_carMat.HasProperty("_Metallic")) _carMat.SetFloat("_Metallic", 0f);
                if (emissive != null && _carMat.HasProperty("_EmissionMap"))
                {
                    _carMat.SetTexture("_EmissionMap", emissive);
                    _carMat.SetColor("_EmissionColor", Color.white);
                    _carMat.EnableKeyword("_EMISSION");
                    _carMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                string matPath = "Assets/Settings/RetroCarBlack.mat";
                AssetDatabase.DeleteAsset(matPath);
                AssetDatabase.CreateAsset(_carMat, matPath);
            }
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < arr.Length; k++) arr[k] = _carMat;
                r.sharedMaterials = arr;
            }
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

        // Faros: 2 spotlights (izq/der) sobre el paragolpes delantero, apagados por
        // defecto. El auto queda recentrado/escalado a TargetLength con el frente en
        // +Z local (mismo eje que CarController.transform.forward, ya probado con el
        // manejo) -- mismo look que la linterna del jugador (Spot, sin sombras, cálido),
        // un poco más de alcance/apertura por ser 2 luces reales de auto.
        static Light[] BuildHeadlights(Transform car)
        {
            float front = TargetLength * 0.46f; // cerca de la punta, no exacto (el modelo no es un cubo perfecto)
            var lights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                float side = TargetLength * (i == 0 ? -0.0909f : 0.0909f); // proporcional al ancho de este auto
                var go = new GameObject("Headlight" + (i == 0 ? "L" : "R"));
                go.transform.SetParent(car);
                go.transform.localPosition = new Vector3(side, 0.55f, front);
                go.transform.localRotation = Quaternion.identity;
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = MapLayout.FlashlightRange * 1.4f;
                l.spotAngle = MapLayout.FlashlightSpotAngle;
                l.intensity = 20f;
                l.color = new Color(0.95f, 0.93f, 0.82f); // blanco cálido, más frío que la linterna
                l.shadows = LightShadows.None;
                l.enabled = false; // CarController.SetHeadlights los prende
                lights[i] = l;
            }
            return lights;
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
