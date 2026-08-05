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
            // owner: FindRoadTip() (calcular la punta leyendo el mesh real) volvió a
            // fallar incluso con la escena limpia -- ya van 2 veces que esta técnica
            // agarra un punto que no es el centro real del camino. Vuelto a
            // horneado directo: el owner voló hasta la punta real (escena ya
            // sincronizada limpia con el compañero) y pasó Position/Rotation tal
            // cual -- mismo criterio que Seat_RearMid más abajo, la única forma que
            // funcionó de verdad en toda esta saga.
            var pos = new Vector3(1868.026f, 17.04301f, 10.39595f);
            float yaw = -104.379f;

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
            // owner: "estas mapeando mal la ruta" -- la línea recta (intento
            // anterior) no sigue la curva real, así que el auto quedaba fuera del
            // asfalto apenas la ruta se desviaba. Los intentos previos de leer la
            // malla por NOMBRE ("PavedRoad_Surface") fallaron porque la escena tiene
            // 70+ copias rotas de ese objeto. Esta vez, en vez de buscar por
            // nombre, se usa la FÍSICA real de Unity: un abanico de raycasts hacia
            // abajo por delante del auto, buscando dónde hay asfalto de verdad
            // (material "...asphalt..." o la capa de asfalto pintada en el
            // Terrain) -- sigue lo que la escena REALMENTE tiene, no un nombre de
            // objeto que puede estar duplicado/roto.
            float turnInX = MapLayout.YpfStation.x - 5f;
            Vector2 straightEnd = new Vector2(turnInX, MapLayout.PavedRouteZAt(turnInX));
            Vector2 straightStart = new Vector2(pos.x, pos.z);
            var traced = TraceRoadByRaycast(pos, straightEnd, stepDist: 8f,
                                             maxDist: Vector2.Distance(straightStart, straightEnd) * 1.5f + 60f);
            // owner: "sigue yendose para afuera, no vuelve" -- el trazado real
            // (grilla ancha) encuentra el asfalto real pero paso a paso, cada punto
            // por separado, tiene ruido -- pequeños zigzags que CarAutoDrive (que
            // apunta directo al waypoint que sigue) siguen tal cual, en vez de
            // promediarlos. Suavizado con un promedio de 3 puntos (cada punto pasa
            // a ser el promedio de sí mismo + sus 2 vecinos) antes de usarlo como
            // waypoints -- saca el zigzag sin perder la forma real de la curva.
            traced = SmoothPath(traced);
            Vector2 lastPoint = straightStart;
            if (traced.Count >= 5)
            {
                waypoints.AddRange(traced);
                lastPoint = traced[traced.Count - 1];
                Debug.Log($"<color=cyan>[CarBuilder] Ruta real trazada por asfalto (raycast+material): {traced.Count} puntos.</color>");
            }
            else
            {
                Debug.LogWarning("[CarBuilder] No encontré suficiente asfalto real por raycast -- uso línea recta desde el spawn.");
            }
            float remainingDist = Vector2.Distance(lastPoint, straightEnd);
            int remainingSteps = Mathf.Max(1, Mathf.CeilToInt(remainingDist / stepX));
            for (int i = 1; i <= remainingSteps; i++)
                waypoints.Add(Vector2.Lerp(lastPoint, straightEnd, i / (float)remainingSteps));
            // owner: "las paredes no van con la ruta" -- las paredes por tramo
            // quedaban mal alineadas (el trazado real tiene algo de ruido punto a
            // punto, y eso se nota mucho en paredes rectas entre puntos consecutivos).
            // Ya no hacen tanta falta: con la gravedad apagada + el auto pegado al
            // piso (CarController.FixedUpdate) alcanza para que no se caiga aunque
            // no haya paredes -- sacadas para no meter un problema de colisión nuevo.
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
            // owner: "no esta yendo a 40" -- este campo (y arriveRadius/steerGain/
            // slowdownDistance) solo toman el valor default de C# en el momento en que
            // Generate los agrega ACÁ; si el auto ya existía en la escena de una
            // generación anterior, recompilar el script NO actualiza el valor ya
            // guardado -- hace falta volver a Generar. Puesto EXPLÍCITO acá (en vez de
            // depender solo del default en CarAutoDrive.cs) para que quede claro que
            // cambiar este número siempre requiere Regenerar.
            auto.cruiseSpeedKmh = 50f; // owner: "pone el auto a 50 kmh"
            // owner: "puse play y no spawnie dentro del auto se fue sin mi" -- si esto
            // arranca activo ACÁ (Generate/bake), el auto empieza a manejar desde el
            // frame 1 de Play, ANTES de que OpeningDriveSequence termine de sentar al
            // jugador/perro (el glide de la cámara tarda enterDuration). Queda apagado
            // acá; OpeningDriveSequence lo prende recién después de sentar a los dos.
            auto.active = false;
            ctrl.autoPilot = false;

            return car;
        }

        // ── Reconstruir el opening-drive desde la geometría REAL de la escena ─────────
        // owner: "que el auto salga de la punta de la ruta nueva", y después "en el cruce
        // de las dos rutas se tira para la derecha; que siga hasta la YPF y recién ahí
        // doble y entre".
        //
        // El problema de fondo: el owner arma la ruta A MANO. No solo agregó una extensión
        // duplicando el asfalto ("PavedRoad_Surface (1)"), sino que también CORRIÓ todo el
        // corredor: el asfalto original quedó en Z=-143 y la YPF en (449, -71.3, escala 2).
        // Los waypoints que calcula Build() usan coordenadas de CÓDIGO (MapLayout.PavedRoute
        // en Z≈40, YpfStation sin mover), así que al pasar de la extensión (bien puesta) al
        // tramo original (movido) el auto se iba de la ruta.
        //
        // Solución robusta: acá, DESPUÉS de ApplySavedLayout (con todo ya en su lugar real),
        // reconstruimos TODA la ruta del auto desde la geometría de la escena, sin coordenadas
        // hardcodeadas — se adapta a donde el owner ponga las cosas (y haga Save Map Layout):
        //   1) Junta la línea central de TODAS las piezas de asfalto vivas (base + extensiones)
        //      como pieza.TransformPoint(centro de la ruta). El mesh del asfalto tiene sus
        //      vértices en espacio-mundo de la ruta (RoadsideBuilder), así que esto da la
        //      línea central exacta, ya movida/rotada.
        //   2) Ordena esos puntos por X mundo (la punta lejana primero, la YPF al final).
        //   3) Busca la YPF REAL en la escena y corta el recorrido cerca de ella; el auto
        //      sigue derecho por la ruta y recién ahí dobla y entra al lote.
        const float RoadY = 17.05f; // = MapLayout.RoadSurfaceHeight + lift (RoadsideBuilder)

        // Promedio de 3 puntos (el punto + sus 2 vecinos) para sacar zigzag del
        // trazado por raycast, sin perder la forma real de la curva. Los extremos
        // (primero/último) quedan tal cual -- no tienen 2 vecinos de cada lado.
        static System.Collections.Generic.List<Vector2> SmoothPath(System.Collections.Generic.List<Vector2> path)
        {
            if (path.Count < 3) return path;
            var smoothed = new System.Collections.Generic.List<Vector2> { path[0] };
            for (int i = 1; i < path.Count - 1; i++)
                smoothed.Add((path[i - 1] + path[i] + path[i + 1]) / 3f);
            smoothed.Add(path[path.Count - 1]);
            return smoothed;
        }

        // Camina desde 'start' hacia 'towardHint' buscando asfalto REAL con física
        // (raycast), no nombres de objeto. owner: la v1 (abanico angosto de rayos
        // en línea recta desde la posición actual) se perdía en curvas cerradas --
        // con un giro fuerte, el próximo tramo de asfalto puede no caer sobre
        // NINGÚN rayo que arranque derecho desde 'current'. Ahora en cada paso se
        // barre una GRILLA 2D ancha (24m a cada lado, cada 4m) alrededor del punto
        // esperado (current + dir*stepDist), no solo un abanico angular -- cubre
        // curvas de cualquier cerradura. Entre todos los puntos de asfalto real
        // encontrados en la grilla, se prefiere el que esté más ADELANTE (no
        // detrás) y más cerca de la distancia de un paso normal. Tolera hasta 20
        // pasos seguidos sin encontrar nada (huecos largos) antes de rendirse.
        static System.Collections.Generic.List<Vector2> TraceRoadByRaycast(Vector3 start, Vector2 towardHint, float stepDist, float maxDist)
        {
            var result = new System.Collections.Generic.List<Vector2> { new Vector2(start.x, start.z) };
            Vector3 current = start;
            Vector3 dir = new Vector3(towardHint.x - start.x, 0f, towardHint.y - start.z).normalized;
            float traveled = 0f;
            int stall = 0;
            const float gridRadius = 24f, gridStep = 4f;
            while (traveled < maxDist && stall < 20)
            {
                Vector3 predicted = current + dir * stepDist;
                Vector3 best = Vector3.zero; float bestScore = float.NegativeInfinity; bool found = false;
                for (float lx = -gridRadius; lx <= gridRadius; lx += gridStep)
                {
                    for (float lz = -gridRadius; lz <= gridRadius; lz += gridStep)
                    {
                        Vector3 origin = predicted + new Vector3(lx, 0f, lz) + Vector3.up * 60f;
                        if (!Physics.Raycast(origin, Vector3.down, out var hit, 150f)) continue;
                        if (!IsAsphalt(hit)) continue;
                        Vector3 toHit = hit.point - current;
                        float mag = toHit.magnitude;
                        if (mag < 0.5f) continue;
                        float forwardDot = Vector3.Dot(toHit / mag, dir);
                        if (forwardDot < 0.15f) continue; // descarta lo que queda atrás
                        float score = forwardDot * 20f - Mathf.Abs(mag - stepDist); // preferir adelante y cerca del paso normal
                        if (score > bestScore) { bestScore = score; best = hit.point; found = true; }
                    }
                }
                if (!found)
                {
                    stall++;
                    current += dir * stepDist;
                }
                else
                {
                    stall = 0;
                    Vector3 flat = new Vector3(best.x, 0f, best.z) - new Vector3(current.x, 0f, current.z);
                    if (flat.sqrMagnitude > 0.01f) dir = flat.normalized;
                    current = best;
                }
                traveled += stepDist;
                result.Add(new Vector2(current.x, current.z));
            }
            return result;
        }

        // ¿Hay asfalto de verdad en este impacto? Mira el material del renderer
        // (convención del proyecto: "mat_ypf_asphalt", "mat_tunnel_asphalt", etc.,
        // todos con "asphalt" en el nombre) o, si es Terrain, si la capa de
        // asfalto (índice 2, ver TerrainBuilder.PaintTextures) es la DOMINANTE ahí
        // -- no un umbral fijo (0.5), que perdía los bordes mezclados/en transición
        // entre capas.
        static bool IsAsphalt(RaycastHit hit)
        {
            var rend = hit.collider.GetComponent<Renderer>();
            if (rend != null)
            {
                foreach (var m in rend.sharedMaterials)
                    if (m != null && m.name.ToLower().Contains("asphalt")) return true;
            }
            var terrain = hit.collider.GetComponent<Terrain>();
            if (terrain != null)
            {
                var td = terrain.terrainData;
                Vector3 local = hit.point - terrain.transform.position;
                float nx = Mathf.Clamp01(local.x / td.size.x);
                float nz = Mathf.Clamp01(local.z / td.size.z);
                int mx = Mathf.Clamp(Mathf.RoundToInt(nx * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1);
                int mz = Mathf.Clamp(Mathf.RoundToInt(nz * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);
                var map = td.GetAlphamaps(mx, mz, 1, 1);
                int layers = map.GetLength(2);
                int dominant = 0; float dominantW = -1f;
                for (int i = 0; i < layers; i++)
                    if (map[0, 0, i] > dominantW) { dominantW = map[0, 0, i]; dominant = i; }
                if (dominant == 2) return true; // capa 2 = asfalto, la que más pesa acá
            }
            return false;
        }
        // Dónde entra el auto a la estación, respecto del centro de la YPF (ver nota abajo).
        static readonly Vector2 YpfEntryOffset = new Vector2(59.65f, -18.13f);
        const float ParkBesidePumpDist = 3.5f; // a cuántos metros del surtidor frena el auto (al lado, no encima)
        // owner: "el auto va por la izquierda, debería ir por la derecha" (Argentina = mano
        // derecha). Corre los waypoints de la ruta esta cantidad hacia la DERECHA de la
        // dirección de viaje. Negativo = izquierda (por si hay que invertir el lado).
        const float RightLaneOffset = 8f;
        // Cuántos metros ANTES de la entrada arranca el giro hacia la YPF. Este valor
        // controla dos cosas a la vez: CUÁNDO empieza a doblar (menos metros = dobla
        // más tarde) Y qué tan CERRADO es el giro (el mismo cambio de rumbo, ruta →
        // entrada, comprimido en menos distancia = giro más brusco).
        // Historial: 15 (cruzaba el asfalto muy al este, rozaba la barranca) -> 10
        // ("debería doblar 5 metros después") -> 0 (owner: "no está entrando derecho,
        // dobla 10m después y dobla más -- no está doblando tanto, se choca contra un
        // árbol la punta" -- con 10 el auto seguía derecho de más y el corte hacia la
        // entrada quedaba muy amplio/lento para el margen real que hay hasta el árbol).
        // En 0: derecho hasta la línea de la entrada y ahí sí, giro corto y cerrado.
        const float TurnBeforeEntry = 0f;

        public static void SnapToRoadExtensionTip(Transform mapRoot)
        {
            var car = mapRoot.Find("Renault12");
            if (car == null) return;

            // 1) Línea central de TODAS las piezas de asfalto (base "PavedRoad_Surface" +
            //    extensiones "PavedRoad_Surface (N)"), leída DIRECTO de los vértices de la
            //    malla real de cada pieza -- no de MapLayout.PavedRoute. Encontrado en
            //    vivo: reconstruir el centro con PavedRoute daba una línea corrida ~10m
            //    hacia la banquina (el trazado de MapLayout cambió con la extensión del
            //    mapa y ya no coincide con la malla que el compañero dejó colocada) -- el
            //    auto perseguía esa línea corrida, chocaba el borde y zigzagueaba.
            //    La malla (RoadsideBuilder.BuildPavedRoadMesh) se construye con 5
            //    vértices por sección transversal, y el índice i*5+1 de cada sección es
            //    EXACTAMENTE el centro de la ruta -- ese es el dato bueno, ya en el orden
            //    del recorrido (sin re-ordenar nada dentro de una pieza).
            var rxClone = new System.Text.RegularExpressions.Regex(@"^PavedRoad_Surface( \(\d+\))?$");
            var pieces = new System.Collections.Generic.List<(float tipX, System.Collections.Generic.List<Vector3> line)>();
            foreach (Transform child in mapRoot)
            {
                if (!rxClone.IsMatch(child.name)) continue;
                var mf = child.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var verts = mf.sharedMesh.vertices;
                var line = new System.Collections.Generic.List<Vector3>(verts.Length / 5 + 1);
                for (int i = 1; i < verts.Length; i += 5) // centro de cada sección
                    line.Add(child.TransformPoint(verts[i]));
                if (line.Count < 2) continue;
                // dentro de la pieza, ir de la PUNTA (X alto) hacia la YPF (X bajo)
                if (line[0].x < line[line.Count - 1].x) line.Reverse();
                pieces.Add((line[0].x, line));
            }
            var pts = new System.Collections.Generic.List<Vector3>();
            // 2) Piezas ordenadas por su punta (X alto primero: extensión → base); cada
            //    una ya viene en orden interno. NO se re-ordenan los puntos globalmente:
            //    un sort por X mezclaría puntos de piezas distintas si se solapan.
            pieces.Sort((a, b) => b.tipX.CompareTo(a.tipX));
            foreach (var pc in pieces) pts.AddRange(pc.line);
            if (pts.Count < 2) return; // sin asfalto en escena → no toco nada

            // 3) YPF REAL en la escena (el owner la movió/escaló). Fallback: posición de código.
            //    (Se busca ANTES del corrimiento de carril de abajo, porque el fade del
            //    corrimiento necesita saber dónde está el giro de entrada a la estación.)
            Transform ypf = FindDeep(mapRoot, "ML_009_EstacionYPF") ?? FindDeep(mapRoot, "EstacionYPF");
            Vector3 ypfPos = ypf != null
                ? ypf.position
                : new Vector3(MapLayout.YpfStation.x, RoadY, MapLayout.PavedRouteZAt(MapLayout.YpfStation.x));
            float turnX = ypfPos.x + YpfEntryOffset.x + TurnBeforeEntry; // donde arranca el giro hacia la YPF

            // 2b) MANO DERECHA (Argentina): correr la línea RightLaneOffset metros hacia
            //     la derecha de la dirección de viaje, punto por punto (RightOf respeta
            //     las curvas). El spawn, el yaw y los waypoints salen de esta línea ya
            //     corrida. Los puntos de entrada/estacionamiento de la YPF NO se corren
            //     (son lugares puntuales relativos a la estación, no carril).
            //     FADE cerca del giro: el corrimiento se desvanece en los últimos ~80m
            //     antes del giro de entrada, para que el auto vuelva al centro y salga
            //     del asfalto por la MISMA trayectoria diagonal que el owner calibró
            //     (entry/park). Con el corrimiento a full hasta el final, el auto salía
            //     del borde norte mucho antes y por otro punto -- ahí no hay rampa y
            //     quedaba ENCAJADO en la zanja entre el terraplén de la ruta y la
            //     barranca de tierra (confirmado con telemetría: posición congelada,
            //     APOYADO contra 'PavedRoad_Surface' y 'Terrain_Merged' a la vez,
            //     ruedas a 48 km/h).
            //     Los offsets se calculan TODOS antes de aplicar ninguno, para que
            //     RightOf lea la línea original y no una mezcla corrida/sin correr.
            if (Mathf.Abs(RightLaneOffset) > 0.001f)
            {
                const float fadeLen = 80f;
                var shifted = new System.Collections.Generic.List<Vector3>(pts.Count);
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 r = RightOf(pts, i);
                    float k = Mathf.Clamp01((pts[i].x - turnX) / fadeLen); // 1 en ruta abierta, 0 en el giro
                    shifted.Add(pts[i] + new Vector3(r.x, 0f, r.y) * (RightLaneOffset * k));
                }
                pts = shifted;
            }

            // Spawn ~15m adentro de la punta, mirando hacia la YPF.
            Vector3 spawn = pts[0];
            int spawnIdx = 0;
            float acc = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += Vector3.Distance(pts[i - 1], pts[i]);
                spawn = pts[i]; spawnIdx = i;
                if (acc >= 15f) break;
            }
            Vector3 fwd = pts[Mathf.Min(spawnIdx + 1, pts.Count - 1)] - spawn; fwd.y = 0f;
            float yaw = fwd.sqrMagnitude > 0.0001f ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg : car.eulerAngles.y;
            car.position = spawn + Vector3.up * 0.05f;
            car.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Punto de ENTRADA a la estación, RELATIVO al centro de la YPF. El owner lo marcó
            // una vez con el TEST_PLAYER en (508.65,-89.43), con la YPF en (449,-71.3) →
            // offset (+59.65,-18.13). Se guarda relativo para que siga a la estación si la
            // mueve. OJO: NO se lee del TEST_PLAYER vivo (ese objeto es el SPAWN del jugador y
            // el owner lo mueve por otros motivos — al ponerlo al inicio del mapa el auto se
            // iba hasta los árboles). Si querés correr dónde entra el auto, tocá este offset.
            Vector2 entry = new Vector2(ypfPos.x + YpfEntryOffset.x, ypfPos.z + YpfEntryOffset.y);

            // Punto FINAL: al lado de un surtidor. Buscamos surtidores DENTRO de la estación
            // (cualquier objeto con "pump"/"surtidor"/"dispenser" en el nombre; posición viva
            // ya movida/escalada), tomamos el más cercano a la entrada y paramos a
            // ParkBesidePumpDist de él, del lado por el que llega el auto (no encima — la
            // estación tiene colliders). Si NO encontramos ninguno (o falta el modelo), igual
            // avanzamos hacia el centro de la estación para no frenar en el borde de la tierra.
            Vector2 ypfXZ = new Vector2(ypfPos.x, ypfPos.z);
            Transform searchRoot = ypf != null ? ypf : mapRoot;
            Vector2 bestPump = Vector2.zero; float bestPumpD = float.MaxValue; int pumpCount = 0;
            foreach (var t in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!(n.Contains("pump") || n.Contains("surtidor") || n.Contains("dispenser"))) continue;
                pumpCount++;
                Vector2 pxz = new Vector2(t.position.x, t.position.z);
                float d = (pxz - entry).sqrMagnitude;
                if (d < bestPumpD) { bestPumpD = d; bestPump = pxz; }
            }
            Vector2 park;
            if (pumpCount > 0)
            {
                Vector2 toEntry = entry - bestPump;
                Vector2 dir = toEntry.sqrMagnitude > 0.01f ? toEntry.normalized : Vector2.zero;
                park = bestPump + dir * ParkBesidePumpDist; // frena al lado del surtidor
            }
            else
            {
                // sin surtidores detectados: avanzar hacia el centro de la estación
                Vector2 toCenter = ypfXZ - entry;
                park = entry + (toCenter.sqrMagnitude > 1f ? toCenter.normalized : Vector2.zero) * 22f;
            }

            // Arranca el giro TurnBeforeEntry metros ANTES (lado este) del punto de
            // entrada, para doblar con avance y no en seco. pts va con X descendente.
            int turnIdx = spawnIdx;
            for (int i = spawnIdx; i < pts.Count; i++)
                if (pts[i].x >= entry.x + TurnBeforeEntry) turnIdx = i;

            // 4) Waypoints: recto por la ruta hasta el giro, dobla en la entrada y sigue un
            //    poco más hasta quedar al lado del surtidor.
            var auto = car.GetComponent<FolkloreArchives.CarAutoDrive>();
            if (auto != null)
            {
                var wps = new System.Collections.Generic.List<Vector2>();
                for (int i = spawnIdx; i <= turnIdx; i++) wps.Add(new Vector2(pts[i].x, pts[i].z));
                wps.Add(entry);                                            // dobla y entra
                if ((park - entry).sqrMagnitude > 0.25f) wps.Add(park);    // sigue hasta el surtidor
                auto.waypoints = wps.ToArray();
            }

            Debug.Log($"<color=lime>[CarBuilder] Opening-drive reconstruido desde la escena: {pts.Count} puntos de asfalto, " +
                      $"spawn en la punta (X≈{pts[0].x:0}), entra en ({entry.x:0},{entry.y:0}), " +
                      $"para en ({park.x:0},{park.y:0}) — {pumpCount} surtidores detectados.</color>");
        }

        // Centro de la ruta en el espacio LOCAL del mesh del asfalto (= espacio-mundo de la
        // ruta original): PavedRoute[i] ya es el centro (x, z=PavedRouteZAt(x)).
        static Vector3 CenterLocal(Vector2 routePt) => new Vector3(routePt.x, RoadY, routePt.y);

        // Vector unitario "a la derecha" de la dirección de viaje en el punto i (plano XZ).
        // Para forward (fx,fz), derecha = (fz,-fx) — regla de la mano derecha de Unity
        // (right = up × forward), la mano correcta para tránsito por derecha.
        static Vector2 RightOf(System.Collections.Generic.List<Vector3> pts, int i)
        {
            Vector3 a = pts[Mathf.Max(0, i - 1)];
            Vector3 b = pts[Mathf.Min(pts.Count - 1, i + 1)];
            Vector2 fwd = new Vector2(b.x - a.x, b.z - a.z);
            if (fwd.sqrMagnitude < 1e-4f) return Vector2.zero;
            fwd.Normalize();
            return new Vector2(fwd.y, -fwd.x);
        }

        // Búsqueda recursiva por nombre bajo un Transform (incluye inactivos).
        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
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
