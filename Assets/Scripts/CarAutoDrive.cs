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
        public float arriveRadius = 5f;  // qué tan cerca hay que estar de un waypoint para pasar al siguiente
        public float cruiseThrottle = 0.55f;
        public float steerGain = 1f;
        public float slowdownDistance = 15f; // frena suave en los últimos metros antes del último waypoint

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

            Vector3 toTarget = new Vector3(target.x - p.x, 0f, target.y - p.z);
            float angle = Vector3.SignedAngle(transform.forward, toTarget, Vector3.up);
            float steer = Mathf.Clamp(angle / 45f, -1f, 1f) * steerGain;

            // frenar suave solo en el tramo final, hacia el último waypoint
            bool lastLeg = _index == waypoints.Length - 1;
            float throttle = cruiseThrottle;
            if (lastLeg && dist < slowdownDistance)
                throttle *= Mathf.Clamp01(dist / slowdownDistance);

            car.autoPilot = true;
            car.externalThrottle = throttle;
            car.externalSteer = steer;
        }
    }
}
