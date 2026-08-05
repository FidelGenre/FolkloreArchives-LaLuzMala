// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarAutoDrive.cs — owner: "vamos todos en el auto desde el inicio de
//  mapa hasta la gasolinera" -- el auto maneja SOLO (sin input) siguiendo
//  una lista de puntos XZ horneada por CarBuilder.cs (sigue la curva real
//  de la ruta pavimentada, ver MapLayout.PavedRouteZAt -- esa función es
//  editor-only, así que la ruta se samplea UNA VEZ en Generate y se
//  guarda acá como datos simples).
//  No toca la física directo: solo escribe CarController.externalThrottle/
//  externalSteer, mismo camino que ya usa el manejo con teclado.
//  Valores (cruiseThrottle/steerGain/arriveRadius) son una primera
//  estimación -- necesitan ajuste en vivo (Play) como todo lo demás del
//  auto en este proyecto.
// ============================================================
using UnityEngine;

namespace FolkloreArchives
{
    public class CarAutoDrive : MonoBehaviour
    {
        public Vector2[] waypoints;      // puntos XZ, en orden, horneados por CarBuilder
        // owner: "se pone a girar" al llegar -- con el waypoint muy cerca, la
        // DIRECCIÓN hacia él se vuelve muy ruidosa (un pasito de más y el ángulo salta
        // 180°), y el steer clampeado a ±1 lo hacía girar en el lugar tratando de
        // corregir sin parar. Radio más generoso para no perseguir un punto tan
        // puntual.
        public float arriveRadius = 8f;  // qué tan cerca hay que estar de un waypoint para pasar al siguiente
        // owner: "de camino ir mas lento... sigue igual" -- bajar cruiseThrottle (0.55
        // -> 0.4) no cambió NADA la velocidad real porque CarController.Update() no
        // usa el VALOR del throttle para nada, solo su signo: `if (throttle > 0.1f)
        // speed = MoveTowards(speed, maxSpeed, accel*dt)` -- acelera a fondo hacia
        // maxSpeed sin importar si el throttle es 0.11 o 1.0. Frenar el crucero de
        // verdad necesita que ESTE script decida cuándo CORTAR el acelerador (no solo
        // bajar un número que CarController ignora) -- reemplazado por un control de
        // velocidad objetivo: acelera mientras esté por debajo de cruiseSpeedKmh,
        // corta el acelerador al alcanzarlo (mismo patrón que ya usaba la frenada en
        // el lote, ahora aplicado a TODO el trayecto).
        public float cruiseSpeedKmh = 50f;
        // owner: subido a 2.5 en un intento anterior de arreglar "no sigue la
        // ruta" -- resultó ser el diagnóstico equivocado (la causa real era que
        // SnapToRoadExtensionTip corría en el orden incorrecto en MapGenerator.cs
        // y el auto terminaba usando una ruta de fallback de 21 puntos, mucho más
        // tosca). Revertido a 1 -- con la ruta real (muchos más puntos, ya
        // ordenada correctamente) este valor base debería alcanzar.
        public float steerGain = 1f;
        // owner: "al llegar a la ypf no frena el auto choca" -- el frenado solo miraba
        // la distancia del ÚLTIMO tramo (waypoint a waypoint), pero el giro hacia
        // adentro del lote de la YPF agrega un tramo final CORTO -- el auto llegaba a
        // ese tramo todavía a velocidad crucero, sin espacio para frenar a tiempo.
        // Ahora slowdownDistance se mide contra la distancia TOTAL restante (sumando
        // TODOS los tramos que faltan, no solo el actual).
        // owner: "deberia... frenarse antes" -- más metros de margen para empezar a
        // soltar velocidad antes del giro/estacionamiento.
        public float slowdownDistance = 45f; // frena suave en los últimos metros antes del último waypoint
        // owner: "mientras más avanza al entrar más vaya mirando para la derecha, así
        // entra bien en la curva" -- cuántos metros se corre el punto apuntado hacia
        // LA DERECHA DEL AUTO (no un punto horneado fijo) mientras se acerca a la
        // entrada de la YPF. Ver 'aimingEntryGate' más abajo.
        public float entryRightBias = 6f;

