// ============================================================
//  FOLKLORE ARCHIVES - LA LUZ MALA
//  MapExplorer.cs — first-person test controller (runtime).
//  Uses the NEW Input System (matches this project's settings).
//
//  Controls: WASD move | Mouse look | Shift run | Space jump
//            Ctrl/C crouch | F flashlight | Esc release cursor | Click re-lock
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;
using WASDSound;

namespace FolkloreArchives
{
    [RequireComponent(typeof(CharacterController))]
    public class MapExplorer : MonoBehaviour
    {
        // owner: "sonidos... pisadas viento etc" -- WASDFootstepSource viene del pack
        // "WASD Footstep SFX Free Bundle" (Assets/ExternalAssets/WASDFootstepSFX/),
        // asignado en TestPlayerBuilder.cs/NetworkBuilder.cs al armar el jugador. La
        // superficie (pasto/barro/asfalto/roca) sale de TerrainSurfaceDetector, que
        // samplea el splat del Terrain bajo los pies -- no del raycast+Material que
        // trae el pack (acá el piso es UN SOLO Terrain con capas mezcladas, no
        // objetos separados por superficie).
        WASDFootstepSource footstepSource;
        bool wasGrounded = true;
        public float walkSpeed = 2.6f;   // slower, tense walk (FtF-style)
        public float runSpeed = 4.3f;    // owner: "un poco mas lento el sprint" (era 5)
        public float crouchSpeed = 1.6f;
        public float jumpHeight = 1.1f;
        public float gravity = 18f;
        public float mouseSensitivity = 0.08f;

        // owner: "que dando doble click con el espacio pueda volar como modo
        // creativo de minecraft... esto es solo por ahora para recorrer mientras
        // pruebo el mapa" -- feature de debug, no de gameplay final. Doble-tap de
        // Espacio prende/apaga; volando, Espacio mantenido = subir, Ctrl/C = bajar
        // (mismas teclas que agachar, que no tiene sentido en el aire), WASD sigue
        // siendo horizontal puro (transform.forward/right ya son solo yaw, la
        // inclinación de cámara no los afecta -- mismo comportamiento que el vuelo
        // creativo de Minecraft sin tocar nada del movimiento existente).
        [Header("Vuelo (debug, doble Espacio)")]
        public float flySpeed = 30f; // owner: "va muy lento" -- era 8, para recorrer el mapa rápido
        public float doubleTapWindow = 0.3f;
        bool flying;
        float lastSpaceTapTime = -10f;

        // Stamina: owner: "que la estamina siga estando pero infinita... que no este
        // mas la barra" -- se sigue llevando la cuenta (drena corriendo, se regenera
        // caminando) pero YA NO bloquea correr ni se muestra en pantalla; queda como
        // estado interno nomás, por si hace falta reactivarla más adelante.
        [Header("Stamina")]
        public float maxStamina = 100f;
        public float staminaDrain = 28f;    // por segundo mientras corrés
        public float staminaRegen = 18f;    // por segundo mientras NO corrés
        public float exhaustRecover = 30f;  // hay que regenerar hasta acá para volver a correr
        float stamina;
        bool exhausted;

        // crouch tuning
        public float standHeight = 1.8f;
        public float crouchHeight = 1.0f;
        public float crouchLerpSpeed = 10f;

        // head bob while walking (camera sway) - a smooth figure-8: vertical bounce at
        // 2x the horizontal sway, plus a tiny roll, faded in/out by bobBlend so it
        // never jumps.
        public float bobAmount = 0.045f;   // vertical bob height
        public float bobSway = 0.04f;      // horizontal sway
        public float bobRoll = 0.6f;       // camera roll (degrees) side to side
        public float bobSpeed = 8f;        // step cadence
        float bobBlend;                    // 0..1 how "in" the bob is (smooths start/stop)

        CharacterController controller;
        Transform cam;
        Light flashlight;
        float pitch;
        float verticalVelocity;

