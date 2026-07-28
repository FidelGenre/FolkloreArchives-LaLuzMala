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
        // owner: "de camino ir mas lento" -- crucero más bajo en la ruta principal.
        public float cruiseThrottle = 0.4f;
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
            // la posición real). Ahora también avanza si el waypoint quedó DETRÁS
            // nuestro (producto punto negativo con transform.forward), sin importar
            // qué tan lejos haya pasado -- garantiza que el índice siempre progresa a
            // medida que el auto avanza por la ruta.
            Vector3 toTargetNow = new Vector3(target.x - p.x, 0f, target.y - p.z);
            bool passedWaypoint = Vector3.Dot(transform.forward, toTargetNow) < 0f;
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
            // owner: "deberia doblar antes" -- 2.5x seguía quedando corto para el giro
            // cerrado hacia el lote. Más radio de anticipación (~32m).
            bool isLastWaypoint = _index == waypoints.Length - 1;
            bool nearForAim = dist < arriveRadius * 4f;
            bool nearForStop = dist < arriveRadius * 1.5f;
            Vector2 aim = target;
            if (nearForAim && !isLastWaypoint) aim = waypoints[_index + 1];

            float steer = 0f;
            if (!(nearForStop && isLastWaypoint))
            {
                Vector3 toTarget = new Vector3(aim.x - p.x, 0f, aim.y - p.z);
                float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
                steer = Mathf.Clamp(angle / 45f, -1f, 1f) * steerGain;
            }

            // owner: "se frena antes de entrar al pavimento" -- remaining sumaba TODOS
            // los tramos que faltan, incluido el último tramo de RUTA (antes de doblar
            // hacia el lote); si ese tramo + el giro + el estacionamiento ya sumaban
            // menos de slowdownDistance, el auto empezaba a frenar todavía en la ruta,
            // antes de siquiera doblar hacia la YPF. La zona de frenado ahora solo
            // aplica en los últimos 3 waypoints horneados por CarBuilder (los DOS giros
            // hacia ADENTRO del lote + el punto de estacionar) -- en la ruta, siempre a
            // velocidad crucero. 3 en vez de 2: owner reportó después "no esta entrando
            // al pavimento, sigue trabando" -- el giro cerrado a velocidad crucero se
            // pasaba de largo sin capturar el waypoint; entrar más lento a la zona de
            // giro (no solo al tramo final) ayuda al steer a completarlo a tiempo.
            // owner: "deberia... frenarse antes" -- un waypoint más de margen (incluye
            // el último tramo de RUTA, no solo los 2 giros + estacionar) para que el
            // frenado pueda empezar un poco antes de llegar a la zona de giro.
            bool inLotZone = _index >= waypoints.Length - 4;

            // owner: "no frena el auto choca" -- soltar el acelerador solo desacelera
            // con coastDecel (suave); adentro de la zona de frenado hay que FRENAR de
            // verdad (throttle negativo → CarController usa brakeDecel, mucho más
            // fuerte) si la velocidad actual supera lo que "debería" tener a esta
            // distancia del final.
            float throttle;
            if (inLotZone && remaining < slowdownDistance)
            {
                float targetSpeed = car.maxSpeed * Mathf.Clamp01(remaining / slowdownDistance);
                float currentSpeed = car.SpeedKmh / 3.6f;
                if (currentSpeed > targetSpeed + 0.5f)
                    throttle = -0.6f; // frenar activo
                else if (remaining < arriveRadius)
                    // owner: "no frena del todo el auto se queda trancado andando" --
                    // este empuje chiquito (cruiseThrottle*0.3) nunca llegaba a CERO,
                    // así que tan cerca del punto final el auto seguía reptando para
                    // siempre sin nunca entrar en el radio de "llegada". Sin acelerador
                    // ahí, frena solo por resistencia (coastDecel) hasta pararse de verdad.
                    throttle = 0f;
                else
                    throttle = cruiseThrottle * 0.3f;
            }
            else
            {
                throttle = cruiseThrottle;
            }

            car.autoPilot = true;
            car.externalThrottle = throttle;
            car.externalSteer = steer;
        }
    }
}