        // owner: "necesito que estacione al lado del pump mirando para adelante,
        // dejando el tanque para cargar, así como ese otro auto" -- orientación
        // final al llegar (grados, mundo). La pone CarBuilder desde la rotación
        // real del surtidor elegido. Se aplica girando EN EL LUGAR recién al
        // llegar (mientras maneja no se toca -- el steering normal ya hace su
        // trabajo, esto es solo el acomodo final).
        [HideInInspector] public bool hasFinalYaw;
        [HideInInspector] public float finalYaw;
        public float finalYawSpeed = 120f; // grados/seg del acomodo final

        public bool active;
        public bool HasArrived { get; private set; }

        // owner: "necesito que sea más suave... metiendo volantazos todo" -- cuánto
        // puede cambiar externalSteer por segundo (unidades de steer/seg). steer
        // llega a pedir hasta ±steerGain*3 (zona cerca del destino) -- con este
        // límite, ir de un extremo al otro toma ~1s en vez de ser instantáneo.
        public float steerRateLimit = 6f;

        CarController car;
        int _index;
        float _smoothedSteer;

        void Awake() => car = GetComponent<CarController>();

        void Update()
        {
            if (HasArrived)
            {
                // Acomodo final: ya frenado, gira EN EL LUGAR hasta la orientación que
                // marcó CarBuilder (la del surtidor elegido) -- el steering normal no
                // sirve acá (CarController solo gira con el auto en movimiento, ver
                // FixedUpdate). Nada que hacer si no hay orientación objetivo.
                if (hasFinalYaw)
                {
                    float cur = transform.eulerAngles.y;
                    float next = Mathf.MoveTowardsAngle(cur, finalYaw, finalYawSpeed * Time.deltaTime);
                    transform.rotation = Quaternion.Euler(0f, next, 0f);
                }
                return;
            }
            if (!active || waypoints == null || waypoints.Length == 0 || car == null) return;

            Vector3 p = transform.position;
            Vector2 target = waypoints[_index];
            float dist = Vector2.Distance(new Vector2(p.x, p.z), target);

            // owner: "sigue yendose de largo... no frena nunca" -- el fix de "se pone a
            // girar" (más abajo) hace que el auto apunte al SIGUIENTE waypoint apenas
            // se acerca al actual, así que puede terminar pasando de largo un waypoint
            // sin nunca entrar en su arriveRadius (corta camino, "corner cutting" -- el
            // giro hacia el siguiente empieza antes de cerrar la distancia al actual).
            // Con solo la condición de distancia, _index se quedaba trabado ahí para
            // siempre -- nunca llegaba a la zona de frenado (inLotZone mira _index, no
            // la posición real).
            // owner (2da vuelta, "se esta quedando trabado nuevamente"): el primer
            // intento de este fix usaba transform.forward (hacia dónde apunta el
            // MORRO del auto AHORA) -- con el radio de anticipación agrandado después
            // (4x arriveRadius) el auto empieza a curvar hacia el siguiente punto
            // TAN pronto que el waypoint actual puede terminar bien al COSTADO en vez
            // de atrás, y el morro (que gira siguiendo el volante) puede seguir
            // "mirando" hacia adelante de él sin que el producto punto se vuelva
            // negativo nunca -- se quedaba trabado otra vez. Cambiado a un criterio
            // que no depende de hacia dónde mira el auto: proyección sobre la
            // dirección del TRAMO de ruta (waypoint anterior → actual) -- "pasado" si
            // el auto, medido a lo largo de esa línea, ya dejó atrás al punto, sin
            // importar el ángulo del volante en este instante.
            Vector2 segFrom = _index > 0 ? waypoints[_index - 1] : new Vector2(p.x, p.z);
            Vector3 segDir = new Vector3(target.x - segFrom.x, 0f, target.y - segFrom.y);
            Vector3 toTargetNow = new Vector3(target.x - p.x, 0f, target.y - p.z);
            bool passedWaypoint = segDir.sqrMagnitude > 1f && Vector3.Dot(toTargetNow, segDir) < 0f;
            // La ENTRADA a la YPF (anteúltimo waypoint) es una PUERTA calibrada a mano:
            // el cruce del borde del asfalto hacia la tierra tiene rampa SOLO ahí. El
            // corner-cutting normal (llegar "cerca" y ya doblar hacia el siguiente
            // punto) hacía que el auto saliera del asfalto 12-20m antes del cruce
            // calibrado y quedara ENCAJADO contra la barranca (telemetría: APOYADO
            // contra 'PavedRoad_Surface' + 'Terrain_Merged' a la vez, ruedas a 48).
            // Para ese waypoint: radio de llegada a la mitad y sin pre-apuntar al
            // siguiente (más abajo) -- hay que pasar por la puerta, no cortar camino.
            bool isEntryGate = waypoints.Length >= 2 && _index == waypoints.Length - 2;
            float arrive = isEntryGate ? arriveRadius * 0.5f : arriveRadius;
            if (dist < arrive || passedWaypoint)
            {
                _index++;
                if (_index >= waypoints.Length)
                {
                    HasArrived = true;
                    active = false;
                    car.externalThrottle = 0f;
                    car.externalSteer = 0f;
                    return;
                }
                target = waypoints[_index];
                dist = Vector2.Distance(new Vector2(p.x, p.z), target);
            }

            // distancia TOTAL restante hasta el ÚLTIMO waypoint (el tramo actual + la
            // suma de los que faltan), no solo el tramo actual -- así un tramo final
            // corto (como el giro hacia el lote de la YPF) no deja al auto sin
            // espacio para frenar.
            float remaining = dist;
            for (int j = _index; j < waypoints.Length - 1; j++)
                remaining += Vector2.Distance(waypoints[j], waypoints[j + 1]);

            // owner: "necesito que vaya a 40kmh y ahora se esta trabando de nuevo
            // contra la entrada de la ypf" -- la zona de frenado ahora incluye también
            // el waypoint de GIRO (últimos 3: giro + entrada real + estacionar), no
            // solo los últimos 2 -- empieza a soltar velocidad ANTES del giro cerrado.
            // Calculado ACÁ (antes del steer) porque el siguiente fix también lo usa.
            bool inLotZone = _index >= waypoints.Length - 3;

            // owner: "el auto sigue cayendose/saliendose en la curva" -- con la ruta
            // ahora trazada desde la malla real (TraceRoadPath en CarBuilder), puede
            // haber curvas cerradas en CUALQUIER parte del camino, no solo cerca de
            // la YPF (antes SOLO inLotZone, los últimos 3 waypoints, tenía más fuerza
            // de giro). Generalizado: mirar el ángulo entre el tramo que estamos
            // por terminar y el siguiente -- si es cerrado, tratar esta curva con el
            // mismo criterio que el giro de la YPF (más steerGain), pero con un TOPE
            // de velocidad en vez de un frenado a fondo (no es el destino final, solo
            // hay que pasar la curva más despacio y seguir de largo después).
            Vector2 prevPoint = _index > 0 ? waypoints[_index - 1] : new Vector2(p.x, p.z);
            bool sharpTurnAhead = false;
            if (_index + 1 < waypoints.Length)
            {
                Vector2 segIn = target - prevPoint;
                Vector2 segOut = waypoints[_index + 1] - target;
                if (segIn.sqrMagnitude > 0.01f && segOut.sqrMagnitude > 0.01f)
                    sharpTurnAhead = Vector2.Angle(segIn, segOut) > 35f;
            }

            // owner (2da vuelta): "se sigue quedando trabado ahora si va a 40" --
            // CarController.FixedUpdate() escala la capacidad de GIRO con la
            // velocidad (`turnRate * Clamp01(speed/maxSpeed)`): a velocidad CERO no
            // gira nada. Frenar antes del giro cerrado (fix anterior) reduce la
            // velocidad para no pasarse de largo, pero ESO MISMO le saca autoridad de
            // giro justo cuando más la necesita -- frenar de más y no poder doblar son
            // la MISMA perilla, tirando para lados opuestos. Solución: no pelear más
            // con la velocidad, compensar con más steerGain SOLO dentro del lote --
            // así el auto puede cerrar el giro aunque venga más lento.
            // owner: "se sigue yendo para el costado, no vuelve" -- si el auto ya
            // no está sobre asfalto de verdad, corrección EN VIVO con prioridad
            // sobre lo que decía el waypoint horneado (ver más abajo, en el steer):
            // se busca el asfalto más cercano y se vira fuerte hacia ahí, no importa
            // qué tan mal esté el dato pre-horneado para este tramo.
            // owner: "al entrar a la gasolinera se queda trabado andando" -- DENTRO
            // del lote de la YPF el auto se mete a propósito en el playón/tierra
            // junto al surtidor (SnapToRoadExtensionTip, en CarBuilder), que no
            // siempre cuenta como "asfalto" para IsOnAsphalt -- ahí este sistema de
            // rescate lo tironeaba de vuelta hacia la ruta principal MIENTRAS el
            // sistema de estacionamiento lo llevaba al surtidor. El primer intento
            // apagaba el rescate solo con inLotZone (últimos 3 waypoints por
            // ÍNDICE) -- owner: "se tranca cuando pasa el asfalto... e ingresa a la
            // ruta [del lote]", el auto se sale del asfalto un poco ANTES de llegar
            // a esos waypoints exactos (la transición asfalto→playón no coincide
            // con el índice del waypoint). Cambiado a distancia REAL restante
            // (`remaining`, ya calculado arriba) -- cubre toda la zona final con
            // margen, sin depender de cuántos waypoints exactos falten.
            bool nearDestination = inLotZone || remaining < slowdownDistance;
            bool rescuing = !nearDestination && !IsOnAsphalt(p);
            float lotSteerGain = (nearDestination || sharpTurnAhead || rescuing) ? steerGain * 3f : steerGain;
            // owner: "al querer ingresar mete un giro 360" -- el intento anterior
            // (lotSteerGain*6 en la puerta de entrada) SUMADO al corrimiento del
            // aimPoint (entryRightBias, más abajo) hacía que el ángulo objetivo fuera
            // grande Y la ganancia también -- la telemetría mostró steer=6.00 (tope
            // del clamp ±1 por ganancia 6), que a 48km/h da ~320°/s de giro: una
            // vuelta entera en poco más de 1 segundo. Sacado el multiplicador extra
            // acá -- el giro "mira más a la derecha" ahora lo da SOLO el corrimiento
            // progresivo del aimPoint (entryRightBias) sobre la ganancia normal de
            // zona (steerGain*3, la misma que ya usa cualquier tramo cerca del
            // destino), sin apilar los dos efectos.

            // owner: "se pone a girar" -- MUY cerca de un waypoint, la dirección hacia
            // ÉSE punto se vuelve ruidosísima (un paso más y el ángulo salta 180°),
            // steer clampeado a ±1 lo hacía girar en el lugar. El fix anterior solo
            // apagaba el steer cerca del ÚLTIMO waypoint (finalApproach) -- ahí es
            // correcto porque ya no hay a dónde girar, solo frenar derecho. Pero con
            // los waypoints de giro NUEVOS hacia el lote (waypoints intermedios, no el
            // último) apagar el steer ahí hacía que el auto soltara el volante
            // JUSTO en medio del giro y siguiera de largo sin doblar -- owner: "sigue
            // de largo y atraviesa todo" / "se pone a girar". Fix general (estilo
            // "pure pursuit"): cerca de un waypoint que NO es el último, mirar hacia
            // el SIGUIENTE de una vez (ya vamos para allá) en vez de fijar la mirada en
            // el punto que estamos a punto de pasar -- da un ángulo estable en vez de
            // ruidoso. Solo cerca del waypoint FINAL (sin "siguiente" al cual mirar) se
            // suelta el volante del todo.
            // owner: "dobla muy tarde deberia doblar antes" -- mirar al siguiente punto
            // recién a 1.5x arriveRadius (12m) daba muy poco margen para acomodar el
            // rumbo antes de un giro cerrado como el de entrada al lote. Radio de
            // anticipación aparte (más grande) solo para ESTO -- el radio chico
            // (arriveRadius*1.5) se guarda nomás para soltar el volante del todo cerca
            // del waypoint FINAL (ahí sí hace falta estar bien cerca, es donde frena).
            // owner: "dobla muy despues... se sigue trabando" -- agrandar este radio a
            // 4x para que doble antes resultó frágil (cuanto más grande, más fácil que
            // el auto cortara camino y el índice de ruta quedara trabado, ver más
            // arriba). Vuelto a 2.5x -- estable. La geometría del giro en sí la maneja
            // CarBuilder (owner pidió que sea cerca de la estación, 5m); lo que hace
            // que un giro tan cerrado sea completable ahora es la velocidad de crucero
            // más baja (cruiseSpeedKmh), no un lookahead de runtime agresivo.
            bool isLastWaypoint = _index == waypoints.Length - 1;
            // Puerta de entrada a la YPF (ver isEntryGate arriba, recalculado acá por
            // si _index avanzó): NO pre-apuntar al estacionamiento mientras el target
            // es la entrada -- hay que cruzar el borde por el punto calibrado exacto.
            bool aimingEntryGate = waypoints.Length >= 2 && _index == waypoints.Length - 2;
            bool nearForAim = dist < arriveRadius * 2.5f && !aimingEntryGate;
            bool nearForStop = dist < arriveRadius * 1.5f;
            Vector2 aim = target;
            if (nearForAim && !isLastWaypoint) aim = waypoints[_index + 1];

            float steer = 0f;
            if (!(nearForStop && isLastWaypoint))
            {
                // Fuera de asfalto de verdad (ver 'rescuing' arriba): en vez del
                // waypoint horneado, apuntar directo al asfalto real más cercano.
                Vector3 aimPoint = new Vector3(aim.x, p.y, aim.y);
                if (rescuing && FindNearestAsphalt(p, transform.forward, out Vector3 rescue))
                    aimPoint = rescue;
                else if (aimingEntryGate)
                {
                    // owner: "mientras más avanza al entrar más vaya mirando para la
                    // derecha" -- se probó una curva horneada (Bézier en CarBuilder)
                    // con puntos intermedios corridos a la derecha, pero chocaba
                    // contra el terreno/borde real (la curva no sabe qué hay ahí
                    // físicamente). Esto en cambio corre el punto apuntado una
                    // cantidad que CRECE a medida que se acerca a la entrada.
                    // owner: "se esta chocando el auto metiendo volantazos todo" --
                    // el primer intento corría el punto hacia transform.right (la
                    // derecha DEL AUTO), que cambia cada frame MIENTRAS el auto gira
                    // en respuesta a ese mismo corrimiento -- lazo de realimentación
                    // (gira → el punto se corre porque "derecha" cambió → gira más →
                    // se corre más...), el volantazo en cadena. Ahora la dirección es
                    // FIJA: la del tramo de ruta (waypoint anterior → entrada), que no
                    // depende del rumbo del auto -- sigue "abriendo" la curva hacia la
                    // derecha, pero de forma estable, sin retroalimentarse.
                    Vector2 prevWp = _index > 0 ? waypoints[_index - 1] : new Vector2(p.x, p.z);
                    Vector2 segToEntry = target - prevWp;
                    Vector3 stableRight = segToEntry.sqrMagnitude > 0.01f
                        ? new Vector3(segToEntry.y, 0f, -segToEntry.x).normalized
                        : transform.right;
                    float closeness = 1f - Mathf.Clamp01(dist / (arriveRadius * 5f)); // 0 lejos -> 1 cerca
                    aimPoint += stableRight * (entryRightBias * closeness);
                }
                Vector3 toTarget = aimPoint - p; toTarget.y = 0f;
                float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
                steer = Mathf.Clamp(angle / 45f, -1f, 1f) * lotSteerGain;
            }

            // owner: "todo el trayecto... vaya mas lento" -- velocidad objetivo: la de
            // crucero en la ruta abierta, o la que va bajando (tapering) cerca del
            // final dentro de la zona de frenado -- la que sea más chica de las dos
            // (por si cruiseSpeedKmh ya es menor que lo que pediría el tapering).
            float cruiseSpeedMs = cruiseSpeedKmh / 3.6f;
            float currentSpeed = car.SpeedKmh / 3.6f;
            float targetSpeed = cruiseSpeedMs;
            bool braking = false;
            if (inLotZone && remaining < slowdownDistance)
            {
                targetSpeed = Mathf.Min(cruiseSpeedMs, car.maxSpeed * Mathf.Clamp01(remaining / slowdownDistance));
                braking = true;
            }
            // Curva cerrada en medio de la ruta (no el destino final): no hay que
            // frenar a fondo, solo topear la velocidad mientras dura el giro -- vuelve
            // a crucero normal solo después de pasarla.
            else if (sharpTurnAhead)
            {
                targetSpeed = Mathf.Min(targetSpeed, 14f / 3.6f); // ~14 km/h por la curva
                braking = true;
            }
            // owner (3ra vuelta): "sigue trabandose" -- el steerGain triplicado (fix
            // anterior) no alcanza si la velocidad misma cae por debajo de 0.3 m/s:
            // CarController.FixedUpdate() ni siquiera INTENTA girar bajo ese umbral
            // (`if (Mathf.Abs(speed) > 0.3f)`), sin importar cuánto steer se le mande.
            // El tapering de `targetSpeed` de arriba baja hacia CERO a medida que
            // `remaining` se achica -- pero `remaining` se achica ANTES de terminar el
            // giro (mide distancia hasta el final, no si ya se enderezó), así que el
            // auto podía quedarse casi sin velocidad todavía a mitad del giro, mucho
            // antes de necesitarlo. Piso de velocidad mientras todavía queda ALGÚN
            // waypoint por delante (no el último, que es el que sí frena a fondo):
            // no lo deja caer por debajo de lo necesario para retener autoridad de giro.
            if (braking && !isLastWaypoint)
                targetSpeed = Mathf.Max(targetSpeed, 4f); // ~14 km/h, piso para poder girar

            // owner: "no frena el auto choca" -- soltar el acelerador solo desacelera
            // con coastDecel (suave); adentro de la zona de frenado hay que FRENAR de
            // verdad (throttle negativo → CarController usa brakeDecel, mucho más
            // fuerte) si la velocidad actual supera lo que "debería" tener a esta
            // distancia del final. Fuera de la zona de frenado (en la ruta abierta),
            // alcanza con CORTAR el acelerador (throttle=0 → coastDecel, suave) para
            // no pasarse de cruiseSpeedKmh -- no hace falta frenar activo ahí.
            float throttle;
            if (currentSpeed > targetSpeed + 0.5f)
                throttle = braking ? -0.6f : 0f;
            else if (braking && remaining < arriveRadius)
                // owner: "no frena del todo el auto se queda trancado andando" -- un
                // empuje chiquito acá nunca llegaba a CERO, así que tan cerca del punto
                // final el auto seguía reptando para siempre sin nunca entrar en el
                // radio de "llegada". Sin acelerador ahí, frena solo por resistencia
                // (coastDecel) hasta pararse de verdad.
                throttle = 0f;
            else if (currentSpeed < targetSpeed - 0.5f)
                throttle = 1f; // acelerar hacia la velocidad objetivo
            else
                throttle = 0f; // ya en la velocidad objetivo -- no acelerar más

            car.autoPilot = true;
            car.externalThrottle = throttle;

            // owner: "necesito que sea más suave... metiendo volantazos todo" -- el
            // ángulo objetivo (steer, arriba) puede saltar de golpe frame a frame:
            // un bache/roce contra el borde del asfalto empuja el auto y el ángulo
            // hacia el target cambia brusco, o el aim se recalcula distinto al
            // cruzar de un waypoint a otro. Antes eso iba DIRECTO al volante. Ahora
            // se limita cuánto puede cambiar el volante por segundo (steerRateLimit)
            // -- por más que el rumbo "correcto" cambie de un salto, el volante lo
            // sigue gradual, no de un tirón. Amortigua tanto los golpes físicos como
            // los recálculos de rumbo, sin importar la causa puntual de cada uno.
            _smoothedSteer = Mathf.MoveTowards(_smoothedSteer, steer, steerRateLimit * Time.deltaTime);
            car.externalSteer = _smoothedSteer;

            // TELEMETRÍA temporal (2da vez): ahora para diagnosticar el trancazo
            // entrando a la YPF. Mismo formato que la vez del zigzag. Sacar cuando
            // la entrada + estacionamiento anden bien.
            _dbgTimer += Time.deltaTime;
            if (_dbgTimer >= 0.5f)
            {
                _dbgTimer = 0f;
                Debug.Log($"[AUTO] pos=({p.x:0.0},{p.z:0.0}) wp={_index}/{waypoints.Length} " +
                          $"target=({target.x:0.0},{target.y:0.0}) dist={dist:0.0} vel={car.SpeedKmh:0.0}km/h " +
                          $"steer={steer:0.00} thr={throttle:0.00} sharp={sharpTurnAhead} resc={rescuing} " +
                          $"nearDest={nearDestination} lot={inLotZone} rem={remaining:0}");
            }
        }

