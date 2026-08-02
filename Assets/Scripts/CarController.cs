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
        float speed;   // velocidad hacia adelante con signo
        float steer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
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

            // owner: "el auto se cae" -- en piloto automático el auto puede cruzar
            // zonas sin collider continuo (huecos entre el mesh de la ruta nueva y
            // el terreno, tramos todavía no calzados perfectos) y ahí cae por
            // gravedad de verdad. En vez de depender de que el camino horneado sea
            // geométricamente perfecto (varios intentos de trazarlo desde la malla
            // real fallaron esta sesión), mientras autoPilot está activo el auto se
            // "pega" a cualquier superficie que haya debajo (raycast hacia abajo)
            // en vez de caer -- inmune a huecos puntuales del camino. El manejo
            // MANUAL (jugador) no se toca, sigue 100% física real como siempre.
            // owner: "ahora se va volando hacia arriba" -- el raycast de arriba
            // chocaba con el collider DEL PROPIO AUTO (la caja principal, no
            // trigger, arrancando el rayo desde arriba pasa primero por su propio
            // techo) en vez de tocar el suelo real -- el auto se "pegaba" contra
            // su propia carrocería y subía en vez de bajar. RaycastAll + ignorar
            // cualquier hit que sea hijo de este mismo Transform (o triggers, como
            // los asientos/puertas) hasta encontrar el primer hit que sea terreno
            // de verdad.
            if (autoPilot)
            {
                var hits = Physics.RaycastAll(rb.position + Vector3.up * 3f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.transform == transform || h.transform.IsChildOf(transform)) continue; // ignora al propio auto
                    Vector3 pos = rb.position;
                    pos.y = Mathf.MoveTowards(pos.y, h.point.y + 0.05f, 10f * Time.fixedDeltaTime);
                    rb.position = pos;
                    if (rb.linearVelocity.y < 0f)
                    {
                        var v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;
                    }
                    break;
                }
            }
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
