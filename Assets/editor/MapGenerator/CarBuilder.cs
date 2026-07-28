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
        const float  TargetLength = 6.6f;    // owner: "que sea mas alto o mas grande lo que vos veas" (era 5.8, subo otro poco)
        const float  HeightBoost  = 1.15f;   // owner: "un poquito mas alto" -- estira solo el alto, no el largo
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
            float carHeight = TargetLength * 0.45f * HeightBoost; // fallback si falta el fbx
            if (donor != null)
            {
                var inst = (GameObject)Object.Instantiate(donor, car.transform);
                inst.name = "Car";
                inst.transform.localRotation = Quaternion.Euler(0f, ModelYawOffset, 0f);
                inst.transform.localScale = Vector3.one;

                // AUTO-ESCALADO: medir el modelo y llevarlo a TargetLength (el lado más largo).
                // HeightBoost estira solo el eje Y encima de eso (owner: "un poquito mas alto").
                Bounds b = WorldBounds(inst);
                float longest = Mathf.Max(b.size.x, b.size.z);
                float scale = longest > 0.001f ? TargetLength / longest : 1f;
                inst.transform.localScale = new Vector3(scale, scale * HeightBoost, scale);

                // recentrar en X/Z y apoyar el fondo en y=0
                b = WorldBounds(inst);
                inst.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
                carHeight = b.size.y; // medido REAL (ya con TargetLength+HeightBoost aplicados) -- para los faros

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

            // owner: "necesito que el perro vea que esta abierta la puerta pq desde su
            // camara se ve cerrada" -- antes cada jugador llevaba su PROPIO estado de
            // puertas (local a PlayerVehicleInteractor), así que en red cada cliente veía
            // algo distinto. CarDoors centraliza y sincroniza ese estado (NetworkObject
            // "in-scene placed" -- no rompe nada si nunca se hostea/conecta, CarDoors
            // detecta que no hay red activa y anima localmente igual que antes).
            car.AddComponent<Unity.Netcode.NetworkObject>();
            var carDoorsSync = car.AddComponent<FolkloreArchives.Net.CarDoors>();
            carDoorsSync.car = ctrl;

            // Asiento del conductor: detrás y arriba del volante (auto-alineado).
            // Separaciones entre asientos como proporción de TargetLength (no un
            // número fijo) para que escalen solas si el auto vuelve a cambiar de tamaño.
            // owner: "cuando me subo quedo detras de los asientos" -- probando el
            // asiento del medio de atrás (nuevo, nunca probado con un jugador real
            // sentado ahí -- solo se usaba como referencia para calcular la posición de
            // los amigos decorativos). Las posiciones finales que terminaron sirviendo
            // para los amigos (ajustadas 100% a mano) quedaron con Z ~-0.8, MUCHO menos
            // que lo que da este seatDepth (-1.71) -- la fórmula empuja los asientos
            // traseros bien más atrás de la butaca real. Reducido a la mitad.
            float seatSpread = TargetLength * 0.1409f, seatDepth = TargetLength * -0.13f;
            // owner: "al subirme atras la camara queda detras del asiento" -- los otros
            // 3 asientos se calculaban a partir de dSeat, que YA incluye el empuje hacia
            // atrás (-0.30) para despegar al conductor del volante/tablero. Ese empuje
            // solo tiene sentido para EL CONDUCTOR (por el volante) -- sumado encima al
            // seatDepth de los asientos traseros, los mandaba mucho más atrás de la
            // butaca real. seatBase (sin ese empuje, solo la altura del ojo) es la base
            // común para acompañante/traseros; el conductor solo usa dSeat.
            Vector3 seatBase = new Vector3(-0.31f, 1.0f, 0.15f) * (TargetLength / 4.4f);
            Vector3 dSeat = seatBase;
            if (steer != null)
            {
                // owner: "sigo sin ver a travez de los vidrios" -- en realidad la
                // cámara quedaba METIDA dentro del tablero: este offset (0.42,-0.30)
                // se calibró para TargetLength=4.4 y quedó fijo mientras el auto creció
                // a 6.6 + HeightBoost, así que ya no alcanzaba para despegar el ojo del
                // volante/tablero. Escalado a lo mismo que creció el auto.
                Vector3 wheelLocal = car.transform.InverseTransformPoint(steer.position);
                seatBase = wheelLocal + new Vector3(0f, 0.42f * HeightBoost, 0f) * (TargetLength / 4.4f);
                dSeat = seatBase + new Vector3(0f, 0f, -0.30f) * (TargetLength / 4.4f);
            }
            // owner: "el de adelante esta muy adelante, ponelo mas atras" -- primer
            // intento -0.15 no alcanzó; -0.45 se pasó para el otro lado (ahora atraviesa
            // el respaldo/asiento de atrás). Vuelto a -0.30, el MISMO empuje que ya usa
            // el conductor (dSeat) -- ya está probado que ese valor no choca contra nada.
            Vector3 paxBase = seatBase + new Vector3(0f, 0f, -0.30f) * (TargetLength / 4.4f);
            ctrl.driverSeat     = Seat(car.transform, "Seat_Driver",   dSeat);
            ctrl.frontPassenger = Seat(car.transform, "Seat_FrontPax", paxBase + new Vector3(seatSpread, 0f, 0f));
            ctrl.rearLeft       = Seat(car.transform, "Seat_RearL",    paxBase + new Vector3(0f, 0f, seatDepth));
            ctrl.rearRight      = Seat(car.transform, "Seat_RearR",    paxBase + new Vector3(seatSpread, 0f, seatDepth));
            // owner: "asiento extra en el auto, en la parte de atras en medio" -- banco
            // trasero apretado a 3, a mitad de camino entre rearLeft y rearRight.
            // owner: probó sentar al perro ahí en vivo (Play) y confirmó "toma" con la
            // posición resultante -- horneada TAL CUAL en vez de seguir de la fórmula
            // (mismo criterio que los seatPosOverride de los amigos: más confiable que
            // perseguir la fórmula a ciegas).
            ctrl.rearMid        = Seat(car.transform, "Seat_RearMid",  new Vector3(-0.1558f, 2.11162f, -0.4575f));

            // Colliders + marcadores para la MIRA (raycast): puertas y asientos.
            AddInteractColliders(ctrl);

            // Faros (owner: "usarse las del auto con la misma tecla que la normal" --
            // la linterna del jugador se apaga al subirse de conductor, y F pasa a
            // prender/apagar estos). Apagados por defecto (SetHeadlights los prende).
            ctrl.headlights = BuildHeadlights(car.transform, carHeight);

            car.transform.position = pos + Vector3.up * 0.05f;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // owner: "vamos todos en el auto desde el inicio de mapa hasta la
            // gasolinera" -- hornea la ruta REAL (sigue la curva de PavedRouteZAt)
            // desde el spawn hasta ADENTRO del lote de la estación YPF. MapLayout es
            // editor-only, así que esto se calcula UNA SOLA VEZ acá en Generate --
            // CarAutoDrive (runtime) solo sigue esta lista de puntos.
            var waypoints = new System.Collections.Generic.List<Vector2>();
            float stepX = 8f;
            // owner: "no se mete dentro de la ypf" -- primer intento solo seguía la
            // ruta principal y frenaba EN el asfalto, nunca doblaba hacia el lote (que
            // está a un costado, al norte -- ver MapLayout.YpfPadNearZ/FarZ, un lote
            // aparte de la ruta, no sobre ella). Ahora sigue la ruta hasta pasar un
            // poco YpfStation.x, y agrega puntos más doblando hacia ADENTRO del lote.
            // owner (2da vuelta): "no esta entrando al pavimento, sigue trabando y
            // andando para delante" -- con UN SOLO waypoint de giro, el auto tenía que
            // saltar ~12m de lado (Z) en muy pocos metros de avance (X, el tramo entre
            // turnInX y YpfStation.x) -- un giro demasiado cerrado para completarlo a
            // velocidad crucero con el steer clampeado; lo pasaba de largo sin llegar
            // a "capturar" el waypoint (dist nunca bajaba de arriveRadius) y seguía de
            // largo por la ruta para siempre.
            // owner (3ra vuelta): "dobla muy despues deberia doblar antes osea entrar
            // antes a la ypf, y frenar ni bien entra" -- probé estirar el giro a 30m de
            // anticipación (fue demasiado -- se veía doblando en plena ruta abierta).
            // owner (4ta vuelta): "lo hiciste mal deberia ser a los 5m no a los 30m" --
            // vuelto a un giro CERCA de la estación (5m), como el owner pidió
            // explícitamente. El problema de fondo que hacía que un giro tan cerrado
            // fallara (auto pasándolo de largo a velocidad crucero) se ataca ahora
            // desde el otro lado: CarAutoDrive frena la velocidad de CRUCERO en toda
            // la ruta (ver cruiseSpeedKmh), así que el auto llega mucho más lento a
            // este giro cerrado y sí lo puede completar.
            float turnInX = MapLayout.YpfStation.x - 5f;
            for (float x = carX; x > turnInX; x -= stepX)
                waypoints.Add(new Vector2(x, MapLayout.PavedRouteZAt(x)));
            float roadZAtStation = MapLayout.PavedRouteZAt(MapLayout.YpfStation.x);
            float padMidZ = roadZAtStation + (MapLayout.YpfPadNearZ + MapLayout.YpfPadFarZ) * 0.5f;
            float padEntryZ = roadZAtStation + MapLayout.YpfPadNearZ + 2f;
            waypoints.Add(new Vector2(MapLayout.YpfStation.x - 2.5f, (roadZAtStation + padEntryZ) * 0.5f)); // giro: a mitad de camino
            // owner: "frenar ni bien entra" -- este punto (padEntryZ) marca "ya está
            // ADENTRO del pavimento"; CarAutoDrive.inLotZone frena recién desde acá,
            // no antes (mientras todavía viene acomodando el giro).
            waypoints.Add(new Vector2(MapLayout.YpfStation.x, padEntryZ)); // entra al lote de verdad
            waypoints.Add(new Vector2(MapLayout.YpfStation.x, padMidZ)); // frena adentro, cerca del centro del lote

            var auto = car.AddComponent<FolkloreArchives.CarAutoDrive>();
            auto.waypoints = waypoints.ToArray();
            // owner: "puse play y no spawnie dentro del auto se fue sin mi" -- si esto
            // arranca activo ACÁ (Generate/bake), el auto empieza a manejar desde el
            // frame 1 de Play, ANTES de que OpeningDriveSequence termine de sentar al
            // jugador/perro (el glide de la cámara tarda enterDuration). Queda apagado
            // acá; OpeningDriveSequence lo prende recién después de sentar a los dos.
            auto.active = false;
            ctrl.autoPilot = false;

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
            var glass = GlassMat();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                // owner: "no estoy viendo atra vez de los vidrios deberian ser
                // transparente" -- el vidrio NO es un GameObject separado (todo el
                // auto es una sola malla "Car_Base"), es un SLOT de material dentro
                // del mismo renderer, con el material original del FBX llamado
                // "carwind..." (confirmado mirando los sub-assets del .fbx). Hay que
                // detectarlo por el nombre del material ORIGINAL de cada slot, no por
                // el nombre del GameObject.
                var original = r.sharedMaterials;
                var arr = new Material[original.Length];
                for (int k = 0; k < arr.Length; k++)
                {
                    bool isGlass = original[k] != null && original[k].name.ToLower().Contains("carwind");
                    arr[k] = isGlass ? glass : _carMat;
                }
                r.sharedMaterials = arr;
            }
        }

        static Material _glassMat;
        static Material GlassMat()
        {
            if (_glassMat != null) return _glassMat;
            _glassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _glassMat.SetFloat("_Surface", 1f);          // 0=Opaque, 1=Transparent
            _glassMat.SetFloat("_Blend", 0f);            // Alpha blend
            _glassMat.SetFloat("_AlphaClip", 0f);
            _glassMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glassMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glassMat.SetFloat("_ZWrite", 0f);
            _glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _glassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (_glassMat.HasProperty("_BaseColor")) _glassMat.SetColor("_BaseColor", new Color(0.55f, 0.65f, 0.62f, 0.25f));
            if (_glassMat.HasProperty("_Smoothness")) _glassMat.SetFloat("_Smoothness", 0.8f);
            if (_glassMat.HasProperty("_Metallic")) _glassMat.SetFloat("_Metallic", 0f);
            string matPath = "Assets/Settings/RetroCarGlass.mat";
            AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(_glassMat, matPath);
            return _glassMat;
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
            SeatCollider(ctrl.rearMid, ctrl);
        }

        static void SeatCollider(Transform seat, FolkloreArchives.CarController ctrl)
        {
            if (seat == null) return;
            var bc = seat.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, -0.35f, 0f);   // baja al asiento (el ancla está a la altura del ojo)
            // owner: "al subirme atras aparezco arriba del male green" -- con el nuevo
            // Seat_RearMid apretado ENTRE rearLeft/rearRight (separados solo ~0.93m,
            // seatSpread), el ancho viejo (0.85, mitad 0.425) hacía que los 3 hitboxes
            // traseros se pisaran entre sí -- la mira agarraba el asiento vecino en vez
            // del que apuntabas. Angosto en X (mitad 0.175, bien adentro de los 0.465m
            // de separación a cada vecino) para que cada asiento se targetee solo.
            bc.size = new Vector3(0.35f, 1.25f, 0.85f);
            bc.isTrigger = true;
            var ci = seat.gameObject.AddComponent<FolkloreArchives.CarInteractable>();
            ci.car = ctrl; ci.part = seat; ci.isSeat = true;
        }

        // Faros: 2 spotlights (izq/der) sobre el paragolpes delantero, apagados por
        // defecto. El auto queda recentrado/escalado a TargetLength con el frente en
        // +Z local (mismo eje que CarController.transform.forward, ya probado con el
        // manejo) -- mismo look que la linterna del jugador (Spot, sin sombras, cálido),
        // un poco más de alcance/apertura por ser 2 luces reales de auto.
        // owner: "deberian iluminar mucho las luces del auto, casi no se ven, enfocan
        // debajo del auto no mas" -- la altura (Y) estaba HARDCODEADA en 0.55m, un valor
        // fijo que no se movió cuando el auto creció (TargetLength 4.4→6.6 + HeightBoost
        // 1.15, mismo patrón de bug ya visto en dSeat/seatBase): quedaba metida cerca del
        // piso del paragolpes de un auto mucho más grande, muy baja para tirar luz lejos.
        // Ahora se mide la altura REAL del auto ya escalado (carHeight, medido en Build())
        // y se ubica a una FRACCIÓN de esa altura -- se autoescala solo si el auto vuelve
        // a cambiar de tamaño. Intensidad subida bastante (20→55) porque de noche con
        // niebla/vignette (VhsPostFx) una luz débil se pierde por completo.
        const float HeadlightHeightFrac = 0.35f; // fracción de carHeight, altura típica de paragolpes -- ajustar acá si queda alta/baja
        static Light[] BuildHeadlights(Transform car, float carHeight)
        {
            float front = TargetLength * 0.46f; // cerca de la punta, no exacto (el modelo no es un cubo perfecto)
            float y = carHeight * HeadlightHeightFrac;
            var lights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                float side = TargetLength * (i == 0 ? -0.0909f : 0.0909f); // proporcional al ancho de este auto
                var go = new GameObject("Headlight" + (i == 0 ? "L" : "R"));
                go.transform.SetParent(car);
                go.transform.localPosition = new Vector3(side, y, front);
                go.transform.localRotation = Quaternion.Euler(6f, 0f, 0f); // leve inclinación hacia el piso, como un auto real
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = MapLayout.FlashlightRange * 1.6f;
                l.spotAngle = MapLayout.FlashlightSpotAngle;
                l.intensity = 55f;
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