        float _dbgTimer;

        // Choques con nombre y lugar exactos (parte de la telemetría).
        void OnCollisionEnter(Collision c)
        {
            if (!active || c.contactCount == 0) return;
            var ct = c.GetContact(0).point;
            Debug.Log($"<color=orange>[AUTO] CHOQUE contra '{c.collider.name}' en ({ct.x:0.0},{ct.z:0.0})</color>");
        }
        void OnCollisionStay(Collision c)
        {
            if (!active || c.contactCount == 0) return;
            _stayTimer += Time.fixedDeltaTime;
            if (_stayTimer < 1f) return; // loguear solo contacto SOSTENIDO (1x/seg aprox)
            _stayTimer = 0f;
            var ct = c.GetContact(0).point;
            Debug.Log($"<color=red>[AUTO] APOYADO contra '{c.collider.name}' en ({ct.x:0.0},{ct.z:0.0}) vel={car.SpeedKmh:0.0}km/h</color>");
        }
        float _stayTimer;

        // ¿Hay asfalto de verdad bajo el auto ahora mismo? Raycast hacia abajo,
        // mismo criterio que CarBuilder.IsAsphalt (Editor-time) pero en runtime.
        // OJO (encontrado con la telemetría, resc=True en TODAS las líneas): el rayo
        // sale de 2m ARRIBA del auto hacia abajo -- con Physics.Raycast simple lo
        // primero que tocaba era el TECHO del propio auto, nunca el asfalto, así que
        // esto devolvía false SIEMPRE y el auto viajaba todo el trayecto en modo
        // "rescate" (aim por FindNearestAsphalt + steerGain*3) en vez de seguir sus
        // waypoints -- el zigzag de la punta era ese sistema eligiendo puntos raros.
        // Igual que CarController.FixedUpdate: RaycastAll + ignorar los colliders
        // propios (y triggers de historia, que también flotan sobre la ruta).
        bool IsOnAsphalt(Vector3 worldPos)
        {
            var hits = Physics.RaycastAll(worldPos + Vector3.up * 2f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
            bool found = false; float bestD = float.MaxValue; RaycastHit best = default;
            foreach (var h in hits)
            {
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
                if (h.distance < bestD) { bestD = h.distance; best = h; found = true; }
            }
            return found && HitIsAsphalt(best);
        }

        static bool HitIsAsphalt(RaycastHit hit)
        {
            // La ruta REAL que arma el compañero a mano (PavedRoad_Surface, y sus
            // extensiones "PavedRoad_Surface (N)") no tiene por qué tener "asphalt"
            // en el nombre de su material -- mismo criterio de nombre que ya usa el
            // resto del código (CarBuilder.SnapToRoadExtensionTip, MapGenerator) para
            // identificarla. Sin esto, el auto arrancaba "rescuing" (steering
            // agresivo buscando asfalto) apenas empezaba a manejar, parado encima
            // de la ruta real, porque el chequeo de material no la reconocía.
            if (hit.collider.transform.name.StartsWith("PavedRoad_Surface")) return true;

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
                if (dominant == 2) return true;
            }
            return false;
        }

        // Busca el asfalto real más cercano en un anillo de rayos alrededor de
        // 'from' (16 direcciones, 15m de radio) -- barato (16 raycasts), pensado
        // para correr todos los frames que el auto esté fuera del asfalto (que
        // debería ser la excepción, no la regla). Devuelve el punto encontrado
        // más cercano a 'from', priorizando los que caen hacia 'preferDir' (el
        // frente actual del auto) para no invertir la marcha de golpe.
        static bool FindNearestAsphalt(Vector3 from, Vector3 preferDir, out Vector3 result)
        {
            result = Vector3.zero;
            float bestScore = float.NegativeInfinity;
            bool found = false;
            const float radius = 15f;
            for (int i = 0; i < 16; i++)
            {
                float ang = i * (360f / 16);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                Vector3 testPos = from + dir * radius;
                if (!Physics.Raycast(testPos + Vector3.up * 30f, Vector3.down, out var hit, 80f)) continue;
                if (!HitIsAsphalt(hit)) continue;
                float forwardDot = Vector3.Dot(dir, preferDir.normalized);
                float score = forwardDot; // entre los que sirven, preferir los que quedan adelante
                if (score > bestScore) { bestScore = score; result = hit.point; found = true; }
            }
            return found;
        }
    }
}
