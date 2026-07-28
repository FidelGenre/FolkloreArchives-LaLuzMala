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
        public float cruiseThrottle = 0.55f;
        public float steerGain = 1f;
        // owner: "al llegar a la ypf no frena el auto choca" -- el frenado solo miraba
        // la distancia del ÚLTIMO tramo (waypoint a waypoint), pero el giro hacia
        // adentro del lote de la YPF agrega un tramo final CORTO -- el auto llegaba a
        // ese tramo todavía a velocidad crucero, sin espacio para frenar a tiempo.
        // Ahora slowdownDistance se mide contra la distancia TOTAL restante (sumando
        // TODOS los tramos que faltan, no solo el actual).
        public float slowdownDistance = 25f; // frena suave en los últimos metros antes del último waypoint

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

            if (dist < arriveRadius)
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

            // owner: "se pone a girar" -- MUY cerca del waypoint final, no perseguir
            // más el ángulo exacto (ruidoso a corta distancia, causaba el giro en el
            // lugar) -- ir derecho y solo frenar.
            bool finalApproach = _index == waypoints.Length - 1 && dist < arriveRadius * 1.5f;
            float steer = 0f;
            if (!finalApproach)
            {
                Vector3 toTarget = new Vector3(target.x - p.x, 0f, target.y - p.z);
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
            bool inLotZone = _index >= waypoints.Length - 3;

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
