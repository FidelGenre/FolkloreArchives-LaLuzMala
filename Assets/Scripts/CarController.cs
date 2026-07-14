// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  CarController.cs — manejo ARCADE con Rigidbody (estable en
//  terreno/camino). WASD/flechas: acelerar, frenar, retroceder,
//  doblar. Se activa cuando el jugador se sube (driving=true), lo
//  maneja PlayerVehicleInteractor. Asientos como anclas para la
//  cámara (adelante x2, atrás x2). Faros/radio se enganchan en la
//  fase siguiente.
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

        [HideInInspector] public bool driving = false;

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
            if (driving && kb != null && !SettingsMenu.IsOpen)
            {
                throttle = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                         - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                steer    = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                         - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
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
        }

        public float SpeedKmh => Mathf.Abs(speed) * 3.6f;
    }
}
