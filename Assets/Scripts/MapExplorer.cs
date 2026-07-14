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

namespace FolkloreArchives
{
    [RequireComponent(typeof(CharacterController))]
    public class MapExplorer : MonoBehaviour
    {
        public float walkSpeed = 2.6f;   // slower, tense walk (FtF-style)
        public float runSpeed = 5f;
        public float crouchSpeed = 1.6f;
        public float jumpHeight = 1.1f;
        public float gravity = 18f;
        public float mouseSensitivity = 0.08f;

        // Stamina: correr (Shift + moviéndose) la gasta; se regenera al no correr.
        // Al llegar a 0 quedás "exhausted" y no podés correr hasta recuperar
        // exhaustRecover. Barra en pantalla (OnGUI).
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

            // Crouch (hold Ctrl or C). Can't stand up if there's something overhead.
            bool wantCrouch = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            if (!wantCrouch && crouching && !CanStandUp()) wantCrouch = true; // blocked by ceiling
            crouching = wantCrouch;

            // smoothly lerp controller height + camera base toward the target pose
            float targetHeight = crouching ? crouchHeight : standHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            // Move
            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);

            // Stamina: solo corrés si apretás Shift, te estás moviendo, no estás
            // agachado, te queda estamina y no estás exhausto.
            bool movingNow = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f);
            bool running = kb.leftShiftKey.isPressed && movingNow && !crouching && stamina > 0f && !exhausted;
            if (running)
            {
                stamina -= staminaDrain * Time.deltaTime;
                if (stamina <= 0f) { stamina = 0f; exhausted = true; } // agotado: a caminar
            }
            else
            {
                stamina = Mathf.Min(maxStamina, stamina + staminaRegen * Time.deltaTime);
                if (exhausted && stamina >= exhaustRecover) exhausted = false; // ya podés correr de nuevo
            }

            float speed = crouching ? crouchSpeed : (running ? runSpeed : walkSpeed);
            Vector3 move = (transform.forward * v + transform.right * h).normalized * speed;

            // Head bob (camera sway while walking) + crouch height, combined.
            if (cam != null)
            {
                float targetBaseY = crouching ? camCrouchY : camStandY;
                camBaseY = Mathf.Lerp(camBaseY, targetBaseY, crouchLerpSpeed * Time.deltaTime);

                bool moving = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f) && controller.isGrounded;

                // Fade the bob AMPLITUDE in/out (never reset the timer - that was the
                // "jumping"). The phase stays continuous so it's always smooth.
                bobBlend = Mathf.Lerp(bobBlend, moving ? 1f : 0f, 9f * Time.deltaTime);
                if (moving) bobTimer += Time.deltaTime * bobSpeed * (speed / walkSpeed);

                float amp = (crouching ? 0.6f : 1f) * bobBlend;
                float bobY = Mathf.Sin(bobTimer * 2f) * bobAmount * amp; // vertical: 2 bounces per stride
                float bobX = Mathf.Sin(bobTimer) * bobSway * amp;        // horizontal: 1 sway per stride (figure-8)
                float roll = Mathf.Sin(bobTimer) * bobRoll * amp;        // subtle head tilt with the sway

                cam.localPosition = new Vector3(camBaseX + bobX, camBaseY + bobY, cam.localPosition.z);
                cam.localEulerAngles = new Vector3(pitch, 0f, roll);
            }

            // Gravity + jump
            if (controller.isGrounded)
            {
                verticalVelocity = -1f;
                // jump on Space (not while crouched)
                if (kb.spaceKey.wasPressedThisFrame && !crouching)
                    verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }
            move.y = verticalVelocity;
            controller.Move(move * Time.deltaTime);

            // Flashlight
            if (kb.fKey.wasPressedThisFrame && flashlight != null)
                flashlight.enabled = !flashlight.enabled;

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

            // Stamina bar (abajo a la izquierda). Alloc-free: DrawTexture + GUI.color.
            float barW = 220f, barH = 16f, bx = 14f;
            float by = Screen.height - barH - 16f;
            float frac = maxStamina > 0f ? Mathf.Clamp01(stamina / maxStamina) : 0f;
            var prev = GUI.color;
            // fondo
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(bx - 2f, by - 2f, barW + 4f, barH + 4f), Texture2D.whiteTexture);
            // relleno: rojo si exhausto, si no de amarillo (bajo) a verde (lleno)
            GUI.color = exhausted
                ? new Color(0.80f, 0.18f, 0.14f, 0.95f)
                : Color.Lerp(new Color(0.85f, 0.6f, 0.12f, 0.95f), new Color(0.32f, 0.72f, 0.26f, 0.95f), frac);
            GUI.DrawTexture(new Rect(bx, by, barW * frac, barH), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