        // owner: bajar del auto "me cambia la camara para adelante" -- este script
        // guarda su propio pitch (privado) y lo reaplica ni bien se reactiva; si venía
        // de subirse/bajarse del auto y quedó desactualizado (con la última mirada de
        // ANTES de subir al auto), pisaba de golpe la cámara apenas volvía el control.
        // PlayerVehicleInteractor llama esto justo antes de reactivar el script.
        public void SetLookPitch(float p) { pitch = Mathf.Clamp(p, -85f, 85f); }

        bool suppressMoveUntilKeysReleased;
        void OnEnable() { suppressMoveUntilKeysReleased = true; }

        bool crouching;
        float camStandY;   // camera local Y when standing (captured at Start)
        float camCrouchY;  // camera local Y when crouched
        float camBaseY;    // current (crouch-smoothed) base Y, before head bob
        float camBaseX;    // resting local X
        float bobTimer;

        // on-screen FPS counter (refreshed a few times/sec so it's readable, not GC-spammy)
        float fpsTimer;
        int fpsFrames;
        float fpsDisplay;
        string fpsText = "-- FPS";   // rebuilt only when fpsDisplay updates (4x/sec), not per frame
        GUIStyle fpsStyle;           // cached once — creating it per OnGUI was allocating GC garbage every frame

        void Start()
        {
            controller = GetComponent<CharacterController>();
            standHeight = controller.height; // respect whatever the builder set
            footstepSource = GetComponent<WASDFootstepSource>();
            Camera c = GetComponentInChildren<Camera>();
            if (c != null)
            {
                cam = c.transform;
                flashlight = c.GetComponentInChildren<Light>();
                camStandY = cam.localPosition.y;
                camCrouchY = camStandY - (standHeight - crouchHeight); // drop the view by the height lost
                camBaseY = camStandY;
                camBaseX = cam.localPosition.x;
            }
            Cursor.lockState = CursorLockMode.Locked;
            stamina = maxStamina;

            SnapToGround(); // apoyar sobre el suelo real al arrancar (evita spawn enterrado)
        }

        // Control externo de la linterna (PlayerVehicleInteractor la apaga al subirte
        // al auto -- owner: "al entrar deberia apagarse mi linterna" -- y la restaura
        // tal cual estaba al bajarte). Funciona aunque este componente esté
        // "enabled=false" (mientras estás sentado), porque solo toca el Light, no
        // depende de que Update() esté corriendo.
        public bool FlashlightOn => flashlight != null && flashlight.enabled;
        public void SetFlashlight(bool on) { if (flashlight != null) flashlight.enabled = on; }

        // Apoya al jugador sobre la superficie real (terreno/ruta/puente) tirando un
        // raycast hacia abajo desde bien arriba de su XZ. Así, aunque la posición
        // guardada no coincida con el terreno actual, siempre aparecés parado sobre el
        // piso — nunca enterrado ni flotando. Se desactiva el CharacterController un
        // instante para que el rayo no choque contra la propia cápsula.
        void SnapToGround()
        {
            if (controller == null) return;
            bool ccWasOn = controller.enabled;
            controller.enabled = false;

            Vector3 from = transform.position + Vector3.up * 60f;
            var hits = Physics.RaycastAll(from, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.NegativeInfinity; bool found = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue; // ignorar colliders propios
                if (h.point.y > bestY) { bestY = h.point.y; found = true; } // superficie más alta = piso
            }
            if (found)
                transform.position = new Vector3(transform.position.x, bestY + 0.1f, transform.position.z);

            verticalVelocity = 0f;
            if (ccWasOn) controller.enabled = true;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            if (SettingsMenu.IsOpen) return; // menú de opciones abierto: no mover/mirar
            if (controller == null || !controller.enabled) return; // arriba del auto: el CC está apagado

            // Look
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, delta.x, 0f);
                pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
                if (cam != null) cam.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }

