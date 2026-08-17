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

            // owner: "no me deja doblar". La fricción del BoxCollider contra el piso CANCELA la
            // rotación (el avance lo forzamos por velocidad, pero el giro lo come la fricción del
            // contacto). Collider SIN fricción (truco de auto arcade) -> el giro se aplica libre.
            if (box != null)
            {
                var slick = new PhysicsMaterial("CarSlick")
                {
                    dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                box.material = slick;
            }

            // ruido de motor procedural (sin assets) -- se agrega solo si falta, así no
            // hace falta regenerar el auto para que suene.
            if (GetComponent<CarEngineSound>() == null) gameObject.AddComponent<CarEngineSound>();
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
            // owner: si estás controlando al PERRO, el auto NO lee WASD (si no, movías al perro y
            // al auto a la vez). Sin input -> coast: el auto frena solo. Al volver a la persona, manejás.
            else if (driving && kb != null && !SettingsMenu.IsOpen && !PartyController.DogControlled)
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
            // owner: "los personajes (perro/humano/cualquier NPC) no deberían chocar el
            // auto y correrlo -- el auto debería ser inmóvil si no se maneja". Cuando no
            // lo maneja nadie (ni el jugador ni el autopilot), el Rigidbody pasa a
            // KINEMATIC: los NPCs chocan contra él pero NO lo empujan ni lo hacen girar.
            // Vuelve a dinámico apenas alguien lo maneja (jugador o secuencia de apertura).
            bool driven = driving || autoPilot;
            bool shouldBeKinematic = !driven;
            if (rb.isKinematic != shouldBeKinematic) rb.isKinematic = shouldBeKinematic;
            if (!driven)
            {
                speed = 0f;
                // owner: "está flotando el auto antes de subirme, bajalo como estaba" --
                // al ser kinematic ya no lo asienta la gravedad. Lo apoyamos nosotros:
                // bajamos el auto hasta que la base de su collider toque el piso (el
                // fondo del BoxCollider está en el origen del auto, así que la Y del piso
                // es directo la Y del auto). Mismo raycast que usa el autopilot.
                if (GroundYAt(rb.position, out float restY))
                {
                    Vector3 p = rb.position; p.y = restY; rb.position = p;
                }
                return; // inmóvil: no aplicar velocidad ni giro
            }

            // doblar solo con el auto en movimiento (como un auto real). Uso angularVelocity (no
            // MoveRotation): en un Rigidbody DINÁMICO, MoveRotation lo pelea la fricción del piso y
            // el auto no giraba. Con un mínimo de maniobra dobla bien aunque no vaya a full.
            if (Mathf.Abs(speed) > 0.2f)
            {
                float dir = Mathf.Sign(speed);
                float speedFactor = Mathf.Max(0.35f, Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed));
                float turnDegPerSec = steer * turnRate * speedFactor * dir;
                rb.angularVelocity = new Vector3(0f, turnDegPerSec * Mathf.Deg2Rad, 0f);
            }
            else rb.angularVelocity = Vector3.zero;
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
                    // adelante y los dos costados (mismo límite de escalón, ver stepLimit
                    // para no treparse a paredes/surtidores de verdad) -- el auto se
                    // eleva para pasar por encima de cualquier lomo cercano, no solo
                    // el que tiene justo enfrente.
                    float targetY = hereY;
                    // owner: seguía chocando/trabándose justo en la costura tierra↔
                    // cemento de la YPF pese a todo lo anterior -- la malla de la ruta
                    // (RoadsideBuilder.BuildPavedRoadMesh) tiene un "faldón" vertical
                    // de 2.5m en los bordes A PROPÓSITO (tapa la costura visual con el
                    // terreno) -- el límite de escalón de 1.5m rechazaba justo ESE
                    // desnivel conocido (lo trataba como pared de verdad). Subido a
                    // 2.7m para cubrirlo con margen.
                    const float stepLimit = 2.7f;
                    float halfWidth = box != null ? box.size.x * 0.5f : 1.4f;
                    // owner: seguía rozando en la entrada a la YPF incluso con estos
                    // sensores -- a 48km/h (~13 m/s) los 2.5m/1.2m fijos son solo
                    // ~0.2s de anticipación, muy poco para un giro cerrado a esa
                    // velocidad. Ahora la distancia de los sensores escala con la
                    // velocidad actual (~0.35s de anticipación), con un piso para
                    // cuando el auto está lento/parado.
                    float lookAhead = Mathf.Max(2.5f, Mathf.Abs(speed) * 0.35f);
                    float sideLookAhead = Mathf.Max(1.2f, Mathf.Abs(speed) * 0.2f);
                    Vector3[] probes = {
                        rb.position + transform.forward * lookAhead,
                        rb.position + transform.forward * sideLookAhead + transform.right * halfWidth,
                        rb.position + transform.forward * sideLookAhead - transform.right * halfWidth,
                    };
                    foreach (var probe in probes)
                    {
                        if (GroundYAt(probe, out float probeY) && probeY > targetY && probeY - hereY <= stepLimit)
                            targetY = probeY;
                    }

                    // owner: mismo lugar exacto (x≈520-525) chocando siempre, pase lo
                    // que pase con los sensores -- probablemente un desnivel fino
                    // entre el collider de la ruta y el del terreno que a 48km/h los
                    // sensores puntuales no siempre alcanzan a agarrar a tiempo.
                    // "hacelo que vaya más alto... no tan pegado al piso" -- en vez de
                    // seguir afinando la detección, más colchón de altura (0.05 -> 0.35)
                    // para que ese tipo de desnivel fino deje de importar.
                    Vector3 pos = rb.position;
                    pos.y = Mathf.MoveTowards(pos.y, targetY + 0.35f, 30f * Time.fixedDeltaTime);
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
