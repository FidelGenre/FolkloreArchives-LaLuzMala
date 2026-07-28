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
        public float cruiseSpeedKmh = 40f;
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

        public bool active;
        public bool HasArrived { get; private set; }

        CarController car;
        int _index;

        void Awake() => car = GetComponent<CarController>();

        void Update()
        {
            if (!active || HasArrived || waypoints == null || waypoints.Length == 0 || car == null) return;

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
            if (dist < arriveRadius || passedWaypoint)
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

            // owner (2da vuelta): "se sigue quedando trabado ahora si va a 40" --
            // CarController.FixedUpdate() escala la capacidad de GIRO con la
            // velocidad (`turnRate * Clamp01(speed/maxSpeed)`): a velocidad CERO no
            // gira nada. Frenar antes del giro cerrado (fix anterior) reduce la
            // velocidad para no pasarse de largo, pero ESO MISMO le saca autoridad de
            // giro justo cuando más la necesita -- frenar de más y no poder doblar son
            // la MISMA perilla, tirando para lados opuestos. Solución: no pelear más
            // con la velocidad, compensar con más steerGain SOLO dentro del lote --
            // así el auto puede cerrar el giro aunque venga más lento.
            float lotSteerGain = inLotZone ? steerGain * 3f : steerGain;

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
            bool nearForAim = dist < arriveRadius * 2.5f;
            bool nearForStop = dist < arriveRadius * 1.5f;
            Vector2 aim = target;
            if (nearForAim && !isLastWaypoint) aim = waypoints[_index + 1];

            float steer = 0f;
            if (!(nearForStop && isLastWaypoint))
            {
                Vector3 toTarget = new Vector3(aim.x - p.x, 0f, aim.y - p.z);
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
            car.externalSteer = steer;
        }
    }
}
