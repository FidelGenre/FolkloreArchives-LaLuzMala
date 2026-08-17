// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  DogController.cs — el perro. Tres modos:
//    Follow  = IA, sigue a la persona (modo solo, por defecto)
//    Player  = lo controlás vos (WASD/flechas para girar + avanzar)
//    Idle    = quieto
//  Locomoción sobre CharacterController (no crouch/estamina/linterna:
//  es un cuadrúpedo, no la persona).
//  Modelo: "PS1 Dog" by Jo_Zinn5632 — CC-BY (ver DEV_LOG.md).
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace FolkloreArchives
{
    [RequireComponent(typeof(CharacterController))]
    public class DogController : MonoBehaviour
    {
        public enum Mode { Idle, Follow, Player }
        public Mode mode = Mode.Follow;

        [Header("Locomoción")]
        public float walkSpeed = 3.2f;
        public float runSpeed  = 6.5f;
        public float turnSpeed = 200f;   // grados/seg (giro de la IA)
        public float mouseSensitivity = 0.08f; // giro con mouse (modo jugador)
        public float gravity   = 18f;
        public float jumpHeight = 0.9f;   // saltar (Espacio)
        [HideInInspector] public bool IsGrounded; // lo lee DogAudio para no sonar pasos en el aire
        public float crouchRatio = 0.5f;  // agacharse (Ctrl/C): baja a esta fracción del alto

        // owner: "que dando doble click con el espacio pueda volar... tanto en perro
        // como jugador 1" -- mismo feature de debug que MapExplorer, solo activo en
        // Mode.Player (no tiene sentido que la IA de Follow vuele sola).
        [Header("Vuelo (debug, doble Espacio, solo Mode.Player)")]
        public float flySpeed = 30f; // owner: "va muy lento" -- era 8, para recorrer el mapa rápido
        public float doubleTapWindow = 0.3f;
        bool flying;
        float lastSpaceTapTime = -10f;

        [Header("Follow (IA)")]
        public Transform followTarget;             // la persona
        public float followStopDistance = 3.0f;    // se planta a esta distancia
        public float followRunDistance  = 8f;      // trota si está más lejos que esto
        public float followTeleportDistance = 45f; // si quedó MUY lejos (atascado), aparece al lado

        [Header("Input Jugador 2 (co-op, teclado compartido)")]
        // Si es true, en modo Player usa las FLECHAS (jugador 2). Si es false, WASD
        // (jugador 1). En modo solo el perro se controla con WASD (useArrowKeys=false).
        public bool useArrowKeys = false;

        CharacterController cc;
        float verticalVel;
        Transform camT;
        float standHeight, camBaseY;
        float pitch;   // mirar arriba/abajo (grados); parte del ángulo base de la cámara

        // owner: bajar del auto "me cambia la camara" -- este script guarda su propio
        // pitch (privado) y lo reaplica ni bien se reactiva (al subir/bajar del auto,
        // PlayerVehicleInteractor lo deshabilita/habilita igual que a MapExplorer); sin
        // esto quedaría desactualizado y pisaría la vista recién puesta al bajar.
        public void SetLookPitch(float p) { pitch = Mathf.Clamp(p, -80f, 80f); }

        // --- animación (perro riggeado: Idle/Walk/Run/Lie) ---
        Animator animator;
        int curAnim;
        static readonly int H_Idle = Animator.StringToHash("Idle");
        static readonly int H_Walk = Animator.StringToHash("Walk");
        static readonly int H_Run  = Animator.StringToHash("Run");
        static readonly int H_Lie  = Animator.StringToHash("Lie");

        void Start()
        {
            cc = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            standHeight = cc.height;
            if (GetComponent<FolkloreArchives.DogAudio>() == null) gameObject.AddComponent<FolkloreArchives.DogAudio>(); // ladridos
            // owner: "sigue spawneando bajo tierra" -- por si Terrain.activeTerrain
            // todavía no estaba resuelto en este momento del orden de Start()
            // (silenciosamente desactivaba TODO el anclaje sin ningún error), ya no se
            // cachea acá: GroundY() lo busca en vivo cada vez que hace falta.
            Debug.Log($"[DOG-GROUND] Start: pos={transform.position} terrain={(Terrain.activeTerrain != null ? Terrain.activeTerrain.name : "NULL")}");
            var camGo = GetComponentInChildren<Camera>(true);
            if (camGo != null)
            {
                camT = camGo.transform;
                camBaseY = camT.localPosition.y;
                pitch = camT.localEulerAngles.x;   // respeta la inclinación inicial (3ª persona solo)
                if (pitch > 180f) pitch -= 360f;
            }
        }

        void Update()
        {
            // doble-tap de Espacio prende/apaga el vuelo de debug -- solo tiene
            // sentido controlado (Mode.Player); si se apaga el modo jugador con el
            // vuelo prendido, se apaga solo para no dejar la IA de Follow volando.
            if (mode == Mode.Player)
            {
                var kbFly = Keyboard.current;
                if (kbFly != null && kbFly.spaceKey.wasPressedThisFrame)
                {
                    if (Time.time - lastSpaceTapTime < doubleTapWindow) { flying = !flying; verticalVel = 0f; }
                    lastSpaceTapTime = Time.time;
                }
                // owner: el perro ladra SOLO cuando lo controlás y apretás B (no automático)
                if (kbFly != null && kbFly.bKey.wasPressedThisFrame)
                    GetComponent<DogAudio>()?.Bark();
            }
            else if (flying) flying = false;

            Vector3 planar = Vector3.zero;
            switch (mode)
            {
                case Mode.Player: planar = PlayerMove(); break;
                case Mode.Follow: planar = FollowMove(); break;
            }

            bool grounded = cc.isGrounded;
            IsGrounded = grounded;
            if (flying)
            {
                var kb = Keyboard.current;
                float vert = kb == null ? 0f : (kb.spaceKey.isPressed ? 1f : 0f) - ((kb.leftCtrlKey.isPressed || kb.cKey.isPressed) ? 1f : 0f);
                verticalVel = vert * flySpeed;
            }
            else if (grounded) verticalVel = -1f;
            else verticalVel -= gravity * Time.deltaTime;

            if (mode == Mode.Player && !flying) Jump(grounded);   // saltar solo cuando lo controlás (el perro no se agacha)

            Vector3 before = transform.position;
            Vector3 move = planar;
            move.y = verticalVel;
            var flags = cc.Move(move * Time.deltaTime);

            // owner: "sigue spawneando el perro bajo tierra las patas" -- el intento
            // anterior solo corregía cuando grounded==true, pero si YA nace superpuesto
            // con el terreno (spawn embebido), un CharacterController casi nunca reporta
            // grounded=true sobre un overlap inicial -- nunca se disparaba. Ahora es
            // incondicional: si el pivote quedó POR DEBAJO de donde debería apoyar (el
            // terreno real en esta XZ), se sube ahí directo, nazca embebido o se haya
            // hundido de a poco caminando. Nunca interfiere con saltar hacia ARRIBA
            // (ahí ya está por ENCIMA del target, la condición no entra).
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float groundY = terrain.SampleHeight(transform.position) + terrain.transform.position.y;
                float bottomOffset = cc.center.y - cc.height * 0.5f; // pies relativos al pivote
                float targetY = groundY - bottomOffset;
                if (transform.position.y < targetY - 0.001f)
                {
                    if (Time.time < 3f) Debug.Log($"[DOG-GROUND] corrigiendo: y={transform.position.y:0.000} target={targetY:0.000} (groundY={groundY:0.000}, bottomOffset={bottomOffset:0.000})");
                    cc.enabled = false;
                    transform.position = new Vector3(transform.position.x, targetY, transform.position.z);
                    cc.enabled = true;
                    if (verticalVel < 0f) verticalVel = -1f; // ya tocó piso, no seguir acumulando caída
                }
            }
            else if (Time.time < 1f) Debug.LogWarning("[DOG-GROUND] Terrain.activeTerrain es NULL -- no se puede anclar.");

            // AUTO-SALTO: SOLO en modo Follow (IA). Controlado, el salto lo hace el
            // jugador (Espacio). Detecta que está BLOQUEADO: quería avanzar pero apenas
            // se movió (o chocó de costado). Más fiable que depender solo de Sides, que
            // no siempre se activa según la forma del obstáculo.
            if (mode == Mode.Follow && grounded && planar.sqrMagnitude > 0.05f && Time.time >= _nextAutoJump)
            {
                Vector3 actual = transform.position - before; actual.y = 0f;
                float wanted = new Vector2(planar.x, planar.z).magnitude * Time.deltaTime;
                bool blocked = (flags & CollisionFlags.Sides) != 0 || actual.magnitude < wanted * 0.5f;
                if (blocked)
                {
                    verticalVel = Mathf.Sqrt(2f * gravity * jumpHeight);
                    _nextAutoJump = Time.time + 0.6f;   // cooldown para no saltar en loop
                    GetComponent<DogAudio>()?.PlayJump();
                }
            }

            UpdateAnim(new Vector2(planar.x, planar.z).magnitude);
        }
        float _nextAutoJump;

        // elige la animación según la velocidad y el modo. Quieto (Idle mode) = echado (Lie).
        void UpdateAnim(float speed)
        {
            if (animator == null) return;
            int target;
            if (speed > runSpeed * 0.6f) target = H_Run;
            else if (speed > 0.25f)      target = H_Walk;
            else                         target = (mode == Mode.Idle) ? H_Lie : H_Idle;
            if (target != curAnim) { animator.CrossFade(target, 0.18f); curAnim = target; }
        }

        // Espacio = saltar (si está en el piso). El perro NO se agacha (pedido del dueño).
        void Jump(bool grounded)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (grounded && kb.spaceKey.wasPressedThisFrame)
            {
                verticalVel = Mathf.Sqrt(2f * gravity * jumpHeight);
                GetComponent<DogAudio>()?.PlayJump();
            }
        }

        // --- controlado por el jugador (1ª persona: mouse gira, WASD mueve) ---
        Vector3 PlayerMove()
        {
            var kb = Keyboard.current;
            if (kb == null || SettingsMenu.IsOpen) return Vector3.zero;

            // girar con el MOUSE: X gira el cuerpo (yaw), Y inclina la cámara (pitch:
            // mirar abajo para verte las patas / arriba). El pitch va en la CÁMARA, no
            // en el cuerpo, para no volcar al perro.
            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = mouse.delta.ReadValue();
                transform.Rotate(0f, d.x * mouseSensitivity, 0f);
                if (camT != null)
                {
                    pitch = Mathf.Clamp(pitch - d.y * mouseSensitivity, -80f, 80f);
                    camT.localEulerAngles = new Vector3(pitch, 0f, 0f);
                }
            }

            float strafe, fwd; bool run;
            if (useArrowKeys) // jugador 2 (co-op local, no usado en online)
            {
                strafe = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
                fwd    = (kb.upArrowKey.isPressed   ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
                run    = kb.rightShiftKey.isPressed;
            }
            else // WASD (jugador 1 / online / solo)
            {
                strafe = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                fwd    = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                run    = kb.leftShiftKey.isPressed;
            }

            float speed = run ? runSpeed : walkSpeed;
            Vector3 move = transform.forward * fwd + transform.right * strafe; // A/D = lateral
            if (move.sqrMagnitude > 1f) move.Normalize();
            return move * speed;
        }

        // --- IA: seguir a la persona ---
        // owner: "Rufus tiene que ir por donde voy yo (atrás mío), si no no pasa por las puertas y
        // choca las paredes". Rastro de MIGAS: guardo las posiciones por donde pasó la persona y el
        // perro las sigue en orden -> retoma tu CAMINO (cruza puertas, rodea paredes) en vez de ir
        // derecho contra las cosas.
        readonly System.Collections.Generic.List<Vector3> _trail = new System.Collections.Generic.List<Vector3>();
        const float TrailSample = 0.4f;   // deja una miga cada 0.4 m de la persona
        const float TrailReach  = 0.6f;   // la miga se da por alcanzada a esta distancia

        static float FlatDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        Vector3 FollowMove()
        {
            if (followTarget == null) return Vector3.zero;

            // dejar migas por donde va la persona
            if (_trail.Count == 0 || FlatDist(_trail[_trail.Count - 1], followTarget.position) >= TrailSample)
                _trail.Add(followTarget.position);
            if (_trail.Count > 256) _trail.RemoveAt(0);

            float distPlayer = FlatDist(followTarget.position, transform.position);

            // atascado/perdido → reaparece detrás de la persona (y limpio el rastro)
            if (distPlayer > followTeleportDistance)
            {
                Vector3 behind = followTarget.position - followTarget.forward * followStopDistance;
                var cont = GetComponent<CharacterController>();
                cont.enabled = false;
                transform.position = behind;
                cont.enabled = true;
                _trail.Clear();
                return Vector3.zero;
            }

            // consumir las migas ya alcanzadas
            while (_trail.Count > 0 && FlatDist(transform.position, _trail[0]) <= TrailReach)
                _trail.RemoveAt(0);

            // owner: "se me mete adentro y escucho sus pisadas todo el tiempo" -> se PLANTA cuando
            // está cerca tuyo (distancia directa), sin importar las migas, así no se te encima ni
            // camina sin parar. Limpio el rastro para arrancar de nuevo cuando te alejes.
            if (distPlayer <= followStopDistance) { _trail.Clear(); return Vector3.zero; }

            // ir hacia la miga más vieja (tu camino); si no hay, directo a la persona
            Vector3 goal = _trail.Count > 0 ? _trail[0] : followTarget.position;
            Vector3 to = goal - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 1e-4f) return Vector3.zero;
            FaceToward(to);
            float speed = distPlayer > followRunDistance ? runSpeed : walkSpeed;
            return to.normalized * speed;
        }

        void FaceToward(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