            // Doble-tap de Espacio: prende/apaga el vuelo de debug. wasPressedThisFrame
            // ya dispara una sola vez por toque (no por mantenido), así que compararlo
            // contra el toque anterior alcanza para detectar el doble click.
            if (kb.spaceKey.wasPressedThisFrame)
            {
                if (Time.time - lastSpaceTapTime < doubleTapWindow)
                {
                    flying = !flying;
                    verticalVelocity = 0f;
                }
                lastSpaceTapTime = Time.time;
            }

            // Crouch (hold Ctrl or C). Can't stand up if there's something overhead.
            // Volando, Ctrl/C pasan a ser "bajar" (ver más abajo) -- no tiene sentido
            // agacharse en el aire.
            bool wantCrouch = !flying && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed);
            if (!wantCrouch && crouching && !CanStandUp()) wantCrouch = true; // blocked by ceiling
            crouching = wantCrouch;

            // smoothly lerp controller height + camera base toward the target pose
            float targetHeight = crouching ? crouchHeight : standHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            // Move
            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

            // owner: "se me sigue corriendo la vista luego de bajarme hacia adelante" --
            // W también acelera el auto; si te bajás sin soltarlo, este script se
            // reactiva con W todavía apretado y arrancabas a caminar solo, de golpe.
            // Bloqueo el movimiento hasta que sueltes TODO WASD al menos una vez.
            if (suppressMoveUntilKeysReleased)
            {
                if (!kb.wKey.isPressed && !kb.aKey.isPressed && !kb.sKey.isPressed && !kb.dKey.isPressed)
                    suppressMoveUntilKeysReleased = false;
                else
                    h = v = 0f;
            }

            // Stamina: owner: "necesito que la estamina siga estando pero infinita... no
            // este mas la barra" -- el sistema sigue llevando la cuenta (por si hace
            // falta más adelante), pero ya NO bloquea correr: sin el "&& stamina > 0f &&
            // !exhausted" de antes, el jugador nunca se queda sin aire, y la barra en
            // pantalla se sacó (ver OnGUI).
            bool movingNow = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f);
            bool running = kb.leftShiftKey.isPressed && movingNow && !crouching;
            if (running)
            {
                stamina -= staminaDrain * Time.deltaTime;
                if (stamina <= 0f) { stamina = 0f; exhausted = true; }
            }
            else
            {
                stamina = Mathf.Min(maxStamina, stamina + staminaRegen * Time.deltaTime);
                if (exhausted && stamina >= exhaustRecover) exhausted = false;
            }

            float speed = flying ? flySpeed : (crouching ? crouchSpeed : (running ? runSpeed : walkSpeed));
            Vector3 move = (transform.forward * v + transform.right * h).normalized * speed;

            // Head bob (camera sway while walking) + crouch height, combined.
            if (cam != null)
            {
                float targetBaseY = crouching ? camCrouchY : camStandY;
                camBaseY = Mathf.Lerp(camBaseY, targetBaseY, crouchLerpSpeed * Time.deltaTime);

                bool moving = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f) && controller.isGrounded && !flying;

                // Fade the bob AMPLITUDE in/out (never reset the timer - that was the
                // "jumping"). The phase stays continuous so it's always smooth.
                bobBlend = Mathf.Lerp(bobBlend, moving ? 1f : 0f, 9f * Time.deltaTime);
                if (moving)
                {
                    // owner: "sonidos... pisadas" -- el bob vertical usa sin(bobTimer*2),
                    // 2 golpes de pie por vuelta completa de bobTimer -- un "paso" pasa
                    // cada vez que bobTimer cruza un múltiplo de π (medio ciclo). Se
                    // detecta comparando el "número de medio-ciclo" antes/después de
                    // avanzar el timer este frame, en vez de re-samplear el seno (que no
                    // dice de qué LADO viene el cruce).
                    float prevTimer = bobTimer;
                    bobTimer += Time.deltaTime * bobSpeed * (speed / walkSpeed);
                    if (footstepSource != null && Mathf.FloorToInt(bobTimer / Mathf.PI) > Mathf.FloorToInt(prevTimer / Mathf.PI))
                    {
                        var action = crouching ? WASDEnumAction.Sneak : (running ? WASDEnumAction.Run : WASDEnumAction.Walk);
                        footstepSource.PlayFootstepByAction(action, TerrainSurfaceDetector.At(transform.position));
                    }
                }

                float amp = (crouching ? 0.6f : 1f) * bobBlend;
                float bobY = Mathf.Sin(bobTimer * 2f) * bobAmount * amp; // vertical: 2 bounces per stride
                float bobX = Mathf.Sin(bobTimer) * bobSway * amp;        // horizontal: 1 sway per stride (figure-8)
                float roll = Mathf.Sin(bobTimer) * bobRoll * amp;        // subtle head tilt with the sway

                cam.localPosition = new Vector3(camBaseX + bobX, camBaseY + bobY, cam.localPosition.z);
                cam.localEulerAngles = new Vector3(pitch, 0f, roll);
            }

            if (flying)
            {
                // sin gravedad -- Espacio mantenido sube, Ctrl/C mantenido baja (mismo
                // criterio que el vuelo creativo de Minecraft).
                float vert = (kb.spaceKey.isPressed ? 1f : 0f) - ((kb.leftCtrlKey.isPressed || kb.cKey.isPressed) ? 1f : 0f);
                verticalVelocity = vert * flySpeed;
                wasGrounded = controller.isGrounded; // no sonido de aterrizaje al apagar el vuelo cerca del piso
            }
            // Gravity + jump
            else if (controller.isGrounded)
            {
                // owner: "sonidos... salto" -- aterrizaje: transición de "en el aire" a
                // "en el piso" en este mismo frame (no lo dispara estar parado quieto).
                if (!wasGrounded && footstepSource != null)
                    footstepSource.PlayFootstepByAction(WASDEnumAction.Drop, TerrainSurfaceDetector.At(transform.position));
                wasGrounded = true;

                verticalVelocity = -1f;
                // jump on Space (not while crouched)
                if (kb.spaceKey.wasPressedThisFrame && !crouching)
                {
                    verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                    if (footstepSource != null)
                        footstepSource.PlayFootstepByAction(WASDEnumAction.Jump, TerrainSurfaceDetector.At(transform.position));
                }
            }
            else
            {
                wasGrounded = false;
                verticalVelocity -= gravity * Time.deltaTime;
            }
            move.y = verticalVelocity;
            controller.Move(move * Time.deltaTime);

            // Flashlight
            if (kb.fKey.wasPressedThisFrame && flashlight != null)
                flashlight.enabled = !flashlight.enabled;

            // (arriba del auto, esta clase queda "enabled = false" -- ver
            // PlayerVehicleInteractor, que llama a SetFlashlight/FlashlightOn
            // directo en vez de depender de este Update)

            // (el cursor y Esc ahora los maneja SettingsMenu — el menú de opciones)

            // FPS counter (updates 4x/sec)
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.25f)
            {
                fpsDisplay = fpsFrames / fpsTimer;
                fpsFrames = 0;
                fpsTimer = 0f;
                fpsText = fpsDisplay.ToString("0") + " FPS"; // string alloc only 4x/sec, not every frame
            }
        }

        // is there headroom to stand back up? (raycast up from the top of the crouched capsule)
        bool CanStandUp()
        {
            float extra = standHeight - crouchHeight;
            Vector3 top = transform.position + Vector3.up * controller.height;
            return !Physics.SphereCast(top, controller.radius * 0.9f, Vector3.up,
                out _, extra + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        }

        void OnGUI()
        {
            // cache the style once (creating a GUIStyle every OnGUI allocated garbage
            // every frame -> periodic GC hitches -> the FPS-minimum dips)
            if (fpsStyle == null)
                fpsStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            fpsStyle.normal.textColor = fpsDisplay < 30f ? Color.red : (fpsDisplay < 50f ? Color.yellow : Color.green);
            GUI.Box(new Rect(10, 10, 140, 28), GUIContent.none);
            GUI.Label(new Rect(10, 10, 140, 28), fpsText, fpsStyle);
        }
    }
}
