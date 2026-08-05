// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarController.cs — manejo ARCADE con Rigidbody (estable en
//  terreno/camino). WASD/flechas: acelerar, frenar, retroceder,
//  doblar. Se activa cuando el jugador se sube (driving=true), lo
//  maneja PlayerVehicleInteractor. Asientos como anclas para la
//  cámara (adelante x2, atrás x2). Faros: F los prende/apaga mientras
//  manejás (PlayerVehicleInteractor apaga la linterna del jugador al
//  subirte de conductor). Radio: pendiente.
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Header("Manejo (arcade)")]
        public float maxSpeed      = 15f;   // m/s (~54 km/h)
        public float reverseSpeed  = 5f;
        public float accel         = 9f;    // m/s²
        public float brakeDecel    = 22f;   // frenar (S contra la marcha)
        public float coastDecel    = 4f;    // soltar el acelerador
        public float turnRate      = 60f;   // grados/seg a máxima velocidad

        [Header("Asientos (anclas de cámara)")]
        public Transform driverSeat;
        public Transform frontPassenger;
        public Transform rearLeft;
        public Transform rearRight;
        // owner: "quiero que haya un asiento extra en el auto, en la parte de atras en
        // medio" -- 5to asiento (banco trasero apretado a 3), pensado para que el perro
        // (o un 2º jugador en co-op) tenga dónde sentarse ahora que los 3 amigos ocupan
        // los otros asientos de forma decorativa.
        public Transform rearMid;

        [Header("Puerta del conductor (pivote que gira al subir)")]
        public Transform driverDoor;      // pivote de la puerta
        public float doorOpenAngle = -68f; // grados que abre

        [Header("Puertas del modelo (para abrir/cerrar)")]
        public Transform[] doors;          // todas las puertas separadas del FBX

        [Header("Faros (owner: misma tecla F que la linterna del jugador)")]
        public Light[] headlights;
        [HideInInspector] public bool headlightsOn = false;

        [HideInInspector] public bool driving = false;

        // owner: "vamos todos en el auto desde el inicio de mapa hasta la gasolinera" --
        // el auto maneja SOLO durante esa secuencia de apertura (ver CarAutoDrive.cs y
        // OpeningDriveSequence.cs). Mismo camino de física que el manejo normal (probado
        // estable): autoPilot reemplaza el throttle/steer del teclado por estos dos
        // campos, sin tocar nada de FixedUpdate/velocidad/giro.
        [HideInInspector] public bool autoPilot = false;
        [HideInInspector] public float externalThrottle = 0f;
        [HideInInspector] public float externalSteer = 0f;

        Rigidbody rb;
        BoxCollider box;
        float speed;   // velocidad hacia adelante con signo
        float steer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();
            rb.mass = 1200f;
            rb.linearDamping = 0f;
            rb.angularDamping = 4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // No volcar: solo gira en Y; el resto lo maneja la gravedad + collider.
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.centerOfMass = new Vector3(0f, -0.4f, 0f); // bajo = estable
        }

        void Update()
        {
            float throttle = 0f;
            steer = 0f;
            var kb = Keyboard.current;
            if (autoPilot)
            {
                throttle = externalThrottle;
                steer = externalSteer;
            }
            else if (driving && kb != null && !SettingsMenu.IsOpen)
            {
                throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                         - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                steer    = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                         - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);

                // owner: "al entrar deberia apagarse mi linterna y usarse las del auto
                // con la misma tecla que la normal" -- F prende/apaga los faros
                // mientras manejás (PlayerVehicleInteractor ya apagó la linterna al
                // subirte al asiento del conductor).
                if (kb.fKey.wasPressedThisFrame) SetHeadlights(!headlightsOn);
            }

            // acelerar / frenar / retroceder / desacelerar
            if (throttle > 0.1f)
            {
                float a = speed < 0f ? brakeDecel : accel;   // si venías en reversa, frená primero
                speed = Mathf.MoveTowards(speed, maxSpeed, a * Time.deltaTime);
            }
            else if (throttle < -0.1f)
            {
                float a = speed > 0f ? brakeDecel : accel;
                speed = Mathf.MoveTowards(speed, -reverseSpeed, a * Time.deltaTime);
            }
            else
            {
                speed = Mathf.MoveTowards(speed, 0f, coastDecel * Time.deltaTime);
            }
        }

        void FixedUpdate()
        {
            // doblar solo con el auto en movimiento (como un auto real)
            if (Mathf.Abs(speed) > 0.3f)
            {
                float dir = Mathf.Sign(speed);
                float turn = steer * turnRate * Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed) * dir;
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn * Time.fixedDeltaTime, 0f));
            }
            // velocidad hacia adelante, manteniendo la vertical (gravedad/terreno)
            Vector3 fwd = transform.forward * speed;
            rb.linearVelocity = new Vector3(fwd.x, rb.linearVelocity.y, fwd.z);

            // owner: "se sigue cayendo, dame una solucion distinta" -- 2 intentos
            // (raycast simple, después RaycastAll ignorando al propio auto) seguían
            // fallando en algún tramo. Endurecido del todo: mientras autoPilot está
            // activo, la GRAVEDAD SE APAGA (rb.useGravity=false) -- así, aunque el
            // raycast no encuentre nada por algún motivo puntual (hueco, tramo raro
            // del terreno nuevo), el auto simplemente se QUEDA en su altura actual
            // en vez de caer -- caerse deja de ser posible por construcción, no solo
            // "menos probable". Cuando SÍ encuentra piso, lo sigue de cerca (rápido,
            // 30 m/s) para no despegarse en bajadas pronunciadas. Se restaura la
            // gravedad normal apenas autoPilot se apaga (manejo manual intacto).
            rb.useGravity = !autoPilot;
            if (autoPilot)
            {
                // Piso bajo el CENTRO del auto (como siempre) ...
                float hereY;
                bool hasHere = GroundYAt(rb.position, out hereY);
                if (hasHere)
                {
                    // ... y también alrededor: adelante, y a los dos COSTADOS (mitad
                    // del ancho del auto). Encontrado con telemetría (entrada a la
                    // YPF): mirar solo adelante no alcanzaba -- el auto seguía
                    // rozando/chocando repetido contra 'Terrain_Merged' en tramos
                    // donde el desnivel de tierra queda al COSTADO del camino (un
                    // lomo/cordón que corre en paralelo, no una subida de frente) --
                    // la trompa pasaba libre pero una de las puertas mordía el
                    // desnivel. Ahora se toma el punto MÁS ALTO entre el centro,
                    // adelante y los dos costados (mismo límite de escalón de 1.5m
                    // para no treparse a paredes/surtidores de verdad) -- el auto se
                    // eleva para pasar por encima de cualquier lomo cercano, no solo
                    // el que tiene justo enfrente.
                    float targetY = hereY;
                    float halfWidth = box != null ? box.size.x * 0.5f : 1.4f;
                    Vector3[] probes = {
                        rb.position + transform.forward * 2.5f,
                        rb.position + transform.forward * 1.2f + transform.right * halfWidth,
                        rb.position + transform.forward * 1.2f - transform.right * halfWidth,
                    };
                    foreach (var probe in probes)
                    {
                        if (GroundYAt(probe, out float probeY) && probeY > targetY && probeY - hereY <= 1.5f)
                            targetY = probeY;
                    }

                    Vector3 pos = rb.position;
                    pos.y = Mathf.MoveTowards(pos.y, targetY + 0.05f, 30f * Time.fixedDeltaTime);
                    rb.position = pos;
                    var v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;
                }
            }
        }

        // Altura del piso real (ignorando al propio auto y triggers) bajo un punto.
        bool GroundYAt(Vector3 probe, out float y)
        {
            var hits = Physics.RaycastAll(probe + Vector3.up * 5f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
                y = h.point.y;
                return true;
            }
            y = 0f;
            return false;
        }

        public float SpeedKmh => Mathf.Abs(speed) * 3.6f;

        public void SetHeadlights(bool on)
        {
            headlightsOn = on;
            if (headlights == null) return;
            foreach (var l in headlights) if (l != null) l.enabled = on;
        }
    }
}
